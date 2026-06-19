namespace JiaoLongControl.Server.Core.Models;

public class GpuPageConfig : PageConfigBase
{
    public int GpuClock { get; set; }
    public int MemoryClock { get; set; } = 100;
    public int PowerLimit { get; set; } = 140;
}
