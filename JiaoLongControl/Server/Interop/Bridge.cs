using System.Runtime.InteropServices;
using JiaoLongControl.Server.Core.Controllers;
using JiaoLongControl.Server.Core.Models;
using JiaoLongControl.Server.Core.Utils;
using log4net;
using Microsoft.Web.WebView2.Core;

namespace JiaoLongControl.Server.Interop
{
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.AutoDual)]
    public class Bridge : IDisposable
    {
        private const int SaveIntervalMs = 5000;

        private static readonly ILog Logger =
            LogManager.GetLogger(typeof(Bridge));

        private readonly object _configLock = new();
        private Timer _saveTimer;

        private CoreWebView2? _webView;

        public Bridge()
        {
            Config = ConfigSerializer.Load();
            _saveTimer = new Timer(
                _ => FlushIfDirty(),
                null,
                SaveIntervalMs,
                SaveIntervalMs);
        }

        public static Bridge Instance { get; } = new();
        internal JiaoLongConfig Config { get; private set; } = null!;

        public CpuController CPU { get; } = new();
        public FanController Fan { get; } = new();
        public GpuController GPU { get; } = new();
        public LogoLightController LogoLight { get; } = new();
        public KeyboardController Keyboard { get; } = new();
        public PerformanceModeController PerformanceMode { get; } = new();
        public ConfigController ConfigCtrl { get; } = new();
        public AutoStartController AutoStart { get; } = new();
        public AutoFanControl AutoFan { get; } = new();
        public KeyboardGradientController KeyboardGradient { get; } = new();
        public PowerController Power { get; } = new();
        public NvidiaGpuController NvidiaGpu { get; } = new();
        public RyzenSmuController RyzenSmu { get; } = new();
        public SystemInfoController SystemInfo { get; } = new();
        public OsdController Osd { get; } = new();

        public void Dispose()
        {
            _saveTimer.Dispose();
            _saveTimer = null;
            FlushIfDirty();
            CPU.Dispose();
            Fan.Dispose();
            AutoFan.Dispose();
            KeyboardGradient.Dispose();
            RyzenSmu.Dispose();
            NvidiaGpu.Dispose();
            Osd.Dispose();
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
    }
}
