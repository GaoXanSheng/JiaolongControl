using System.Text;
using System.Windows;
using JiaoLongControl.Server.Core.Controllers;
using JiaoLongControl.Server.Interop;
using log4net;
using log4net.Config;

namespace JiaoLongControl.Server
{
    
    public partial class App : Application
    {
        private static Mutex? _mutex;
        public static readonly ILog Logger= LogManager.GetLogger(typeof(App));
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
            base.OnStartup(e);
        }
        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);
            Bridge.Instance.Fan.RemoveFanSpeed();
        }
    }
}