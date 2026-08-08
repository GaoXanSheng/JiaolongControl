using System.Runtime.InteropServices;
using JiaoLongControl.Server.Core.Controllers;
using JiaoLongControl.Server.Core.Models;
using JiaoLongControl.Server.Core.Utils;
using Microsoft.Web.WebView2.Core;

namespace JiaoLongControl.Server.Interop
{
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.AutoDual)]
    public class Bridge : IDisposable
    {
        private static readonly log4net.ILog Logger =
            log4net.LogManager.GetLogger(typeof(Bridge));

        public static Bridge Instance { get; } = new();

        private CoreWebView2? _webView;
        private ConfigWatcher? _watcher;

        internal JiaoLongConfig Config { get; set; } = null!;

        public Bridge()
        {
            Config = ConfigSerializer.Load();
        }

        public void InitWebView(CoreWebView2 webView)
        {
            _webView = webView;

            // WebView 重建时旧 watcher 必须先释放，避免 FileSystemWatcher 泄漏（重复订阅）
            _watcher?.Dispose();
            _watcher = new ConfigWatcher(ConfigSerializer.ConfigDir);
            _watcher.ConfigChanged += () =>
            {
                try
                {
                    Config = ConfigSerializer.Load();
                    _webView?.PostWebMessageAsJson("{\"type\":\"config-changed\"}");
                }
                catch (Exception ex)
                {
                    // 浏览器进程崩溃/重建窗口期调用已失效的 CoreWebView2 会抛 COM 异常；
                    // watcher 在后台线程触发，未捕获会走 AppDomain.UnhandledException 导致闪退，必须兜底
                    Logger.Warn($"config-changed 通知失败: {ex.Message}");
                }
            };
        }

        public CpuController CPU { get; } = new();
        public FanController Fan { get; } = new();
        public GpuController GPU { get; } = new();
        public LogoLightController LogoLight { get; } = new();
        public KeyboardController Keyboard { get; } = new();
        public PerformanceModeController PerformanceMode { get; } = new();
        public ConfigController ConfigCtrl { get; } = new();
        public AutoStartController AutoStart { get; } = new();
        public AutoFanControl AutoFan { get; } = new();
        public PowerController Power { get; } = new();
        public NvidiaGpuController NvidiaGpu { get; } = new();
        public RyzenSmuController RyzenSmu { get; } = new();
        public SystemInfoController SystemInfo { get; } = new();

        public void Dispose()
        {
            _watcher?.Dispose();
            CPU.Dispose();
            Fan.Dispose();
            AutoFan.Dispose();
            RyzenSmu.Dispose();
            NvidiaGpu.Dispose();
        }
    }
}
