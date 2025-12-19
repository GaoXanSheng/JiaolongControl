using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using JiaoLongControl.Server.Interop;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace JiaoLongControl.Server
{
    public partial class MainWindow : Window
    {
        private Hardcodet.Wpf.TaskbarNotification.TaskbarIcon _taskbarIcon;
        private Bridge _bridge;
        private string _webRoot;

        public MainWindow()
        {
            InitializeComponent();
            InitializePaths();
            InitializeWebView();
            InitializeTray();

            Closing += (_, e) =>
            {
                e.Cancel = true;
                Hide();
            };
        }

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

            _bridge = new Bridge();
            ConfigureWebView(webView, _bridge);

            webView.Source = Directory.Exists(_webRoot)
                ? new Uri("https://app.local/index.html")
                : new Uri("http://localhost:5173");

            webView.CoreWebView2.NewWindowRequested += NewWindowRequested;
        }

        private async void NewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
        {
            e.Handled = true;

            var win = new Window
            {
                Width = 1600,
                Height = 900
            };
            
            var child = new WebView2();
            win.Content = child;
            win.Title = "JiaoLong Control";
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

        private void InitializeTray()
        {
            _taskbarIcon = new Hardcodet.Wpf.TaskbarNotification.TaskbarIcon
            {
                Icon = System.Drawing.Icon.ExtractAssociatedIcon(
                    System.Reflection.Assembly.GetEntryAssembly()!.Location
                ),
                ToolTipText = "JiaoLong Control"
            };

            _taskbarIcon.TrayMouseDoubleClick += (_, _) =>
            {
                Show();
                WindowState = WindowState.Normal;
                Activate();
            };

            var menu = new System.Windows.Controls.ContextMenu();

            var show = new System.Windows.Controls.MenuItem { Header = "显示界面" };
            show.Click += (_, _) =>
            {
                Show();
                WindowState = WindowState.Normal;
                Activate();
            };

            var exit = new System.Windows.Controls.MenuItem { Header = "退出" };
            exit.Click += (_, _) => Application.Current.Shutdown();

            menu.Items.Add(show);
            menu.Items.Add(exit);

            _taskbarIcon.ContextMenu = menu;
        }
    }
}
