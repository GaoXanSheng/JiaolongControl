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
        private const int SaveIntervalMs = 5000;

        private readonly object _configLock = new();
        private System.Threading.Timer? _saveTimer;
        internal JiaoLongConfig Config { get; private set; } = null!;

        public Bridge()
        {
            Config = ConfigSerializer.Load();
            _saveTimer = new System.Threading.Timer(
                _ => FlushIfDirty(),
                null,
                SaveIntervalMs,
                SaveIntervalMs);
        }
        
        public void ApplyConfig(JiaoLongConfig config)
        {
            lock (_configLock)
            {
                Config = config;
            }

            try
            {
                _webView?.PostWebMessageAsJson("{\"type\":\"config-changed\"}");
            }
            catch (Exception ex)
            {
                Logger.Warn($"config-changed 通知失败: {ex.Message}");
            }
        }
        internal void FlushIfDirty()
        {
            try
            {
                string memoryYaml;
                lock (_configLock)
                {
                    memoryYaml = ConfigSerializer.Serialize(Config);
                }

                var diskYaml = ConfigSerializer.ReadFileContent();
                if (diskYaml != memoryYaml)
                {
                    lock (_configLock)
                    {
                        ConfigSerializer.Save(Config);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"配置差异落盘失败: {ex.Message}");
            }
        }

        public void InitWebView(CoreWebView2 webView)
        {
            _webView = webView;
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
            _saveTimer?.Dispose();
            _saveTimer = null;
            FlushIfDirty();
            CPU.Dispose();
            Fan.Dispose();
            AutoFan.Dispose();
            RyzenSmu.Dispose();
            NvidiaGpu.Dispose();
        }
    }
}
