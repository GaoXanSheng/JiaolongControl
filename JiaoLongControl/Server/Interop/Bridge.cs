using System.Runtime.InteropServices;
using JiaoLongControl.Server.Core.Controllers;
using JiaoLongControl.Server.Core.Models;

namespace JiaoLongControl.Server.Interop
{
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.AutoDual)]
    public class Bridge  : IDisposable
    {
        public static Bridge Instance { get; } = new();

        public AppPageConfig AppConfig { get; set; } = null!;
        public CpuPageConfig CpuConfig { get; set; } = null!;
        public GpuPageConfig GpuConfig { get; set; } = null!;
        public FanPageConfig FanConfig { get; set; } = null!;
        public SmuPageConfig SmuConfig { get; set; } = null!;

        public Bridge()
        {
            LoadAllConfigs();
        }

        public void LoadAllConfigs()
        {
            PageConfigBase.InitializeConfigs();
            AppConfig = PageConfigBase.Load<AppPageConfig>("app.json");
            CpuConfig = PageConfigBase.Load<CpuPageConfig>("cpu.json");
            GpuConfig = PageConfigBase.Load<GpuPageConfig>("gpu.json");
            FanConfig = PageConfigBase.Load<FanPageConfig>("fan.json");
            SmuConfig = PageConfigBase.Load<SmuPageConfig>("smu.json");
        }

        public CpuController CPU { get; } = new();
        public FanController Fan { get; } = new();
        public GpuController GPU { get; } = new();
        public LogoLightController LogoLight { get; } = new();
        public KeyboardController Keyboard { get; } = new();
        public PerformanceModeController PerformanceMode { get; } = new();
        public ConfigController Config { get; } = new();
        public AutoStartController AutoStart { get; } = new();
        public AutoFanControl AutoFan { get; } = new();
        public PowerController Power { get; } = new();
        public NvidiaGpuController NvidiaGpu { get; } = new();
        public RyzenSmuController RyzenSmu { get; } = new();
        public SystemInfoController SystemInfo { get; } = new();

        public void Dispose()
        {
            CPU.Dispose();
            Fan.Dispose();
            AutoFan.Dispose();
            RyzenSmu.Dispose();
            NvidiaGpu.Dispose();
        }
    }
}