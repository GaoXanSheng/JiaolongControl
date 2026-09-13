using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using JiaoLongControl.Server.Core.Models;
using JiaoLongControl.Server.Core.Native;
using JiaoLongControl.Server.Core.Services;
using JiaoLongControl.Server.Core.Utils;
using JiaoLongControl.Server.Interop;
using log4net;

namespace JiaoLongControl.Server.Core.Controllers
{
    /// <summary>
    /// OSD 屏显控制器: 三条触发链路。
    /// 1) 全局低级键盘钩子监听音量/锁定键 (不吞键), 防抖后读取真实系统状态;
    /// 2) 官方 HID_EVENT20 WMI 事件通道 —— 原生 Fn 热键 (性能模式/背光/Fn锁/触摸板锁/锁定键) 在 EC 层直接生效,
    ///    事件到达后复用检测逻辑读取真实状态;
    /// 3) 事件通道订阅失败时退回 EC 状态轮询兜底。
    /// 同时向 JS 暴露预览与音量读写接口。配置按项目惯例每次触发时读取, 开关与项目开关即时生效。
    /// </summary>
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.AutoDual)]
    public class OsdController : IDisposable
    {
        // 连续按键 (含长按重复) 防抖: 最后一次按键后再显示, 让系统先完成音量/锁定状态变更
        private const int DebounceDelayMs = 120;

        // 强调色 = 工具箱现有主题色板
        private const string AccentVolume = "#3B82F6";      // 蓝 (信息)
        private const string AccentLock = "#FF7D00";        // 橙 (提示)
        private const string AccentKeyboard = "#8A2BE2";    // 紫 (键盘 RGB 主题色)
        private const string AccentPerf = "#34D399";        // 绿 (性能)
        private const string AccentFnLock = "#6366F1";      // 靛 (功能键)
        private const string AccentTouchpad = "#F472B6";    // 粉 (触摸板)

        private static readonly ILog Logger = LogManager.GetLogger(typeof(OsdController));
        private readonly WMIEventService _eventService = new();
        private readonly Dictionary<string, PendingOsd> _pending = new();
        private bool _disposed;
        private IntPtr _hook = IntPtr.Zero;
        private byte? _lastBrightness;
        private bool? _lastCaps;
        private ResultState? _lastFnLock;
        private bool? _lastNum;

        // EC 状态基线: null = 尚无基线 (首次有效读取只记录不触发); 读取失败 (255) 跳过不更新。
        // 轮询与 HID_EVENT20 事件链路复用同一组基线做去重
        private SystemPerMode? _lastPerf;
        private bool? _lastScroll;
        private ResultState? _lastTouchpad;
        private CancellationTokenSource? _pollCts;
        private Task? _pollTask;

        private User32.LowLevelKeyboardProc? _proc;
        private OsdWindow? _window;

        public OsdController() => Instance = this;

        public static OsdController? Instance { get; private set; }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try
            {
                _pollCts?.Cancel();
            }
            catch
            {
                // 忽略取消失败
            }

            _eventService.Dispose();

            try
            {
                if (_hook != IntPtr.Zero) User32.UnhookWindowsHookEx(_hook);
            }
            catch
            {
                // 忽略卸载失败
            }

            _hook = IntPtr.Zero;
            try
            {
                Application.Current?.Dispatcher.BeginInvoke(() => _window?.Close());
            }
            catch
            {
                // 忽略
            }

            if (Instance == this) Instance = null;
        }

        /// <summary>在 UI 线程调用 (MainWindow 构造时) 安装全局键盘钩子。</summary>
        public void Start()
        {
            if (_hook != IntPtr.Zero || _disposed) return;
            try
            {
                _proc = KeyboardHookProc;
                _hook = User32.SetWindowsHookExW(
                    User32.WH_KEYBOARD_LL, _proc, User32.GetModuleHandleW(null), 0);
                if (_hook == IntPtr.Zero)
                    Logger.Warn($"OSD 键盘钩子安装失败 (Win32Error={Marshal.GetLastWin32Error()})");
            }
            catch (Exception ex)
            {
                Logger.Warn($"OSD 键盘钩子安装异常: {ex.Message}");
            }

            // 优先走官方 HID_EVENT20 事件通道 (即时、零轮询开销); 订阅失败退回轮询兜底
            _eventService.EventArrived += OnHotKeyEvent;
            if (!_eventService.Start())
            {
                Logger.Info("EC 事件通道不可用, OSD 状态检测退回轮询模式");
                StartStatePolling();
            }
        }

        /// <summary>HID_EVENT20 热键事件到达: 事件只说明"什么变了", 具体状态复用检测逻辑读取 (含基线去重与开关判断)。</summary>
        private void OnHotKeyEvent(WmiHotKeyEvent evt)
        {
            try
            {
                switch (evt.EventName)
                {
                    case EventName.SystemPerMode:
                        DetectSystemPerMode();
                        break;
                    case EventName.RGBKeyboardBrightness:
                        DetectKeyboardBrightness();
                        break;
                    case EventName.FnState:
                        DetectFnLock();
                        break;
                    case EventName.TouchPadState:
                        DetectTouchpadLock();
                        break;
                    case EventName.CapsLkState:
                        DetectCapsLock();
                        break;
                    case EventName.NumLockState:
                        DetectNumLock();
                        break;
                    case EventName.ScrlockState:
                        DetectScrollLock();
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"OSD 热键事件处理异常: {ex.Message}");
            }
        }

        /// <summary>启动 EC 状态轮询兜底 (HID_EVENT20 事件通道订阅失败时才启用)。</summary>
        private void StartStatePolling()
        {
            if (_pollTask != null) return;
            _pollCts = new CancellationTokenSource();
            _pollTask = Task.Factory.StartNew(
                () => PollLoop(_pollCts.Token),
                _pollCts.Token,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }

        private void PollLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var cfg = Bridge.Instance.Config.Osd;
                    if (cfg.Enabled)
                    {
                        DetectSystemPerMode();
                        DetectKeyboardBrightness();
                        DetectFnLock();
                        DetectTouchpadLock();
                        DetectCapsLock();
                    }

                    token.WaitHandle.WaitOne(Math.Clamp(cfg.PollIntervalMs, 500, 5000));
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Logger.Warn($"OSD 状态轮询异常: {ex.Message}");
                    token.WaitHandle.WaitOne(2000);
                }
            }
        }

        private IntPtr KeyboardHookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (nCode >= 0 &&
                    (wParam == (IntPtr)User32.WM_KEYDOWN || wParam == (IntPtr)User32.WM_SYSKEYDOWN))
                {
                    var kb = Marshal.PtrToStructure<User32.KBDLLHOOKSTRUCT>(lParam);
                    switch ((int)kb.vkCode)
                    {
                        case User32.VK_VOLUME_UP:
                        case User32.VK_VOLUME_DOWN:
                        case User32.VK_VOLUME_MUTE:
                            DebounceShow("volume", () => ShowVolumeOsd(force: false));
                            break;
                        case User32.VK_CAPITAL:
                            DebounceShow("capslock", () =>
                            {
                                // 同步事件/轮询基线, 避免对同一变化重复弹 OSD
                                _lastCaps = User32.IsToggleOn(User32.VK_CAPITAL);
                                NotifyLockStatesChanged();
                                ShowLockOsd(User32.VK_CAPITAL, "大写锁定", force: false);
                            });
                            break;
                        case User32.VK_NUMLOCK:
                            DebounceShow("numlock", () =>
                            {
                                // 同步事件/轮询基线, 避免对同一变化重复弹 OSD
                                _lastNum = User32.IsToggleOn(User32.VK_NUMLOCK);
                                NotifyLockStatesChanged();
                                ShowLockOsd(User32.VK_NUMLOCK, "数字锁定", force: false);
                            });
                            break;
                        case User32.VK_SCROLL:
                            DebounceShow("scrolllock", () =>
                            {
                                _lastScroll = User32.IsToggleOn(User32.VK_SCROLL);
                                NotifyLockStatesChanged();
                                ShowLockOsd(User32.VK_SCROLL, "滚动锁定", force: false);
                            });
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"OSD 键盘钩子处理异常: {ex.Message}");
            }

            // 不吞按键, 继续传递给系统
            return User32.CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        // ===== 触发源 =====

        private void ShowVolumeOsd(bool force)
        {
            var cfg = Bridge.Instance.Config.Osd;
            if (!force && (!cfg.Enabled || !cfg.ShowVolume)) return;
            try
            {
                var state = CoreAudio.GetVolumeState();
                var volume = Math.Clamp(state.Volume, 0f, 1f);
                ShowOnWindow(new OsdItem
                {
                    IconGlyph = state.Muted ? "\uE74F" : "\uE767",
                    AccentHex = AccentVolume,
                    Title = "音量",
                    Subtitle = state.Muted ? "已静音" : $"{Math.Round(volume * 100)}%",
                    BarValue = volume,
                }, cfg);
            }
            catch (Exception ex)
            {
                Logger.Warn($"OSD 音量状态获取失败: {ex.Message}");
            }
        }

        private void ShowLockOsd(int vk, string title, bool force)
        {
            var cfg = Bridge.Instance.Config.Osd;
            if (!force && (!cfg.Enabled || !cfg.ShowLockKeys)) return;
            ShowOnWindow(new OsdItem
            {
                IconGlyph = "\uE72E",
                AccentHex = AccentLock,
                Title = title,
                Subtitle = User32.IsToggleOn(vk) ? "已开启" : "已关闭",
                BarValue = null,
            }, cfg);
        }

        /// <summary>键盘背光档位变化时触发 (来自 KeyboardController; 同步轮询基线避免重复弹 OSD)。</summary>
        public void OnKeyboardBrightnessChanged(byte level)
        {
            _lastBrightness = Math.Clamp(level, (byte)0, (byte)3);
            var cfg = Bridge.Instance.Config.Osd;
            if (!cfg.Enabled || !cfg.ShowKeyboardBacklight) return;
            ShowKeyboardOsd(level, cfg);
        }

        private void ShowKeyboardOsd(byte level, OsdSection cfg)
        {
            var lv = Math.Clamp(level, (byte)0, (byte)3);
            ShowOnWindow(new OsdItem
            {
                IconGlyph = "\uE765",
                AccentHex = AccentKeyboard,
                Title = "键盘背光",
                Subtitle = $"档位 {lv} / 3",
                BarValue = lv / 3.0,
            }, cfg);
        }

        /// <summary>性能模式切换时触发 (来自 PerformanceModeController; 同步轮询基线避免重复弹 OSD)。</summary>
        public void OnPerformanceModeChanged(SystemPerMode mode)
        {
            if (mode != SystemPerMode.Unknow) _lastPerf = mode;
            var cfg = Bridge.Instance.Config.Osd;
            if (!cfg.Enabled || !cfg.ShowPerformanceMode) return;
            var (glyph, accent) = ModeVisual(mode);
            ShowOnWindow(new OsdItem
            {
                IconGlyph = glyph,
                AccentHex = accent,
                Title = "性能模式",
                Subtitle = ModeName(mode),
                BarValue = null,
            }, cfg);
        }

        /// <summary>按性能模式切换 OSD 图标与强调色: 性能=红/闪电, 均衡=蓝/仪表, 安静=绿/静音。</summary>
        private static (string Glyph, string Accent) ModeVisual(SystemPerMode mode) => mode switch
        {
            SystemPerMode.PerformanceMode => ("\uE945", "#F43F5E"),
            SystemPerMode.BalanceMode => ("\uE9D9", "#3B82F6"),
            SystemPerMode.QuietMode => ("\uE706", "#34D399"),
            _ => ("\uE945", AccentPerf),
        };

        /// <summary>功能键 (Fn) 锁定状态变化时触发 (来自 EC 轮询)。</summary>
        public void OnFnLockChanged(ResultState state)
        {
            var cfg = Bridge.Instance.Config.Osd;
            if (!cfg.Enabled || !cfg.ShowFnLock) return;
            ShowOnWindow(new OsdItem
            {
                IconGlyph = "\uE72E",
                AccentHex = AccentFnLock,
                Title = "功能键锁定",
                Subtitle = state == ResultState.ON ? "已开启" : "已关闭",
                BarValue = null,
            }, cfg);
        }

        /// <summary>触摸板锁定状态变化时触发 (来自 EC 轮询)。</summary>
        public void OnTouchpadLockChanged(ResultState state)
        {
            var cfg = Bridge.Instance.Config.Osd;
            if (!cfg.Enabled || !cfg.ShowTouchpad) return;
            ShowOnWindow(new OsdItem
            {
                IconGlyph = "\uE7E2",
                AccentHex = AccentTouchpad,
                Title = "触摸板",
                Subtitle = state == ResultState.ON ? "已锁定" : "已解锁",
                BarValue = null,
            }, cfg);
        }

        // ===== EC 状态变化检测 (轮询线程调用) =====

        private void DetectSystemPerMode()
        {
            var mode = MethodServices.GetValue<SystemPerMode>(MethodName.SystemPerMode);
            if (mode == SystemPerMode.Unknow) return;                 // 读取失败, 跳过
            if (!_lastPerf.HasValue) { _lastPerf = mode; return; }    // 首次有效读取仅作基线
            if (mode == _lastPerf.Value) return;
            _lastPerf = mode;
            OnPerformanceModeChanged(mode);
        }

        private void DetectKeyboardBrightness()
        {
            var level = MethodServices.GetValue<RGBKeyboardBrightnessLevel>(MethodName.RGBKeyboardBrightness);
            if (level == RGBKeyboardBrightnessLevel.Unknow) return;
            if (!_lastBrightness.HasValue) { _lastBrightness = (byte)level; return; }
            if ((byte)level == _lastBrightness.Value) return;
            _lastBrightness = (byte)level;
            OnKeyboardBrightnessChanged((byte)level);
        }

        private void DetectFnLock()
        {
            var state = MethodServices.GetValue<ResultState>(MethodName.FnLock);
            if (state == ResultState.Unknow) return;
            if (!_lastFnLock.HasValue) { _lastFnLock = state; return; }
            if (state == _lastFnLock.Value) return;
            _lastFnLock = state;
            NotifyLockStatesChanged();
            OnFnLockChanged(state);
        }

        private void DetectTouchpadLock()
        {
            var state = MethodServices.GetValue<ResultState>(MethodName.TPLock);
            if (state == ResultState.Unknow) return;
            if (!_lastTouchpad.HasValue) { _lastTouchpad = state; return; }
            if (state == _lastTouchpad.Value) return;
            _lastTouchpad = state;
            NotifyLockStatesChanged();
            OnTouchpadLockChanged(state);
        }

        private void DetectCapsLock()
        {
            var on = User32.IsToggleOn(User32.VK_CAPITAL);
            if (!_lastCaps.HasValue) { _lastCaps = on; return; }
            if (on == _lastCaps.Value) return;
            _lastCaps = on;
            NotifyLockStatesChanged();
            ShowLockOsd(User32.VK_CAPITAL, "大写锁定", force: false);
        }

        private void DetectNumLock()
        {
            var on = User32.IsToggleOn(User32.VK_NUMLOCK);
            if (!_lastNum.HasValue) { _lastNum = on; return; }
            if (on == _lastNum.Value) return;
            _lastNum = on;
            NotifyLockStatesChanged();
            ShowLockOsd(User32.VK_NUMLOCK, "数字锁定", force: false);
        }

        private void DetectScrollLock()
        {
            var on = User32.IsToggleOn(User32.VK_SCROLL);
            if (!_lastScroll.HasValue) { _lastScroll = on; return; }
            if (on == _lastScroll.Value) return;
            _lastScroll = on;
            NotifyLockStatesChanged();
            ShowLockOsd(User32.VK_SCROLL, "滚动锁定", force: false);
        }

        private static string ModeName(SystemPerMode mode) => mode switch
        {
            SystemPerMode.BalanceMode => "均衡模式",
            SystemPerMode.PerformanceMode => "性能模式",
            SystemPerMode.QuietMode => "安静模式",
            _ => "未知模式",
        };

        // ===== JS 接口 =====

        /// <summary>预览 OSD: kind = volume / lock / keyboard / perf。</summary>
        public CommandResult ShowTest(string kind, int value)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null) return new CommandResult(false, "OSD 不可用");
            dispatcher.BeginInvoke(() =>
            {
                try
                {
                    switch (kind)
                    {
                        case "volume":
                            ShowVolumeOsd(force: true);
                            break;
                        case "lock":
                            ShowLockOsd(User32.VK_CAPITAL, "大写锁定", force: true);
                            break;
                        case "keyboard":
                            ShowKeyboardOsd((byte)Math.Clamp(value, 0, 3), Bridge.Instance.Config.Osd);
                            break;
                        case "perf":
                            // WMI 读取放到后台线程, 避免阻塞 UI
                            Task.Run(() =>
                            {
                                var mode = MethodServices.GetValue<SystemPerMode>(MethodName.SystemPerMode);
                                OnPerformanceModeChanged(mode);
                            });
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn($"OSD 预览异常: {ex.Message}");
                }
            });
            return new CommandResult(true, "已触发 OSD 预览");
        }

        public CommandResult GetVolume()
        {
            try
            {
                var s = CoreAudio.GetVolumeState();
                return new CommandResult(true, "获取成功",
                    new { Volume = (int)Math.Round(Math.Clamp(s.Volume, 0f, 1f) * 100), Muted = s.Muted });
            }
            catch (Exception ex)
            {
                return new CommandResult(false, $"获取音量失败: {ex.Message}");
            }
        }

        public CommandResult SetVolume(int percent)
        {
            try
            {
                CoreAudio.SetVolume(Math.Clamp(percent, 0, 100) / 100f);
                ShowVolumeOsd(force: true);
                return new CommandResult(true, "设置成功");
            }
            catch (Exception ex)
            {
                return new CommandResult(false, $"设置音量失败: {ex.Message}");
            }
        }

        public CommandResult SetMute(bool muted)
        {
            try
            {
                CoreAudio.SetMute(muted);
                ShowVolumeOsd(force: true);
                return new CommandResult(true, "设置成功");
            }
            catch (Exception ex)
            {
                return new CommandResult(false, $"设置静音失败: {ex.Message}");
            }
        }

        // ===== 锁定状态控制 (OSD 页按钮) =====

        /// <summary>读取四项锁定状态; FnLock/触摸板锁读取失败时对应字段为 null (未知)。</summary>
        public CommandResult GetLockStates()
        {
            return new CommandResult(true, "获取成功", new
            {
                CapsLock = User32.IsToggleOn(User32.VK_CAPITAL),
                NumLock = User32.IsToggleOn(User32.VK_NUMLOCK),
                FnLock = ReadResultState(MethodName.FnLock),
                TouchpadLock = ReadResultState(MethodName.TPLock),
            });
        }

        public CommandResult SetCapsLock(bool enabled)
        {
            if (User32.IsToggleOn(User32.VK_CAPITAL) == enabled)
                return new CommandResult(true, "状态未变化");
            User32.TapToggleKey(User32.VK_CAPITAL);
            return new CommandResult(true, "设置成功");
        }

        public CommandResult SetNumLock(bool enabled)
        {
            if (User32.IsToggleOn(User32.VK_NUMLOCK) == enabled)
                return new CommandResult(true, "状态未变化");
            User32.TapToggleKey(User32.VK_NUMLOCK);
            return new CommandResult(true, "设置成功");
        }

        public CommandResult SetFnLock(bool enabled)
        {
            var res = MethodServices.SetValue(MethodName.FnLock, enabled ? (byte)1 : (byte)0);
            if (res)
            {
                OnFnLockChanged(enabled ? ResultState.ON : ResultState.OFF);
                NotifyLockStatesChanged();
            }
            return new CommandResult(res, res ? "设置成功" : "设置失败");
        }

        public CommandResult SetTouchpadLock(bool enabled)
        {
            var res = MethodServices.SetValue(MethodName.TPLock, enabled ? (byte)1 : (byte)0);
            if (res)
            {
                OnTouchpadLockChanged(enabled ? ResultState.ON : ResultState.OFF);
                NotifyLockStatesChanged();
            }
            return new CommandResult(res, res ? "设置成功" : "设置失败");
        }

        private static bool? ReadResultState(MethodName method)
        {
            var state = MethodServices.GetValue<ResultState>(method);
            return state == ResultState.Unknow ? null : state == ResultState.ON;
        }

        /// <summary>
        /// 锁定状态变化后推送 lock-states-changed 消息, 常规设置页的按钮实时刷新
        /// (WMI 读取放在后台线程, 结果回 UI 线程发送)。
        /// </summary>
        private void NotifyLockStatesChanged()
        {
            Task.Run(() =>
            {
                try
                {
                    var payload = JsonSerializer.Serialize(new
                    {
                        type = "lock-states-changed",
                        CapsLock = User32.IsToggleOn(User32.VK_CAPITAL),
                        NumLock = User32.IsToggleOn(User32.VK_NUMLOCK),
                        FnLock = ReadResultState(MethodName.FnLock),
                        TouchpadLock = ReadResultState(MethodName.TPLock),
                    });
                    Application.Current?.Dispatcher.BeginInvoke(() =>
                        Bridge.Instance.PostWebMessage(payload));
                }
                catch (Exception ex)
                {
                    Logger.Warn($"锁定状态推送失败: {ex.Message}");
                }
            });
        }

        private void DebounceShow(string key, Action action)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null) return;
            dispatcher.BeginInvoke(() =>
            {
                try
                {
                    if (_pending.TryGetValue(key, out var pending))
                    {
                        pending.Timer.Stop();
                        pending.Action = action;
                        pending.Timer.Start();
                        return;
                    }

                    var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(DebounceDelayMs) };
                    var entry = new PendingOsd { Timer = timer, Action = action };
                    timer.Tick += (_, _) =>
                    {
                        timer.Stop();
                        _pending.Remove(key);
                        entry.Action();
                    };
                    _pending[key] = entry;
                    timer.Start();
                }
                catch (Exception ex)
                {
                    Logger.Warn($"OSD 调度失败: {ex.Message}");
                }
            });
        }

        private void ShowOnWindow(OsdItem item, OsdSection cfg)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null) return;
            dispatcher.BeginInvoke(() =>
            {
                try
                {
                    _window ??= new OsdWindow();
                    _window.ShowOsd(
                        item,
                        cfg.Position,
                        cfg.Opacity,
                        cfg.DurationMs,
                        cfg.AnimationMs,
                        cfg.Scale,
                        UiTheme.IsLight(Bridge.Instance.Config.App.Theme));
                }
                catch (Exception ex)
                {
                    Logger.Warn($"OSD 显示异常: {ex.Message}");
                }
            });
        }

        // ===== 调度与显示 =====

        private sealed class PendingOsd
        {
            public DispatcherTimer Timer { get; init; } = null!;
            public Action Action { get; set; } = null!;
        }
    }
}
