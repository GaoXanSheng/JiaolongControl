using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using JiaoLongControl.Server.Core.Utils;
using JiaoLongControl.Server.Interop;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Microsoft.Win32;

namespace JiaoLongControl.Server
{
    public partial class MainWindow : Window
    {
        private Hardcodet.Wpf.TaskbarNotification.TaskbarIcon _taskbarIcon = null!;
        private string _webRoot = string.Empty;
        private WebView2? _webView;
        private bool _webViewDestroyed = true;
        private bool _allowClose;
        // WebView 重建代次：异步初始化完成后需校验代次，避免旧任务的错误覆盖层/导航落到新 WebView 或已销毁的 UI 树
        private int _webViewGeneration;

        private static bool IsBootStart =>
            Environment.GetCommandLineArgs()
                .Any(arg => arg.Equals("--boot", StringComparison.OrdinalIgnoreCase));

        private readonly bool _startInTray;


        public MainWindow()
        {
            InitializeComponent();
            _startInTray =
                IsBootStart &&
                Bridge.Instance.Config.App.BootMinimized;
            InitializePaths();
            InitializeTray();
            CreateWebView();

            // 启动后的策略恢复放到后台线程，避免驱动加载/WMI 查询阻塞窗口显示
            Loaded += async (_, _) =>
            {
                if (_startInTray)
                {
                    Hide();
                    await SuspendWebViewAsync();
                }

                try
                {
                    await Task.Run(() => new SelfStart());
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"SelfStart 执行异常: {ex.Message}");
                }
            };

