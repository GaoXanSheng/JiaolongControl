using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using JiaoLongControl.Server.Core.Controllers;
using JiaoLongControl.Server.Interop;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace JiaoLongControl.Server
{
    public partial class MainWindow : Window
    {
        private Hardcodet.Wpf.TaskbarNotification.TaskbarIcon _taskbarIcon;

        private readonly Bridge _bridge = new();
        private string _webRoot = string.Empty;

        private WebView2? _webView;
        private bool _webViewDestroyed = true;

        private static bool IsBootStart =>
            Environment.GetCommandLineArgs()
                .Any(arg => arg.Equals("--boot", StringComparison.OrdinalIgnoreCase));

        private readonly bool _startInTray;
        private readonly bool _startInFan;

        public MainWindow()
        {
            ConfigController.Load();
            InitializeComponent();

            _startInTray =
                IsBootStart &&
                ConfigController.Config.MinimizedAfterBooting;

            _startInFan =
                ConfigController.Config.EnableAdvancedFanControlSystem &&
                ConfigController.Config.BootStartAdvancedFanControlSystem;

            InitializePaths();
            InitializeTray();

            CreateWebView();

            if (_startInTray)
            {
                DestroyWebView();
                Loaded += (_, _) => Hide();
            }

            if (_startInFan)
            {
                _bridge.AutoFan.Start();
            }

            Closing += OnClosing;
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

            ConfigureWebView(view, _bridge);

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

        private void ConfigureWebView(WebView2 view, Bridge bridge)
        {
            view.CoreWebView2.AddHostObjectToScript("bridge", bridge);

            if (Directory.Exists(_webRoot))
            {
                view.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    "app.local",
                    _webRoot,
                    CoreWebView2HostResourceAccessKind.Allow
                );
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
