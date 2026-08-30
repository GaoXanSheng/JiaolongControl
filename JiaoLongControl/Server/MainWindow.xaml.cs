using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using JiaoLongControl.Server.Core.Utils;
using JiaoLongControl.Server.Interop;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Microsoft.Win32;

namespace JiaoLongControl.Server
{
    public partial class MainWindow : Window
    {
        private static readonly log4net.ILog Logger =
            log4net.LogManager.GetLogger(typeof(MainWindow));

        private Hardcodet.Wpf.TaskbarNotification.TaskbarIcon _taskbarIcon = null!;
        private string _webRoot = string.Empty;
        private WebView2? _webView;
        // 是否已销毁：仅表示 WebView 对象的有无，不能代表 CoreWebView2 已初始化完成。
        // CoreWebView2 是异步初始化的，因此判断"能否使用"必须看 SafeCore(_webView) 是否为 null。
        private bool _webViewDestroyed = true;
        private bool _allowClose;
        // 退出中：抑制退出阶段 ProcessFailed 等无意义日志/重建（浏览器进程被销毁时正常退出）
        private bool _isShuttingDown;
        // WebView 重建代次：异步初始化完成后需校验代次，避免旧任务的错误覆盖层/导航落到新 WebView 或已销毁的 UI 树
        private int _webViewGeneration;
        // 进程崩溃连续计数：渲染/GPU 进程崩溃先轻量 Reload，连续崩溃则整体重建
        private int _processFailCount;
        // 连续重建计数：自动重建超过上限则停止，避免进程反复崩溃时无限重建
        private int _recreateCount;
        private Grid? _loadingOverlay;
        private Grid? _errorOverlay;
        // 当前是否浅色主题: 由配置(App.Theme)解析, 前端切换主题时经 theme-changed 消息同步
        private bool _isLight;
        // ProcessFailed 处理器引用：ConfigureWebView 订阅、DestroyWebView 注销，保证重建后旧回调不再触发
        private EventHandler<CoreWebView2ProcessFailedEventArgs>? _processFailedHandler;

        public MainWindow()
        {
            InitializeComponent();
            // 配置已在 App.OnStartup 初始化完成, 此处解析主题并先于 WebView 创建着色, 避免启动闪色
            _isLight = UiTheme.IsLight(Bridge.Instance.Config.App.Theme);
            ApplyThemeColors();
            InitializePaths();
            InitializeTray();
            CreateWebView();

            // 启动后立刻在后台恢复开机策略，不依赖窗口显示。
            // 注意：--boot 隐藏启动时 App.OnStartup 不会 Show 本窗口，Loaded 事件永远不触发，
            // 若把 SelfStart 放在 Loaded 里策略将永不应用。后台线程避免驱动加载/WMI 查询阻塞窗口显示。
            _ = RunSelfStartAsync();

            Closing += OnClosing;
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
        }

        /// <summary>在后台应用开机自启策略，异常不外泄。</summary>
        private static async Task RunSelfStartAsync()
        {
            try
            {
                await Task.Run(() => new SelfStart());
            }
            catch (Exception ex)
            {
                Logger.Error("SelfStart 执行异常", ex);
            }
        }

        private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            // 当系统从休眠/睡眠中恢复时，在后台重新应用开机策略
            if (e.Mode == PowerModes.Resume)
            {
                _ = Task.Run(() =>
                {
                    try
                    {
                        new SelfStart();
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("恢复后 SelfStart 执行异常", ex);
                    }
                });
            }
        }

        #region 初始化

        /// <summary>
        /// 安全获取 CoreWebView2。
        /// 浏览器进程崩溃后，WebView2.CoreWebView2 属性 getter 会抛 InvalidOperationException
        /// （VerifyBrowserNotCrashed）而非返回 null，所有访问都必须经此封装。
        /// 初始化为 null 也代表"当前不可用"，调用方应据此重建而不是尝试 Resume。
        /// </summary>
        private static CoreWebView2? SafeCore(WebView2? view)
        {
            if (view == null)
                return null;
            try
            {
                return view.CoreWebView2;
            }
            catch
            {
                return null;
            }
        }

