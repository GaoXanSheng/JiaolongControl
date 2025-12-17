using System.IO;
using System.Windows;
using JiaoLongControl.Server.Core.Services;
using JiaoLongControl.Server.Interop;

namespace JiaoLongControl.Server
{
    public partial class MainWindow : Window
    {
        private Hardcodet.Wpf.TaskbarNotification.TaskbarIcon _taskbarIcon;
        private Bridge _bridge;

        public MainWindow()
        {
            InitializeComponent();
            InitializeHardware();
            InitializeWebView();
            InitializeTray();
            
            // 窗口关闭事件拦截
            this.Closing += (s, e) => {
                // 默认最小化到托盘
                e.Cancel = true;
                this.Hide();
            };
        }

        private void InitializeHardware()
        {
            try
            {
                FanControlService.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"驱动初始化失败: {ex.Message}\n请确保以管理员运行。", "错误");
            }
        }

        private async void InitializeWebView()
        {
            // 初始化 WebView2
            await webView.EnsureCoreWebView2Async();

            // 注册 C# 桥接对象，名称为 "bridge"
            _bridge = new Bridge();
            webView.CoreWebView2.AddHostObjectToScript("bridge", _bridge);
            
            // 加载页面
            string htmlPath = Path.Combine(AppContext.BaseDirectory, "Resources", "WebRoot", "index.html");
            
            // 检测是否是开发环境
            if (File.Exists(htmlPath))
            {
                webView.Source = new Uri(htmlPath);
            }
            else
            {
                // 如果文件不存在，可能是开发模式，尝试连 localhost
                webView.Source = new Uri("http://localhost:5173");
            }
        }

        private void InitializeTray()
        {
            _taskbarIcon = new Hardcodet.Wpf.TaskbarNotification.TaskbarIcon();
            _taskbarIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetEntryAssembly().Location);
            _taskbarIcon.ToolTipText = "JiaoLong Control";
            _taskbarIcon.TrayMouseDoubleClick += (s, e) => this.Show();

            // 构建右键菜单 
            var ctxMenu = new System.Windows.Controls.ContextMenu();
            
            var itemShow = new System.Windows.Controls.MenuItem { Header = "显示界面" };
            itemShow.Click += (s, e) => { this.Show(); this.WindowState = WindowState.Normal; this.Activate(); };
            
            var itemExit = new System.Windows.Controls.MenuItem { Header = "退出" };
            itemExit.Click += (s, e) => {
                FanControlService.Stop();
                Application.Current.Shutdown();
            };

            ctxMenu.Items.Add(itemShow);
            ctxMenu.Items.Add(itemExit);
            _taskbarIcon.ContextMenu = ctxMenu;
        }
    }
}