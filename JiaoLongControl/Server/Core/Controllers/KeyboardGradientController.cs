using System.Runtime.InteropServices;
using JiaoLongControl.Server.Core.Models;
using JiaoLongControl.Server.Core.Services;
using JiaoLongControl.Server.Core.Utils;
using log4net;

namespace JiaoLongControl.Server.Core.Controllers;

/// <summary>
/// 键盘渐变灯：启动时读取当前键盘颜色作为锚点色相，以该色相为起点做 0→360° 循环渐变；
/// 停止时恢复启动前的颜色/亮度/颜色模式快照。
/// </summary>
[ComVisible(true)]
[ClassInterface(ClassInterfaceType.AutoDual)]
public class KeyboardGradientController : IDisposable
{
    private readonly ILog Logger = LogManager.GetLogger(typeof(KeyboardGradientController));
    private volatile bool _isRunning;
    private CancellationTokenSource? _cts;
    private Task? _task;

    private const int StepsPerCycle = 60;
    private const int FrameDelayMs = 100;
    private const int MaxConsecutiveFail = 5;
    private const float MinGradientValue = 0.35f; // 锚点颜色过暗时抬到可见亮度

    // 锚点色与启动前快照（Stop 时恢复）
    private byte _anchorR = 138, _anchorG = 43, _anchorB = 226;
    private byte _restoreR, _restoreG, _restoreB;
    private byte? _restoreMode;
    private byte? _restoreBrightness;

    // 上次写入颜色（值未变化时跳过 WMI 往返）
    private byte _lastWriteR = 255, _lastWriteG = 255, _lastWriteB = 255;

    public CommandResult IsRunning()
    {
        // 以线程是否仍在运行为准, 避免 bool 状态与线程实际生命周期脱节
        bool running = _isRunning && _task is { IsCompleted: false };
        return new CommandResult(running, running ? "键盘渐变灯正在运行" : "键盘渐变灯没有在运行中", running);
    }

    public CommandResult Start()
    {
        if (_isRunning)
            return new CommandResult(true, "键盘渐变灯已经运行中");

        // 1. 读取现有颜色作为渐变锚点（读取失败时沿用上次成功的锚点）
        var color = MethodServices.GetValue<Tuple<int, int, int>>(MethodName.RGBKeyboardColor);
        if (color.Item1 >= 0 && color.Item2 >= 0 && color.Item3 >= 0)
        {
            _anchorR = (byte)Math.Min(color.Item1, 255);
            _anchorG = (byte)Math.Min(color.Item2, 255);
            _anchorB = (byte)Math.Min(color.Item3, 255);
        }
        _restoreR = _anchorR;
        _restoreG = _anchorG;
        _restoreB = _anchorB;

        // 2. 快照亮度/模式；若模式不是固定色则切换以保证颜色可见，Stop 时恢复
        _restoreBrightness = ReadByte(MethodName.RGBKeyboardBrightness);
        byte mode = ReadByte(MethodName.RGBKeyboardMode);
        if (mode != (byte)RGBKeyboardMode.Mode_RGBFixedMode)
        {
            _restoreMode = mode == (byte)RGBKeyboardMode.Unknow ? null : mode;
            if (!MethodServices.SetValue(MethodName.RGBKeyboardMode, (byte)RGBKeyboardMode.Mode_RGBFixedMode))
                Logger.Warn("切换键盘颜色模式为固定色失败，渐变可能不可见");
        }
        else
        {
            _restoreMode = null;
        }

        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _task = Task.Factory.StartNew(
            () => EffectLoop(token),
            token,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default
        );
        Thread.Sleep(50);
        Logger.Info($"Keyboard gradient started, anchor=({_anchorR},{_anchorG},{_anchorB})");
        return new CommandResult(true, "键盘渐变灯已启动");
    }

