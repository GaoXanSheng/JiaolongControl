namespace JiaoLongControl.Server.Core.Models;

public class FanPageConfig : PageConfigBase
{
    public bool FanCurveMerge { get; set; }
    public int ManualFanSpeed { get; set; } = 1500;
    public List<FanPoint> CpuFanCurve { get; set; } = new()
    {
        new() { temp = 60, speed = 1500 }, new() { temp = 65, speed = 2104 },
        new() { temp = 70, speed = 2778 }, new() { temp = 75, speed = 3158 },
        new() { temp = 80, speed = 3365 }, new() { temp = 86, speed = 3607 },
        new() { temp = 91, speed = 3849 }, new() { temp = 94, speed = 4828 },
        new() { temp = 97, speed = 5415 }, new() { temp = 100, speed = 5800 },
    };
    public List<FanPoint> GpuFanCurve { get; set; } = new()
    {
        new() { temp = 60, speed = 3000 }, new() { temp = 65, speed = 4000 },
        new() { temp = 70, speed = 4800 }, new() { temp = 75, speed = 5000 },
        new() { temp = 80, speed = 5400 }, new() { temp = 87, speed = 5800 },
    };
}

public class FanPoint
{
    public int temp { get; set; }
    public int speed { get; set; }
}
