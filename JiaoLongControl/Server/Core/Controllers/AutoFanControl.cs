using System.Runtime.InteropServices;
using JiaoLongControl.Server.Core.Models;
using JiaoLongControl.Server.Core.Utils;
using JiaoLongControl.Server.Interop;
using log4net;

namespace JiaoLongControl.Server.Core.Controllers;

public enum FanType
{
    CPU,
    GPU
}

[ComVisible(true)]
[ClassInterface(ClassInterfaceType.AutoDual)]
public class AutoFanControl : IDisposable
{
    private volatile bool _isRunning;
    private readonly ILog Logger = LogManager.GetLogger(typeof(AutoFanControl));
    private CancellationTokenSource? _cts;
    private Task? _controlTask;
    private const int IntervalMs = 1000;
    
    private const int RPM_UNIT_DIVISOR = 100; // 1 unit = 100 RPM
    private const int MAX_FAN_BYTE = 68;      // 68 * 100 = 6800 RPM
    private const int MIN_FAN_BYTE = 0;       // 0 RPM
    
    private const float AlphaUp = 0.35f;   
    private const float AlphaDown = 0.05f; 
    private const double MaxRampUpRpmPerSec = 800.0;  
    private const double MaxRampDownRpmPerSec = 150.0; 
    private const double MaxRampUpBytePerSec = MaxRampUpRpmPerSec / RPM_UNIT_DIVISOR;
    private const double MaxRampDownBytePerSec = MaxRampDownRpmPerSec / RPM_UNIT_DIVISOR;
    private const double SharedHeatPipeSyncRatio = 0.85; 

    private class FanState
    {
        public float SmoothedTemp { get; set; } = -1f;
        public double CurrentSpeedByte { get; set; } = -1f;
        public int LastAppliedByte { get; set; } = -1;
    }
    
    private readonly Dictionary<FanType, FanState> _states = new()
    {
        { FanType.CPU, new FanState() },
        { FanType.GPU, new FanState() }
    };

    public CommandResult IsRunning()
    {
        return new CommandResult(_isRunning, _isRunning ? "自动风扇控制正在运行" : "自动风扇控制没有在运行中");
    }

    public CommandResult Start()
    {
        if (_isRunning)
            return new CommandResult(_isRunning, "自动风扇控制已经运行中");
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _controlTask = Task.Factory.StartNew(
            () => ControlLoop(token),
            token,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default
        );
        return new CommandResult(_isRunning, "自动风扇控制启动");
    }

    public CommandResult Stop()
    {
        if (!_isRunning)
            return new CommandResult(!_isRunning, "自动风扇控制没有在运行中");
        Logger.Info("Auto Fan Control stopping...");
        _cts?.Cancel();
        try
        {
            _controlTask?.Wait(2000);
        }
        catch (AggregateException) { }
        catch (Exception ex)
        {
            Logger.Error(ex.Message);
        }
        finally
        {
            _isRunning = false;
        }
        return new CommandResult(_isRunning, "自动风扇控制已停止");
    }

