using System.Reflection;
using System.Windows;
using JiaoLongControl.Server.Interop;
using JiaoLongControl.Server.Core.Utils;
using log4net;
using log4net.Config;

namespace JiaoLongControl.Server
{
    public partial class App : Application
    {
        private static Mutex? _mutex;
        private readonly ILog Logger = LogManager.GetLogger(typeof(App));

        protected override void OnStartup(StartupEventArgs e)
        {
            XmlConfigurator.Configure();
            const string appName = "JiaoLongControl_Main_Instance";
            bool createdNew;
            _mutex = new Mutex(true, appName, out createdNew);
            if (!createdNew)
            {
                MessageBox.Show("程序已在运行中。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                Current.Shutdown();
                return;
            }

            var version = "0.0.0";
            foreach (var attr in Assembly.GetExecutingAssembly()
                         .GetCustomAttributes<AssemblyMetadataAttribute>())
            {
                if (attr.Key == "AppVersion")
                {
                    version = attr.Value ?? "0.0.0";
                    break;
                }
            }

            ConfigSerializer.Initialize(version);

            // 全局异常兜底：任何未处理异常都记录日志，避免闪退后无迹可查
            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                Logger.Fatal("AppDomain 未处理异常（应用即将终止）: " +
                             (args.ExceptionObject as Exception)?.ToString(), args.ExceptionObject as Exception);
                Cleanup();
            };

            // UI 线程（Dispatcher）未处理异常：记录日志并阻止闪退
            DispatcherUnhandledException += (_, e) =>
            {
                Logger.Error("UI 线程未处理异常: " + e.Exception, e.Exception);
                e.Handled = true;
            };

            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                Logger.Error("未观察到的 Task 异常: " + e.Exception, e.Exception);
                e.SetObserved();
            };

            AppDomain.CurrentDomain.ProcessExit += (_, __) => Cleanup();

            base.OnStartup(e);

            Task.Run(async () =>
            {
                var updater = new InnoUpdater(version);
                await updater.CheckForUpdatesAsync();
            });

            var mainWindow = new MainWindow();
            bool startInTray = Environment.GetCommandLineArgs()
                .Any(arg => arg.Equals("--boot", StringComparison.OrdinalIgnoreCase)) &&
                Bridge.Instance.Config.App.BootMinimized;

            if (!startInTray)
            {
                mainWindow.Show();
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Cleanup();
            base.OnExit(e);
        }

        private void Cleanup()
        {
            try
            {
                Bridge.Instance.Fan.RemoveFanSpeed();
                Bridge.Instance.Dispose();
            }
            catch (Exception ex)
            {
                Logger.Error("Cleanup failed: " + ex.Message, ex);
            }
        }
    }
}