        private void InitializePaths()
        {
            var exeDir = Path.GetDirectoryName(
                Process.GetCurrentProcess().MainModule!.FileName!
            )!;

            _webRoot = Path.Combine(exeDir, "WebRoot");
        }

        private void CreateWebView()
        {
            // 存在未销毁的 WebView 时拒绝新建，避免重复创建。注意：_webViewDestroyed 为 false
            // 不代表 CoreWebView2 已可用，重建路径需先 DestroyWebView 再调用本方法。
            if (!_webViewDestroyed)
                return;

            int generation = ++_webViewGeneration;
            _processFailCount = 0;

            _webView = new WebView2
            {
                DefaultBackgroundColor = DrawingColorFrom(UiTheme.Background(_isLight))
            };
            WebViewHost.Children.Clear();
            WebViewHost.Children.Add(_webView);
            ShowLoadingOverlay();

            _webViewDestroyed = false;

            // fire-and-forget：内部已做异常处理与重试，绝不让启动阶段崩溃/白屏
            _ = InitializeWebViewAsync(_webView, generation);
        }

        /// <summary>
        /// 初始化 WebView2 并加载前端页面。
        /// 环境创建失败（Runtime 缺失 / userDataFolder 被残留进程锁定等）时按递增间隔重试，
        /// 页面导航失败自动重试；最终失败显示带「重新加载」按钮的错误提示而不是静默白屏。
        /// 每步完成后校验 generation，若期间窗口被销毁或 WebView 被重建则立即放弃，防止操作旧实例。
        /// </summary>
        private async Task InitializeWebViewAsync(WebView2 view, int generation)
        {
            // 阶段一：创建 WebView2 环境（重启后首次冷启动较慢，或 userDataFolder 被上次残留进程锁定，
            // 递增重试给 WebView2 释放锁/完成初始化留出时间；加超时防止 CreateAsync 静默挂起导致死屏）
            bool envReady = false;
            for (int attempt = 1; attempt <= 3 && !envReady; attempt++)
            {
                try
                {
                    string userDataFolder = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "JiaoLongControl",
                        "WebView2"
                    );
                    Directory.CreateDirectory(userDataFolder);

                    // CoreWebView2Environment.CreateAsync 在 userDataFolder 被残留进程锁定时可能长时间挂起而非抛异常，
                    // 必须用超时保护，否则初始化永远卡在第一步，界面停留在加载提示
                    var envTask = CoreWebView2Environment.CreateAsync(null, userDataFolder);
                    var envDone = await Task.WhenAny(envTask, Task.Delay(TimeSpan.FromSeconds(15)));
                    if (envDone != envTask)
                        throw new TimeoutException("WebView2 环境创建超时（userDataFolder 可能被残留进程锁定）");
                    var env = await envTask;

                    if (generation != _webViewGeneration || _webViewDestroyed)
                        return;

                    var initTask = view.EnsureCoreWebView2Async(env);
                    var initDone = await Task.WhenAny(initTask, Task.Delay(TimeSpan.FromSeconds(15)));
                    if (initDone != initTask)
                        throw new TimeoutException("WebView2 初始化超时");
                    await initTask;

                    if (generation != _webViewGeneration || _webViewDestroyed)
                        return;

                    ConfigureWebView(view, generation);
                    Bridge.Instance.InitWebView(SafeCore(view)!);
                    envReady = true;
                }
                catch (Exception ex)
                {
                    Logger.Error($"WebView2 初始化失败（第 {attempt} 次）: {ex.Message}");
                    if (attempt < 3)
                    {
                        await Task.Delay(1500 * attempt); // 1.5s / 3s / 5s
                        if (generation != _webViewGeneration || _webViewDestroyed)
                            return;
                    }
                    else
                    {
                        if (generation == _webViewGeneration && !_webViewDestroyed)
                            ShowWebViewError($"界面初始化失败：{ex.Message}\n请确认已安装 Microsoft Edge WebView2 Runtime 后点击「重新加载」重试。");
                        return;
                    }
                }
            }

            if (!envReady)
                return;

