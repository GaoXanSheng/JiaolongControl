using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using JiaoLongControl.Server.Interop;
using JiaoLongControl.Server.Core.Models;
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

            ConfigSerializer.Initialize();

            AppDomain.CurrentDomain.ProcessExit += (_, __) => Cleanup();
            AppDomain.CurrentDomain.UnhandledException += (_, __) => Cleanup();

            base.OnStartup(e);

            Task.Run(async () =>
            {
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

                var updater = new InnoUpdater(version);
                await updater.CheckForUpdatesAsync();
            });
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
                Bridge.Instance?.Fan?.RemoveFanSpeed();
                Bridge.Instance?.Dispose();
            }
            catch
            {
            }
        }
    }
}