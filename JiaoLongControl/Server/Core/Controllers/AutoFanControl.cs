using System.Runtime.InteropServices;
using JiaoLongControl.Server.Core.Utils;
using JiaoLongControl.Server.Interop;
using log4net;

namespace JiaoLongControl.Server.Core.Controllers
{
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.AutoDual)]
    public class AutoFanControl : IDisposable
    {
        private volatile bool _isRunning;
        private readonly ILog Logger= LogManager.GetLogger(typeof(AutoFanControl));
        private CancellationTokenSource? _cts;
        private Task? _controlTask;
        private const int IntervalMs = 5000;

        private const int RPM_UNIT_DIVISOR = 100;
        private const int MAX_FAN_BYTE = 68;
        private const int MIN_FAN_BYTE = 0;

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
            catch (AggregateException)
            {
            }
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
            Logger.Info("Auto Fan Control started.");

            Queue<float> tempQueue = new();
            const int smoothSampleCount = 3;

            try
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        float rawTemp = Convert.ToSingle(Bridge.Instance.CPU.GetCPUThermometer().Data);

                        tempQueue.Enqueue(rawTemp);
                        if (tempQueue.Count > smoothSampleCount)
                            tempQueue.Dequeue();

                        float smoothTemp = tempQueue.Average();

                        int targetByte = CalculateFanSpeed(smoothTemp);

                        ApplyFanSpeed(targetByte, smoothTemp);

                        Task.Delay(IntervalMs, token).Wait(token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex.Message);
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


        private int CalculateFanSpeed(float currentTemp)
        {
            var configPoints = ConfigController.Config.AdvancedFanControlSystemConfig;

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
                        double ratio =
                            (currentTemp - p1.temp) /
                            (double)(p2.temp - p1.temp);

                        targetRpm =
                            p1.speed +
                            (p2.speed - p1.speed) * ratio;
                        break;
                    }
                }
            }

            int targetByte = (int)Math.Round(targetRpm / RPM_UNIT_DIVISOR);
            return Math.Clamp(targetByte, MIN_FAN_BYTE, MAX_FAN_BYTE);
        }

        private int _lastAppliedByte = -1;

        private void ApplyFanSpeed(int speedByte, float temp)
        {
            if (speedByte == _lastAppliedByte)
                return;

            int rpm = speedByte * 100;

            if (Bridge.Instance.Fan.SetFanSpeed((byte)speedByte).Success)
            {
                Logger.Info($"CPU Temp: {temp}");
                Logger.Info(
                    $"Fan Speed Applied: {rpm} RPM"
                );

                _lastAppliedByte = speedByte;
            }
            else
            {
                Logger.Error($"Fan Speed Apply Failed: EC={speedByte}");
            }
        }

        public void Dispose()
        {
            Stop();
            _cts?.Dispose();
        }
    }
}