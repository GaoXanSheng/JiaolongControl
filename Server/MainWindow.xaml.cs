using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using JiaoLongControl.Server.Core.Controllers;
using JiaoLongControl.Server.Interop;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace JiaoLongControl.Server
{
    public partial class MainWindow : Window
    {
        private Hardcodet.Wpf.TaskbarNotification.TaskbarIcon _taskbarIcon;
        private Bridge _bridge = new();
        private string _webRoot;

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
            _startInFan = ConfigController.Config.EnableAdvancedFanControlSystem &&
                          ConfigController.Config.BootStartAdvancedFanControlSystem;
            InitializePaths();
            InitializeTray();
            InitializeWebView();

            if (_startInTray)
            {
                Loaded += (_, _) => Hide();
                WindowState = WindowState.Minimized;
                ShowInTaskbar = false;
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

        private async void InitializeWebView()
        {
            await webView.EnsureCoreWebView2Async();

            ConfigureWebView(webView, _bridge);

            webView.Source = Directory.Exists(_webRoot)
                ? new Uri("https://app.local/index.html")
                : new Uri("http://localhost:5173");

            webView.CoreWebView2.NewWindowRequested += NewWindowRequested;
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

            var menu = new System.Windows.Controls.ContextMenu();

            var show = new System.Windows.Controls.MenuItem { Header = "显示界面" };
            show.Click += (_, _) => ShowMainWindow();

            var exit = new System.Windows.Controls.MenuItem { Header = "退出" };
            exit.Click += (_, _) =>
            {
                _taskbarIcon.Dispose();
                Application.Current.Shutdown();
            };

            menu.Items.Add(show);
            menu.Items.Add(exit);

            _taskbarIcon.ContextMenu = menu;
        }

        #endregion

        #region WebView2

        private async void NewWindowRequested(
            object? sender,
            CoreWebView2NewWindowRequestedEventArgs e)
        {
            e.Handled = true;

            var win = new Window
            {
                Width = 1600,
                Height = 900,
                Title = "JiaoLong Control"
            };

            var child = new WebView2();
            win.Content = child;
            win.Show();

            await child.EnsureCoreWebView2Async();
            ConfigureWebView(child, _bridge);
            child.Source = new Uri(e.Uri);
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
            Show();
            WindowState = WindowState.Normal;
            ShowInTaskbar = true;
            Activate();
        }

        private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }

        #endregion
    }
}