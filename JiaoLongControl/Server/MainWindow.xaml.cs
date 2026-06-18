using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using JiaoLongControl.Server.Core.Controllers;
using JiaoLongControl.Server.Core.Utils;
using JiaoLongControl.Server.Interop;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Microsoft.Win32;

namespace JiaoLongControl.Server
{
    public partial class MainWindow : Window
    {
        private Hardcodet.Wpf.TaskbarNotification.TaskbarIcon _taskbarIcon;
        private string _webRoot = string.Empty;
        private WebView2? _webView;
        private bool _webViewDestroyed = true;

        private static bool IsBootStart =>
            Environment.GetCommandLineArgs()
                .Any(arg => arg.Equals("--boot", StringComparison.OrdinalIgnoreCase));

        private readonly bool _startInTray;


        public MainWindow()
        {
            ConfigController.Load();
            InitializeComponent();
            _startInTray =
                IsBootStart &&
                ConfigController.Config.BootMinimized;
            InitializePaths();
            InitializeTray();
            CreateWebView();

            if (_startInTray)
            {
                DestroyWebView();
                Loaded += (_, _) => Hide();
            }

            Closing += OnClosing;
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
            new SelfStart();
        }
        private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            // 当系统从休眠/睡眠中恢复时
            if (e.Mode == PowerModes.Resume)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    new SelfStart();
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

            _webView = new WebView2();
            WebViewHost.Children.Clear();
            WebViewHost.Children.Add(_webView);

            InitializeWebView(_webView);

            _webViewDestroyed = false;
        }

        private async void InitializeWebView(WebView2 view)
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

            await view.EnsureCoreWebView2Async(env);

            ConfigureWebView(view);

            view.Source = Directory.Exists(_webRoot)
                ? new Uri("https://app.local/index.html")
                : new Uri("http://localhost:5173");
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
            // 【新增】允许网页使用 CSS 的 app-region 属性实现无边框下的拖动
            view.CoreWebView2.Settings.IsNonClientRegionSupportEnabled = true;

            // 【新增】注册消息事件，处理前端传来的窗口操作请求
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
                    // 此处调用 Close 将触发 MainWindow 的 OnClosing 周期，从而正常调用 DestroyWebView() 和 Hide()
                    Close();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"处理前端窗口控制消息时发生错误: {ex.Message}");
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

            Show();
            WindowState = WindowState.Normal;
            ShowInTaskbar = true;
            Activate();
        }

        private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            DestroyWebView();
            Hide();
        }

        #endregion
    }
}