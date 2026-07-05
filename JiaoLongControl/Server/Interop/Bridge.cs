using System.Runtime.InteropServices;
using JiaoLongControl.Server.Core.Controllers;
using JiaoLongControl.Server.Core.Models;
using Microsoft.Web.WebView2.Core;

namespace JiaoLongControl.Server.Interop
{
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.AutoDual)]
    public class Bridge : IDisposable
    {
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

            _watcher = new ConfigWatcher(ConfigSerializer.ConfigDir);
            _watcher.ConfigChanged += () =>
            {
                Config = ConfigSerializer.Load();
                _webView?.PostWebMessageAsJson("{\"type\":\"config-changed\"}");
            };
        }

        public CpuController CPU { get; } = new();
        public FanController Fan { get; } = new();
        public GpuController GPU { get; } = new();
        public LogoLightController LogoLight { get; } = new();
        public KeyboardController Keyboard { get; } = new();
        public PerformanceModeController PerformanceMode { get; } = new();
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