    public CommandResult Stop()
    {
        if (!_isRunning)
            return new CommandResult(true, "键盘渐变灯没有在运行中");
        Logger.Info("Keyboard gradient stopping...");
        _cts?.Cancel();
        try
        {
            _task?.Wait(2000);
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
        RestoreSnapshot();
        return new CommandResult(true, "键盘渐变灯已停止");
    }

    public void Dispose()
    {
        Stop();
        _cts?.Dispose();
        _cts = null;
    }

    private byte ReadByte(MethodName methodName)
    {
        try { return MethodServices.GetValue<byte>(methodName); }
        catch { return (byte)255; }
    }

    private void RestoreSnapshot()
    {
        try
        {
            if (!MethodServices.SetValue(MethodName.RGBKeyboardColor, new byte[] { _restoreR, _restoreG, _restoreB }))
                Logger.Warn("恢复键盘颜色失败");
            if (_restoreBrightness.HasValue)
                MethodServices.SetValue(MethodName.RGBKeyboardBrightness, _restoreBrightness.Value);
            if (_restoreMode.HasValue)
                MethodServices.SetValue(MethodName.RGBKeyboardMode, _restoreMode.Value);
        }
        catch (Exception ex)
        {
            Logger.Error($"恢复键盘灯状态异常: {ex.Message}");
        }
    }

    private void EffectLoop(CancellationToken token)
    {
        _isRunning = true;
        int failCount = 0;
        var anchorHsv = RgbToHsv(_anchorR, _anchorG, _anchorB);
        float baseHue = anchorHsv.Hue;
        float value = Math.Max(anchorHsv.Val, MinGradientValue);
        try
        {
            while (!token.IsCancellationRequested)
            {
                for (int j = 0; j < StepsPerCycle; j++)
                {
                    if (token.IsCancellationRequested) break;
                    try
                    {
                        float hue = (baseHue + 360f * j / StepsPerCycle) % 360f;
                        var (r, g, b) = HsvToRgb(hue, 1f, value);
                        if (r != _lastWriteR || g != _lastWriteG || b != _lastWriteB)
                        {
                            if (MethodServices.SetValue(MethodName.RGBKeyboardColor, new byte[] { r, g, b }))
                            {
                                _lastWriteR = r;
                                _lastWriteG = g;
                                _lastWriteB = b;
                                failCount = 0;
                            }
                            else if (++failCount >= MaxConsecutiveFail)
                            {
                                Logger.Warn("键盘颜色写入连续失败，退出渐变循环");
                                return;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"键盘渐变循环异常: {ex.Message}");
                        Thread.Sleep(500);
                    }
                    try { Task.Delay(FrameDelayMs, token).Wait(token); }
                    catch (OperationCanceledException) { break; }
                }
            }
        }
        finally
        {
            _isRunning = false;
            Logger.Info("Keyboard gradient stopped.");
        }
    }

    private static (float Hue, float Sat, float Val) RgbToHsv(byte r, byte g, byte b)
    {
        float rf = r / 255f, gf = g / 255f, bf = b / 255f;
        float max = Math.Max(rf, Math.Max(gf, bf));
        float min = Math.Min(rf, Math.Min(gf, bf));
        float delta = max - min;
        float hue = 0;
        if (delta > 0)
        {
            if (max == rf) hue = 60f * (((gf - bf) / delta) % 6);
            else if (max == gf) hue = 60f * ((bf - rf) / delta + 2);
            else hue = 60f * ((rf - gf) / delta + 4);
        }
        if (hue < 0) hue += 360;
        float sat = max == 0 ? 0 : delta / max;
        return (hue, sat, max);
    }

    private static (byte R, byte G, byte B) HsvToRgb(float hue, float sat, float val)
    {
        float c = val * sat;
        float x = c * (1 - Math.Abs(hue / 60f % 2 - 1));
        float m = val - c;
        float rf, gf, bf;
        if (hue < 60) { rf = c; gf = x; bf = 0; }
        else if (hue < 120) { rf = x; gf = c; bf = 0; }
        else if (hue < 180) { rf = 0; gf = c; bf = x; }
        else if (hue < 240) { rf = 0; gf = x; bf = c; }
        else if (hue < 300) { rf = x; gf = 0; bf = c; }
        else { rf = c; gf = 0; bf = x; }
        return (
            (byte)Math.Round((rf + m) * 255),
            (byte)Math.Round((gf + m) * 255),
            (byte)Math.Round((bf + m) * 255)
        );
    }
}
