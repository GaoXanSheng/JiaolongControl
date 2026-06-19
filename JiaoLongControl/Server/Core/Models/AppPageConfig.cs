namespace JiaoLongControl.Server.Core.Models;

public class AppPageConfig : PageConfigBase
{
    public bool BootMinimized { get; set; }
    public bool BootAdvancedFanControlSystem { get; set; }
    public bool BootAdvancedCPUSystem { get; set; }
    public bool BootAdvancedGPUSystem { get; set; }
    public bool BootSetRyzenSumCurveOptimizerAll { get; set; }
}