    private void ControlLoop(CancellationToken token)
    {
        _isRunning = true;
        Logger.Info("Auto Fan Control started with Cross-Cooling Algorithm.");
        
        lock (_states)
        {
            _states[FanType.CPU] = new FanState();
            _states[FanType.GPU] = new FanState();
        }

        try
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    float rawCpuTemp = Convert.ToSingle(Bridge.Instance.CPU.GetCPUThermometer().Data);
                    float rawGpuTemp = Convert.ToSingle(Bridge.Instance.NvidiaGpu.GetGpuTemperature().Message);
                    float smoothedCpuTemp = UpdateSmoothedTemp(FanType.CPU, rawCpuTemp);
                    float smoothedGpuTemp = UpdateSmoothedTemp(FanType.GPU, rawGpuTemp);
                    int targetCpuByte = CalculateFanSpeed(smoothedCpuTemp, FanType.CPU);
                    int targetGpuByte = CalculateFanSpeed(smoothedGpuTemp, FanType.GPU);
                    int syncedCpuTarget = Math.Max(targetCpuByte, (int)(targetGpuByte * SharedHeatPipeSyncRatio));
                    int syncedGpuTarget = Math.Max(targetGpuByte, (int)(targetCpuByte * SharedHeatPipeSyncRatio));
                    ProcessAndApplyFanSpeed(FanType.CPU, syncedCpuTarget);
                    ProcessAndApplyFanSpeed(FanType.GPU, syncedGpuTarget);

                    Task.Delay(IntervalMs, token).Wait(token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Logger.Error($"ControlLoop Error: {ex.Message}");
                    Thread.Sleep(2000);
                }
            }
        }
        finally
        {
            _isRunning = false;
            Logger.Info("Auto Fan Control stopped.");
        }
    }
    
    private float UpdateSmoothedTemp(FanType type, float rawTemp)
    {
        FanState state;
        lock (_states) { state = _states[type]; }

        if (state.SmoothedTemp < 0)
        {
            state.SmoothedTemp = rawTemp;
        }
        else
        {
            float alpha = rawTemp > state.SmoothedTemp ? AlphaUp : AlphaDown;
            state.SmoothedTemp = (alpha * rawTemp) + ((1f - alpha) * state.SmoothedTemp);
        }
        return state.SmoothedTemp;
    }
    private void ProcessAndApplyFanSpeed(FanType type, int targetSpeedByte)
    {
        FanState state;
        lock (_states) { state = _states[type]; }
        if (state.CurrentSpeedByte < 0)
        {
            state.CurrentSpeedByte = targetSpeedByte;
        }
        else
        {
            double diff = targetSpeedByte - state.CurrentSpeedByte;
            if (diff > 0)
            {
                state.CurrentSpeedByte += Math.Min(diff, MaxRampUpBytePerSec);
            }
            else if (diff < 0)
            {
                state.CurrentSpeedByte -= Math.Min(-diff, MaxRampDownBytePerSec);
            }
        }

        int finalSpeedByte = (int)Math.Clamp(Math.Round(state.CurrentSpeedByte), MIN_FAN_BYTE, MAX_FAN_BYTE);
        if (finalSpeedByte == state.LastAppliedByte) return;

        int rpm = finalSpeedByte * RPM_UNIT_DIVISOR;
        if (type == FanType.CPU)
        {
            Bridge.Instance.Fan.CpuFanSetSpeed((byte)finalSpeedByte);
            Logger.Info($"CPU Temp: {state.SmoothedTemp:F1}°C | CPU Fan Applied: {rpm} RPM");
        }
        else if (type == FanType.GPU)
        {
            Bridge.Instance.Fan.GpuFanSetSpeed((byte)finalSpeedByte);
            Logger.Info($"GPU Temp: {state.SmoothedTemp:F1}°C | GPU Fan Applied: {rpm} RPM");
        }

        state.LastAppliedByte = finalSpeedByte;
    }
    private int CalculateFanSpeed(float currentTemp, FanType type)
    {
        var config = ConfigController.Config.AdvancedFanControlSystemConfig;
        if (config == null) return 25; 
        
        List<FanPoint> configPoints = type == FanType.CPU ? config.CpuFan : config.GpuFan;
        if (configPoints == null || configPoints.Count == 0)
            return 25; 
            
        var sortedPoints = configPoints.OrderBy(p => p.temp).ToList();
        double targetRpm;
        if (currentTemp <= sortedPoints.First().temp)
        {
            targetRpm = sortedPoints.First().speed;
        }
        else if (currentTemp >= sortedPoints.Last().temp)
        {
            targetRpm = sortedPoints.Last().speed;
        }
        else
        {
            targetRpm = sortedPoints.First().speed;
            for (int i = 0; i < sortedPoints.Count - 1; i++)
            {
                var p1 = sortedPoints[i];
                var p2 = sortedPoints[i + 1];

                if (currentTemp >= p1.temp && currentTemp <= p2.temp)
                {
                    double ratio = (currentTemp - p1.temp) / (double)(p2.temp - p1.temp);
                    targetRpm = p1.speed + (p2.speed - p1.speed) * ratio;
                    break;
                }
            }
        }
        int targetByte = (int)Math.Round(targetRpm / RPM_UNIT_DIVISOR);
        return Math.Clamp(targetByte, MIN_FAN_BYTE, MAX_FAN_BYTE);
    }

    public void Dispose()
    {
        Stop();
        _cts?.Dispose();
    }
}