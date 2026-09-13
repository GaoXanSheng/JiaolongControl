using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using Windows.Media.Control;
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
        private const string AccentMedia = "#22D3EE";       // 青 (媒体播放)

        // OSD PNG 图标 (经 csproj Resource 链接编译进程序集, 源文件在客户端图标库)
        private const string IconPackBase = "pack://application:,,,/Assets/OSD/";

        private static readonly ILog Logger = LogManager.GetLogger(typeof(OsdController));
        private readonly WMIEventService _eventService = new();

        // 系统媒体会话 (SMTC): 仅挂接"当前会话", 切歌/播放状态变化时提示曲名与歌手
        private readonly object _mediaLock = new();
        private readonly Dictionary<string, PendingOsd> _pending = new();
        private bool _disposed;
        private IntPtr _hook = IntPtr.Zero;
        private byte? _lastBrightness;
        private bool? _lastCaps;
        private ResultState? _lastFnLock;
        private string? _lastMediaKey;
        private bool? _lastNum;

        // EC 状态基线: null = 尚无基线 (首次有效读取只记录不触发); 读取失败 (255) 跳过不更新。
        // 轮询与 HID_EVENT20 事件链路复用同一组基线做去重
        private SystemPerMode? _lastPerf;
        private bool? _lastScroll;
        private ResultState? _lastTouchpad;
        private bool _loggedNoSession;
        private GlobalSystemMediaTransportControlsSessionManager? _mediaManager;
        private DispatcherTimer? _mediaPollTimer;
        private GlobalSystemMediaTransportControlsSession? _mediaSession;
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
                _mediaPollTimer?.Stop();
                _pollCts?.Cancel();
            }
            catch
            {
                // 忽略取消失败
            }

            _eventService.Dispose();

            try
            {
                if (_mediaSession != null)
                {
                    _mediaSession.MediaPropertiesChanged -= OnMediaSessionUpdated;
                    _mediaSession.PlaybackInfoChanged -= OnMediaSessionUpdated;
                }

                if (_mediaManager != null)
                {
                    _mediaManager.CurrentSessionChanged -= OnMediaCurrentSessionChanged;
                    _mediaManager.SessionsChanged -= OnMediaSessionsChanged;
                }
            }
            catch
            {
                // 忽略媒体会话解绑失败
            }

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

            // 系统媒体会话监视 (音乐开始播放/切歌时提示); 初始化失败仅记录, 不影响其它链路
            StartMediaWatcher();
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
                    Kind = "volume",
                    IconGlyph = state.Muted ? "\uE74F" : "\uE767",
                    AccentHex = AccentVolume,
                    Title = "音量",
                    Subtitle = state.Muted ? "已静音" : $"{Math.Round(volume * 100)}%",
                    BarValue = volume,
                    IsMuted = state.Muted,
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
            var on = User32.IsToggleOn(vk);
            ShowOnWindow(new OsdItem
            {
                Kind = "lock",
                IconGlyph = "\uE72E",
                // 大小写切换: 开=AllCaps / 关=AllLowercase; 其余锁定键: 开=Lock / 关=Unlock
                IconImage = vk switch
                {
                    User32.VK_CAPITAL => on ? IconPackBase + "AllCaps.png" : IconPackBase + "AllLowercase.png",
                    _ => on ? IconPackBase + "Lock.png" : IconPackBase + "Unlock.png",
                },
                AccentHex = AccentLock,
                Title = title,
                Subtitle = on ? "已开启" : "已关闭",
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
                Kind = "keyboard",
                IconGlyph = "\uE765",
                AccentHex = AccentKeyboard,
                Title = "键盘背光",
                Subtitle = $"档位 {lv} / 3",
                BarValue = null,
                BrightnessLevel = lv,
            }, cfg);
        }

        /// <summary>性能模式切换时触发 (来自 PerformanceModeController; 同步轮询基线避免重复弹 OSD)。</summary>
        public void OnPerformanceModeChanged(SystemPerMode mode)
        {
            if (mode != SystemPerMode.Unknow) _lastPerf = mode;
            var cfg = Bridge.Instance.Config.Osd;
            if (!cfg.Enabled || !cfg.ShowPerformanceMode) return;
            var (glyph, accent) = ModeVisual(mode);

            if (!cfg.ShowPerfTelemetry)
            {
                ShowOnWindow(new OsdItem
                {
                    Kind = "perf",
                    IconGlyph = glyph,
                    AccentHex = accent,
                    Title = "性能模式",
                    Subtitle = ModeName(mode),
                    BarValue = null,
                }, cfg);
                return;
            }

            // 附带遥测: 先立即弹出模式 OSD (零延迟反馈), 标题携带模式名;
            // 温度/转速经 EC WMI + NvAPI 后台读取, 完成后原位补充到副标题
            // (EC 读取慢约数百 ms; 读取失败保留模式名, 不影响主信息)
            ShowOnWindow(new OsdItem
            {
                Kind = "perf",
                IconGlyph = glyph,
                AccentHex = accent,
                Title = $"性能模式 · {ModeName(mode)}",
                Subtitle = ModeName(mode),
                BarValue = null,
            }, cfg);

            Task.Run(() =>
            {
                var telemetry = ReadPerfTelemetry();
                if (telemetry != null) _window?.UpdatePerfTelemetry(telemetry);
            });
        }

        /// <summary>
        /// 读取性能模式 OSD 的温度/转速遥测 (EC WMI + NvAPI, 后台线程调用)。
        /// 各项独立容错: CPU 温度 255 / 温度转速越界 / 单项异常均跳过; 全部失败返回 null。
        /// 风扇转速取 EC 双风扇值 (RPM), 与风扇曲线页同源; 不用 NvAPI 风扇值 (单位为百分比)。
        /// </summary>
        private static string? ReadPerfTelemetry()
        {
            var parts = new List<string>();
            try
            {
                var res = Bridge.Instance.CPU.GetCPUThermometer();
                if (res.Data is byte cpuTemp and > 0 and < 150)
                    parts.Add($"CPU {cpuTemp}°C");
            }
            catch
            {
                // 忽略单项失败
            }

            try
            {
                var res = Bridge.Instance.NvidiaGpu.GetGpuTemperature();
                if (res.Success && res.Data != null)
                {
                    var gpuTemp = Convert.ToInt32(res.Data);
                    if (gpuTemp is > 0 and < 150)
                        parts.Add($"GPU {gpuTemp}°C");
                }
            }
            catch
            {
                // 忽略单项失败
            }

            try
            {
                var res = Bridge.Instance.Fan.GetFanSpeed();
                if (res.Success && res.Data is FanSpeedInfo fan)
                {
                    var cpu = fan.CPUFanSpeed is > 0 and < 10000 ? fan.CPUFanSpeed : 0;
                    var gpu = fan.GPUFanSpeed is > 0 and < 10000 ? fan.GPUFanSpeed : 0;
                    if (cpu > 0 && gpu > 0) parts.Add($"风扇 {cpu}/{gpu}RPM");
                    else if (cpu > 0 || gpu > 0) parts.Add($"风扇 {Math.Max(cpu, gpu)}RPM");
                }
            }
            catch
            {
                // 忽略单项失败
            }

            return parts.Count == 0 ? null : string.Join(" · ", parts);
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
                Kind = "fnlock",
                IconGlyph = "\uE72E",
                IconImage = state == ResultState.ON ? IconPackBase + "Lock.png" : IconPackBase + "Unlock.png",
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
                Kind = "touchpad",
                IconGlyph = "\uE7E2",
                IconImage = IconPackBase + "TouchPad.png", // 禁用/启动同用触摸板图标
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

        // ===== 系统媒体会话 (SMTC) =====

        /// <summary>
        /// 订阅系统"正在播放"会话 (网易云/Spotify/浏览器等经 SMTC 注册的程序)。
        /// 只挂接系统当前会话, 减少多会话干扰; 请求失败 (如旧系统) 静默禁用。
        /// </summary>
        private async void StartMediaWatcher()
        {
            try
            {
                Logger.Info("媒体会话监视: 正在请求系统会话管理器…");
                // 放到线程池执行: 不占用 UI 线程, 也规避 UI 上下文对 WinRT 异步的潜在干扰
                _mediaManager = await Task.Run(() =>
                    GlobalSystemMediaTransportControlsSessionManager.RequestAsync().AsTask());
                _mediaManager.CurrentSessionChanged += OnMediaCurrentSessionChanged;
                _mediaManager.SessionsChanged += OnMediaSessionsChanged;
                HookMediaSession(_mediaManager.GetCurrentSession());
                Logger.Info($"媒体会话监视: 初始化完成, 当前会话={(_mediaSession != null ? "已就绪" : "无 (等待音乐程序注册)")}");

                // 事件兜底轮询: 即使事件投递失效, 播放状态最多延迟 2s 也会被捕捉 (去重防重复弹)
                _mediaPollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                _mediaPollTimer.Tick += (_, _) =>
                {
                    var session = _mediaManager?.GetCurrentSession();
                    HookMediaSession(session);
                    _ = ShowMediaAsync(session);
                };
                _mediaPollTimer.Start();
            }
            catch (Exception ex)
            {
                Logger.Info($"系统媒体会话监视不可用: {ex.Message}");
            }
        }

        private void OnMediaCurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, object args)
        {
            Logger.Debug("媒体会话事件: CurrentSessionChanged");
            HookMediaSession(sender.GetCurrentSession());
            _ = ShowMediaAsync(sender.GetCurrentSession());
        }

        private void OnMediaSessionsChanged(GlobalSystemMediaTransportControlsSessionManager sender, object args)
        {
            // 会话增减后"当前会话"可能切换, 重新对准并尝试提示 (如音乐软件刚开播)
            Logger.Debug("媒体会话事件: SessionsChanged");
            HookMediaSession(sender.GetCurrentSession());
            _ = ShowMediaAsync(sender.GetCurrentSession());
        }

        /// <summary>把事件挂接从旧会话迁移到新的当前会话 (Session 为运行时新实例, 需按引用换绑)。</summary>
        private void HookMediaSession(GlobalSystemMediaTransportControlsSession? session)
        {
            if (session == null)
            {
                if (!_loggedNoSession)
                {
                    _loggedNoSession = true;
                    Logger.Debug("媒体会话监视: 当前无活动会话");
                }
                return;
            }

            _loggedNoSession = false;
            lock (_mediaLock)
            {
                if (ReferenceEquals(session, _mediaSession)) return;
                if (_mediaSession != null)
                {
                    _mediaSession.MediaPropertiesChanged -= OnMediaSessionUpdated;
                    _mediaSession.PlaybackInfoChanged -= OnMediaSessionUpdated;
                }

                _mediaSession = session;
                _mediaSession.MediaPropertiesChanged += OnMediaSessionUpdated;
                _mediaSession.PlaybackInfoChanged += OnMediaSessionUpdated;
            }

            Logger.Debug($"媒体会话监视: 已挂接会话 {session.SourceAppUserModelId}");
        }

        private void OnMediaSessionUpdated(GlobalSystemMediaTransportControlsSession sender, object args)
        {
            Logger.Debug($"媒体会话事件: {args?.GetType().Name} @ {sender.SourceAppUserModelId}");
            _ = ShowMediaAsync(sender);
        }

        /// <summary>
        /// 读取会话的播放状态与曲名/歌手并弹 OSD。仅播放中弹出; 同一曲目去重 (暂停后恢复不重复弹)。
        /// force 供前端预览: 无播放会话时显示示例内容, 且跳过开关与去重。
        /// </summary>
        private async Task ShowMediaAsync(GlobalSystemMediaTransportControlsSession? session, bool force = false)
        {
            try
            {
                var cfg = Bridge.Instance.Config.Osd;
                if (!force && (!cfg.Enabled || !cfg.ShowMedia))
                {
                    Logger.Debug("媒体提示: OSD 或媒体开关未启用, 跳过");
                    return;
                }

                string title, subtitle;
                var playing = false;
                if (session != null)
                {
                    var playback = session.GetPlaybackInfo();
                    playing = playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
                    Logger.Debug($"媒体提示: 状态 {playback.PlaybackStatus}");
                    if (playing)
                    {
                        // 官方投影的方法名为 TryGetMediaPropertiesAsync (类上无 GetMediaPropertiesAsync)
                        var props = await session.GetMediaPropertiesOrNullAsync();
                        Logger.Debug($"媒体提示: 曲目 {props?.Title} - {props?.Artist}");
                        if (props != null && !string.IsNullOrWhiteSpace(props.Title))
                        {
                            title = props.Title.Trim();
                            subtitle = string.IsNullOrWhiteSpace(props.Artist) ? "正在播放" : props.Artist.Trim();
                        }
                        else if (!force)
                        {
                            SyncMediaPlayState(playing);
                            return;
                        }
                        else
                        {
                            (title, subtitle) = ("示例曲目", "媒体播放提示");
                        }
                    }
                    else if (!force)
                    {
                        // 暂停不打扰: 不重弹 OSD, 但若媒体 OSD 正驻留则原位同步播放/暂停图标
                        SyncMediaPlayState(playing);
                        return;
                    }
                    else
                    {
                        (title, subtitle) = ("示例曲目", "媒体播放提示 (当前未在播放)");
                    }
                }
                else if (!force)
                {
                    return;
                }
                else
                {
                    (title, subtitle) = ("示例曲目", "媒体播放提示 (当前无播放会话)");
                }

                // 曲目+来源去重: 切歌/换程序才弹, 同曲暂停恢复不重复打扰
                var key = $"{title}|{subtitle}|{session?.SourceAppUserModelId}";
                if (!force && key == _lastMediaKey)
                {
                    SyncMediaPlayState(playing);
                    return;
                }
                _lastMediaKey = key;

                ShowOnWindow(new OsdItem
                {
                    Kind = "media",
                    IconGlyph = "\uE8D6",
                    AccentHex = AccentMedia,
                    Title = title,
                    Subtitle = subtitle,
                    BarValue = null,
                    IsPlaying = playing,
                }, cfg);
            }
            catch (Exception ex)
            {
                Logger.Warn($"OSD 媒体提示异常: {ex.Message}");
            }
        }

        /// <summary>媒体 OSD 正驻留时原位同步播放/暂停按钮图标 (不重弹窗口)。</summary>
        private void SyncMediaPlayState(bool playing)
        {
            _window?.UpdatePlayState(playing);
        }

        // ===== JS 接口 =====

        /// <summary>预览 OSD: kind = volume / lock / keyboard / perf / media。</summary>
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
                        case "media":
                            _ = ShowMediaAsync(_mediaManager?.GetCurrentSession(), force: true);
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

        // ===== OSD 交互回调 (交互型 OSD 上的滑条/分段/按钮, UI 线程) =====

        /// <summary>音量滑条拖动/点击: 直接设置系统音量; 界面由窗口原位刷新, 不重播动画。</summary>
        private void OnVolumeSelected(double ratio)
        {
            try
            {
                CoreAudio.SetVolume((float)Math.Clamp(ratio, 0, 1));
            }
            catch (Exception ex)
            {
                Logger.Warn($"OSD 音量调整失败: {ex.Message}");
            }
        }

        /// <summary>音量 OSD 静音按钮: 取反当前静音状态并原位刷新图标。</summary>
        private void OnMuteToggled()
        {
            try
            {
                var muted = CoreAudio.GetVolumeState().Muted;
                CoreAudio.SetMute(!muted);
                ShowVolumeOsd(force: true);
            }
            catch (Exception ex)
            {
                Logger.Warn($"OSD 静音切换失败: {ex.Message}");
            }
        }

        /// <summary>背光分段点击: WMI/EC 写入较慢放后台线程, 成功后走统一回调
        /// 同步轮询基线并刷新 OSD (写入失败则保留乐观显示, 等下次真实状态纠正)。</summary>
        private void OnBrightnessSelected(byte level)
        {
            var target = Math.Clamp(level, (byte)0, (byte)3);
            Task.Run(() =>
            {
                if (MethodServices.SetValue(MethodName.RGBKeyboardBrightness, target))
                {
                    OnKeyboardBrightnessChanged(target);
                }
                else
                {
                    Logger.Warn($"OSD 背光选择: EC 写入失败 (档位 {target})");
                }
            });
        }

        /// <summary>媒体按钮: 模拟媒体键, 经系统 SMTC 分发到当前播放器 (通用支持所有播放软件)。</summary>
        private void OnMediaCommand(string command)
        {
            var vk = command switch
            {
                "prev" => User32.VK_MEDIA_PREV_TRACK,
                "next" => User32.VK_MEDIA_NEXT_TRACK,
                "playpause" => User32.VK_MEDIA_PLAY_PAUSE,
                _ => 0,
            };
            if (vk == 0) return;
            try
            {
                User32.TapMediaKey(vk);
            }
            catch (Exception ex)
            {
                Logger.Warn($"OSD 媒体控制失败: {ex.Message}");
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
                    if (_window == null)
                    {
                        _window = new OsdWindow();
                        _window.VolumeSelected += OnVolumeSelected;
                        _window.MuteToggled += OnMuteToggled;
                        _window.BrightnessSelected += OnBrightnessSelected;
                        _window.MediaCommand += OnMediaCommand;
                    }
                    _window.ShowOsd(item, cfg, UiTheme.IsLight(Bridge.Instance.Config.App.Theme));
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

    /// <summary>
    /// CsWinRT v2 投影适配: 官方投影的方法名为 TryGetMediaPropertiesAsync
    /// (learn.microsoft.com 类成员列表确认, 类上不存在 GetMediaPropertiesAsync)。
    /// </summary>
    internal static class SmtcExtensions
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(SmtcExtensions));

        public static async Task<GlobalSystemMediaTransportControlsSessionMediaProperties?>
            GetMediaPropertiesOrNullAsync(this GlobalSystemMediaTransportControlsSession session)
        {
            try
            {
                return await session.TryGetMediaPropertiesAsync();
            }
            catch (Exception ex)
            {
                Logger.Warn($"SMTC 适配: TryGetMediaPropertiesAsync 失败: {ex.Message}");
                return null;
            }
        }
    }
}