            // 阶段二：页面导航 + 失败重试（最多 3 次）
            await RetryNavigationAsync(view, generation);
        }

        /// <summary>导航并等待 NavigationCompleted，返回是否成功</summary>
        private async Task<bool> NavigateWithTimeoutAsync(WebView2 view, int generation, TimeSpan timeout)
        {
            var core = SafeCore(view);
            if (core == null)
                return false;

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            EventHandler<CoreWebView2NavigationCompletedEventArgs> handler = (sender, e) =>
            {
                // 只响应本实例自己的导航事件，避免旧导航的完成事件误触超时结果
                if (ReferenceEquals(sender, core))
                    tcs.TrySetResult(e.IsSuccess && e.HttpStatusCode == 200);
            };
            core.NavigationCompleted += handler;
            try
            {
                view.Source = Directory.Exists(_webRoot)
                    ? new Uri("https://app.local/index.html")
                    : new Uri("http://localhost:5173");

                var finished = await Task.WhenAny(tcs.Task, Task.Delay(timeout)) == tcs.Task;
                return finished && tcs.Task.Result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"导航异常: {ex.Message}");
                return false;
            }
            finally
            {
                // 若期间 WebView 已被销毁/浏览器进程崩溃，用 SafeCore 容错
                try
                {
                    var c = SafeCore(view);
                    if (c != null)
                        c.NavigationCompleted -= handler;
                }
                catch
                {
                }
            }
        }

        /// <summary>页面导航失败重试（最多 3 次），成功后移除加载层；供初始化与 Resume 恢复共用</summary>
        private async Task RetryNavigationAsync(WebView2 view, int generation)
        {
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                bool ok = await NavigateWithTimeoutAsync(view, generation, TimeSpan.FromSeconds(20));
                if (ok)
                {
                    // 校验代次，避免旧任务的加载层移除误伤新实例的加载层
                    if (generation == _webViewGeneration && !_webViewDestroyed)
                        RemoveLoadingOverlay();
                    return;
                }

                Logger.Warn($"WebView 页面加载失败，第 {attempt} 次重试");
                await Task.Delay(1000);

                if (generation != _webViewGeneration || _webViewDestroyed)
                    return;
            }

            if (generation == _webViewGeneration && !_webViewDestroyed)
                ShowWebViewError("界面加载失败，请点击「重新加载」重试。");
        }

        private void ConfigureWebView(WebView2 view, int generation)
        {
            // 初始化正常阶段可安全访问（刚 EnsureCoreWebView2Async 成功）
            var core = SafeCore(view);
            if (core == null)
                throw new InvalidOperationException("CoreWebView2 不可用");

            core.Settings.IsNonClientRegionSupportEnabled = true;
            core.WebMessageReceived += OnWebMessageReceived;

            // 子进程崩溃恢复。注意：崩溃后 CoreWebView2 属性 getter 会抛异常，
            // 因此回调内只用闭包捕获的 core/generation 判断，绝不访问 _webView.CoreWebView2
            _processFailedHandler = (sender, e) =>
            {
                try
                {
                    Dispatcher.BeginInvoke(() => HandleProcessFailed(sender, e, core, generation));
                }
                catch (Exception ex)
                {
                    // 应用退出中 Dispatcher 可能已关闭
                    Logger.Warn($"ProcessFailed 回调分发失败: {ex.Message}");
                }
            };
            core.ProcessFailed += _processFailedHandler;

            // 任何成功的导航都清掉错误覆盖层，并重置崩溃/重建计数
            core.NavigationCompleted += (sender, e) =>
            {
                // 只响应当前实例，防止旧 WebView 的回调误伤新实例
                if (generation != _webViewGeneration || _webViewDestroyed)
                    return;
                if (!ReferenceEquals(sender, core))
                    return;
                if (e.IsSuccess && e.HttpStatusCode == 200)
                {
                    _processFailCount = 0;
                    _recreateCount = 0;
                    RemoveErrorOverlay();
                }
            };

            core.AddHostObjectToScript("bridge", Bridge.Instance);

            if (Directory.Exists(_webRoot))
            {
                core.SetVirtualHostNameToFolderMapping(
                    "app.local",
                    _webRoot,
                    CoreWebView2HostResourceAccessKind.Allow
                );
            }
        }

        /// <summary>
        /// WebView2 子进程崩溃处理（经 Dispatcher 转到 UI 线程执行，入参均为闭包捕获的稳定引用）。
        /// 浏览器进程退出必须整体重建；渲染/GPU 进程先轻量 Reload，连续崩溃再重建。
        /// </summary>
        private void HandleProcessFailed(object? sender, CoreWebView2ProcessFailedEventArgs e, CoreWebView2 core, int generation)
        {
            try
            {
                // 退出阶段浏览器进程被销毁属正常，直接忽略
                if (_isShuttingDown)
                    return;
                // 只处理当前实例的崩溃，忽略旧 WebView 排队的事件
                if (generation != _webViewGeneration || _webViewDestroyed)
                    return;
                if (!ReferenceEquals(sender, core))
                    return;

                Logger.Warn($"WebView2 子进程异常退出: kind={e.ProcessFailedKind}, exitCode={e.ExitCode}");

                if (e.ProcessFailedKind == CoreWebView2ProcessFailedKind.BrowserProcessExited)
                {
                    _ = DelayedRecreateWebViewAsync(generation, "浏览器进程退出，重建界面");
                    return;
                }

                _processFailCount++;
                if (_processFailCount >= 2)
                {
                    _ = DelayedRecreateWebViewAsync(generation, $"子进程连续崩溃（{_processFailCount} 次），重建界面");
                    return;
                }

                // 渲染/GPU 进程崩溃：轻量恢复——重新加载当前页面（用闭包 core，避免触碰已崩溃的属性 getter）
                try
                {
                    Logger.Warn("尝试 Reload 恢复渲染");
                    core.Reload();
                }
                catch (Exception ex)
                {
                    Logger.Error("Reload 恢复失败", ex);
                    _ = DelayedRecreateWebViewAsync(generation, "Reload 恢复失败，重建界面");
                }
            }
            catch (Exception ex)
            {
                // 兜底：任何异常都不应从这里逃逸到 Dispatcher 导致闪退
                Logger.Error("ProcessFailed 处理异常", ex);
            }
        }

        /// <summary>
        /// 延迟重建：浏览器进程崩溃瞬间重建新 WebView 可能引发原生资源竞争导致二次崩溃，
        /// 等待 1s 让崩溃余波平息后再重建。
        /// </summary>
        private async Task DelayedRecreateWebViewAsync(int generation, string reason)
        {
            try
            {
                await Task.Delay(1000);
                if (generation != _webViewGeneration || _webViewDestroyed)
                    return;
                RecreateWebView(reason);
            }
            catch (Exception ex)
            {
                Logger.Error("延迟重建失败", ex);
            }
        }

        /// <summary>销毁并重建 WebView（含错误/加载覆盖层清理）；连续重建超上限则停止，避免无限循环</summary>
        private void RecreateWebView(string reason)
        {
            try
            {
                _recreateCount++;
                if (_recreateCount > 3)
                {
                    Logger.Error($"WebView 连续重建超过上限，停止自动重建: {reason}");
                    RemoveErrorOverlay();
                    ShowWebViewError("界面连续加载失败，请重启应用。");
                    return;
                }

                Logger.Warn($"重建 WebView: {reason}");
                DestroyWebView();
                if (IsLoaded && Visibility == Visibility.Visible)
                    CreateWebView();
            }
            catch (Exception ex)
            {
                Logger.Error("重建 WebView 失败", ex);
            }
        }

        private void ShowLoadingOverlay()
        {
            try
            {
                var overlay = new Grid
                {
                    Background = new SolidColorBrush(UiTheme.Background(_isLight)),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch
                };
                var text = new TextBlock
                {
                    Text = "正在加载界面…",
                    Foreground = new SolidColorBrush(UiTheme.OverlayText(_isLight)),
                    FontSize = 14,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                overlay.Children.Add(text);
                WebViewHost.Children.Add(overlay); // 后添加，位于 WebView 上层
                _loadingOverlay = overlay;
            }
            catch
            {
            }
        }

        private void RemoveLoadingOverlay()
        {
            try
            {
                if (_loadingOverlay != null && WebViewHost.Children.Contains(_loadingOverlay))
                    WebViewHost.Children.Remove(_loadingOverlay);
                _loadingOverlay = null;
            }
            catch
            {
            }
        }

        private void RemoveErrorOverlay()
        {
            try
            {
                if (_errorOverlay != null && WebViewHost.Children.Contains(_errorOverlay))
                    WebViewHost.Children.Remove(_errorOverlay);
                _errorOverlay = null;
            }
            catch
            {
            }
        }

        /// <summary>在 WebView 区域显示错误提示 + 重新加载按钮（兜底，避免白屏无反馈）</summary>
        private void ShowWebViewError(string message)
        {
            try
            {
                RemoveLoadingOverlay();

                var overlay = new Grid
                {
                    Background = new SolidColorBrush(UiTheme.Background(_isLight)),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch
                };
                var stack = new StackPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                var text = new TextBlock
                {
                    Text = message,
                    Foreground = new SolidColorBrush(UiTheme.OverlayText(_isLight)),
                    FontSize = 14,
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(40, 0, 40, 20)
                };
                stack.Children.Add(text);

                var retryBtn = new Button
                {
                    Content = "重新加载",
                    Width = 140,
                    Height = 38,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    FontSize = 13
                };
                retryBtn.Click += (_, _) =>
                {
                    try
                    {
                        WebViewHost.Children.Remove(overlay);
                        _errorOverlay = null;
                        // 用户手动重载不占用自动重建计数，重置后走标准重建流程
                        _recreateCount = 0;
                        DestroyWebView();
                        if (IsLoaded && Visibility == Visibility.Visible)
                            CreateWebView();
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("手动重新加载失败", ex);
                    }
                };
                stack.Children.Add(retryBtn);

                overlay.Children.Add(stack);
                WebViewHost.Children.Add(overlay);
                _errorOverlay = overlay;
            }
            catch (Exception ex)
            {
                Logger.Error("显示错误提示失败", ex);
            }
        }

        private void InitializeTray()
        {
            _taskbarIcon = new Hardcodet.Wpf.TaskbarNotification.TaskbarIcon
            {
                Icon = System.Drawing.Icon.ExtractAssociatedIcon(
                    System.Reflection.Assembly.GetEntryAssembly()!.Location
                ),
                ToolTipText = "JiaoLong Control"
            };

            _taskbarIcon.TrayMouseDoubleClick += (_, _) => ShowMainWindow();

            var menu = new ContextMenu();

            var show = new MenuItem { Header = "显示界面" };
            show.Click += (_, _) => ShowMainWindow();

            var exit = new MenuItem { Header = "退出" };
            exit.Click += (_, _) =>
            {
                // 标记允许真正关闭，否则 OnClosing 会拦截（隐藏到托盘）
                _allowClose = true;
                _isShuttingDown = true;
                DestroyWebView();
                _taskbarIcon.Dispose();
                Application.Current.Shutdown();
            };

            menu.Items.Add(show);
            menu.Items.Add(exit);

            _taskbarIcon.ContextMenu = menu;
        }

        #endregion

        #region WebView 管理

        private void DestroyWebView()
        {
            if (_webViewDestroyed)
                return;

            try
            {
                var core = SafeCore(_webView);
                if (core != null)
                {
                    core.WebMessageReceived -= OnWebMessageReceived;
                    if (_processFailedHandler != null)
                        core.ProcessFailed -= _processFailedHandler;
                    _processFailedHandler = null;
                    core.Stop();
                }
                _webView?.Dispose();
            }
            catch
            {
                // 防止关闭阶段异常
            }

            WebViewHost.Children.Clear();
            _webView = null;
            _loadingOverlay = null;
            _errorOverlay = null;
            _webViewDestroyed = true;
        }

        /// <summary>判断 WebView 是否处于「已就绪」状态，即对象存在且 CoreWebView2 已初始化可用。</summary>
        private bool IsWebViewReady()
        {
            return !_webViewDestroyed && SafeCore(_webView) != null;
        }

        /// <summary>
        /// 确保 WebView 就绪：已可用则直接返回 true；否则销毁未初始化成功的旧实例并触发重建，返回 false
        /// （重建是异步的，调用方在本轮不应再假设 CoreWebView2 可用）。
        /// </summary>
        private bool EnsureWebViewReady()
        {
            if (IsWebViewReady())
                return true;
            if (!_webViewDestroyed)
                DestroyWebView();
            CreateWebView();
            return false;
        }

        // 【新增】响应前端窗口控制请求的方法
        private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                string message = e.TryGetWebMessageAsString();

                if (message == "window-minimize")
                {
                    WindowState = WindowState.Minimized;
                }
                else if (message == "window-maximize")
                {
                    WindowState = WindowState == WindowState.Maximized
                        ? WindowState.Normal
                        : WindowState.Maximized;
                }
                else if (message == "window-drag")
                {
                    DragMove();
                }
                else if (message == "window-close")
                {
                    Close();
                }
                else if (message.StartsWith("theme-changed:", StringComparison.Ordinal))
                {
                    // 前端切换主题后同步窗口/WebView 底色
                    bool isLight = message.EndsWith(":light", StringComparison.Ordinal);
                    if (_isLight != isLight)
                    {
                        _isLight = isLight;
                        ApplyThemeColors();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"处理前端窗口控制消息时发生错误: {ex.Message}");
            }
        }

        /// <summary>按当前主题刷新窗口/WebView/遮罩配色(启动与 theme-changed 时调用)</summary>
        private void ApplyThemeColors()
        {
            Background = new SolidColorBrush(UiTheme.Background(_isLight));
            if (_webView != null)
            {
                _webView.DefaultBackgroundColor = DrawingColorFrom(UiTheme.Background(_isLight));
            }
            if (_loadingOverlay != null)
            {
                _loadingOverlay.Background = new SolidColorBrush(UiTheme.Background(_isLight));
            }
            if (_errorOverlay != null)
            {
                _errorOverlay.Background = new SolidColorBrush(UiTheme.Background(_isLight));
                foreach (var textBlock in FindVisualChildren<TextBlock>(_errorOverlay))
                {
                    textBlock.Foreground = new SolidColorBrush(UiTheme.OverlayText(_isLight));
                }
            }
        }

        private static System.Drawing.Color DrawingColorFrom(System.Windows.Media.Color color)
        {
            return System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B);
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject? root)
            where T : DependencyObject
        {
            if (root == null)
            {
                yield break;
            }
            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is T typed)
                {
                    yield return typed;
                }
                foreach (var descendant in FindVisualChildren<T>(child))
                {
                    yield return descendant;
                }
            }
        }

        private async Task SuspendWebViewAsync()
        {
            try
            {
                var core = SafeCore(_webView);
                if (core != null)
                {
                    await core.TrySuspendAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Suspend WebView2 异常: {ex.Message}");
            }
        }

        private void ResumeWebView()
        {
            try
            {
                var core = SafeCore(_webView);
                if (core != null)
                {
                    core.Resume();
                    // 自启最小化场景：Suspend 时导航被挂起、必然超时并留下错误层；
                    // 恢复后主动重新导航，错误层由导航成功自动移除
                    if (_errorOverlay != null)
                    {
                        RemoveErrorOverlay();
                        _ = RetryNavigationAsync(_webView!, _webViewGeneration);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Resume WebView2 异常: {ex.Message}");
            }
        }

        #endregion

        #region 窗口控制

        private void ShowMainWindow()
        {
            // 必须先显示窗口，再处理 WebView 的恢复/重建。
            // --boot 隐藏启动时 App.OnStartup 不会 Show 本窗口；若在显示前调用 EnsureWebViewReady 触发重建，
            // 隐藏状态下 WebView2 初始化会失败并返回 false，随后 Show 出来就是白屏。
            Show();
            WindowState = WindowState.Normal;
            ShowInTaskbar = true;
            Activate();

            // 窗口已可见：CoreWebView2 可用则恢复（Suspend 后仍可用），不可用（隐藏期初始化未完成/失败）则销毁重建。
            if (IsWebViewReady())
                ResumeWebView();
            else
                EnsureWebViewReady();
        }

        private async void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // 仅托盘菜单「退出」时允许真正关闭；其余情况（标题栏关闭按钮/前端 window-close）隐藏到托盘
            if (_allowClose)
                return;

            e.Cancel = true;
            Hide();
            await SuspendWebViewAsync();
        }

        #endregion
    }
}