            Closing += OnClosing;
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
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
                        Debug.WriteLine($"恢复后 SelfStart 执行异常: {ex.Message}");
                    }
                });
            }
        }

        #region 初始化

        private void InitializePaths()
        {
            var exeDir = Path.GetDirectoryName(
                Process.GetCurrentProcess().MainModule!.FileName!
            )!;

            _webRoot = Path.Combine(exeDir, "WebRoot");
        }

        private void CreateWebView()
        {
            if (!_webViewDestroyed)
                return;

            int generation = ++_webViewGeneration;

            _webView = new WebView2
            {
                DefaultBackgroundColor = System.Drawing.Color.FromArgb(255, 7, 11, 28)
            };
            WebViewHost.Children.Clear();
            WebViewHost.Children.Add(_webView);

            _webViewDestroyed = false;

            // fire-and-forget：内部已做异常处理与重试，绝不让启动阶段崩溃/白屏
            _ = InitializeWebViewAsync(_webView, generation);
        }

        /// <summary>
        /// 初始化 WebView2 并加载前端页面。
        /// 环境创建失败（Runtime 缺失 / userDataFolder 被残留进程锁定等）或页面导航失败时自动重试，
        /// 最终失败则显示错误提示而不是静默白屏。
        /// 每步完成后校验 generation，若期间窗口被销毁或 WebView 被重建则立即放弃，防止操作旧实例。
        /// </summary>
        private async Task InitializeWebViewAsync(WebView2 view, int generation)
        {
            try
            {
                string userDataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "JiaoLongControl",
                    "WebView2"
                );

                Directory.CreateDirectory(userDataFolder);

                var env = await CoreWebView2Environment.CreateAsync(
                    null,
                    userDataFolder
                );

                if (generation != _webViewGeneration || _webViewDestroyed)
                    return;

                await view.EnsureCoreWebView2Async(env);

                if (generation != _webViewGeneration || _webViewDestroyed)
                    return;

                ConfigureWebView(view);

                Bridge.Instance.InitWebView(view.CoreWebView2);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WebView2 初始化失败: {ex.Message}");
                // 环境创建失败时短暂等待后重试（解决重启后首次启动偶发的 runtime 未就绪/目录锁冲突）
                await Task.Delay(1500);

                if (generation != _webViewGeneration || _webViewDestroyed)
                    return;

                try
                {
                    string userDataFolder = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "JiaoLongControl",
                        "WebView2"
                    );
                    var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
                    await view.EnsureCoreWebView2Async(env);

                    if (generation != _webViewGeneration || _webViewDestroyed)
                        return;

                    ConfigureWebView(view);
                    Bridge.Instance.InitWebView(view.CoreWebView2);
                }
                catch (Exception retryEx)
                {
                    Debug.WriteLine($"WebView2 初始化重试失败: {retryEx.Message}");
                    if (generation == _webViewGeneration && !_webViewDestroyed)
                        ShowWebViewError($"界面初始化失败：{retryEx.Message}\n请确认已安装 Microsoft Edge WebView2 Runtime 后重启应用。");
                    return;
                }
            }

            // 页面导航 + 失败重试（最多 3 次）
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                bool ok = await NavigateWithTimeoutAsync(view, generation, TimeSpan.FromSeconds(20));
                if (ok)
                    return;

                Debug.WriteLine($"WebView 页面加载失败，第 {attempt} 次重试");
                await Task.Delay(1000);

                if (generation != _webViewGeneration || _webViewDestroyed)
                    return;
            }

            if (generation == _webViewGeneration && !_webViewDestroyed)
                ShowWebViewError("界面加载失败，请重启应用。");
        }

        /// <summary>导航并等待 NavigationCompleted，返回是否成功</summary>
        private async Task<bool> NavigateWithTimeoutAsync(WebView2 view, int generation, TimeSpan timeout)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            EventHandler<CoreWebView2NavigationCompletedEventArgs> handler = (_, e) =>
            {
                tcs.TrySetResult(e.IsSuccess && e.HttpStatusCode == 200);
            };
            view.CoreWebView2.NavigationCompleted += handler;
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
                // 若期间 WebView 已被销毁/重建，CoreWebView2 可能为 null，需容错
                try
                {
                    if (!_webViewDestroyed && view.CoreWebView2 != null)
                        view.CoreWebView2.NavigationCompleted -= handler;
                }
                catch
                {
                }
            }
        }

        /// <summary>在 WebView 区域显示错误提示（兜底，避免白屏无反馈）</summary>
        private void ShowWebViewError(string message)
        {
            try
            {
                var overlay = new Grid
                {
                    Background = System.Windows.Media.Brushes.Black,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch
                };
                var text = new TextBlock
                {
                    Text = message,
                    Foreground = System.Windows.Media.Brushes.White,
                    FontSize = 14,
                    TextWrapping = TextWrapping.Wrap,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(40)
                };
                overlay.Children.Add(text);
                WebViewHost.Children.Add(overlay);
            }
            catch
            {
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
                // 注销事件
                if (_webView != null && _webView.CoreWebView2 != null)
                {
                    _webView.CoreWebView2.WebMessageReceived -= OnWebMessageReceived;
                }
                _webView?.CoreWebView2?.Stop();
                _webView?.Dispose();
            }
            catch
            {
                // 防止关闭阶段异常
            }

            WebViewHost.Children.Clear();
            _webView = null;
            _webViewDestroyed = true;
        }

        private void ConfigureWebView(WebView2 view)
        {
            view.CoreWebView2.Settings.IsNonClientRegionSupportEnabled = true;
            view.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

            view.CoreWebView2.AddHostObjectToScript("bridge", Bridge.Instance);

            if (Directory.Exists(_webRoot))
            {
                view.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    "app.local",
                    _webRoot,
                    CoreWebView2HostResourceAccessKind.Allow
                );
            }
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
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"处理前端窗口控制消息时发生错误: {ex.Message}");
            }
        }

        private async Task SuspendWebViewAsync()
        {
            try
            {
                if (_webView?.CoreWebView2 != null)
                {
                    await _webView.CoreWebView2.TrySuspendAsync();
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
                if (_webView?.CoreWebView2 != null)
                {
                    _webView.CoreWebView2.Resume();
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
            if (_webViewDestroyed)
            {
                CreateWebView();
            }
            else
            {
                ResumeWebView();
            }

            Show();
            WindowState = WindowState.Normal;
            ShowInTaskbar = true;
            Activate();
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
