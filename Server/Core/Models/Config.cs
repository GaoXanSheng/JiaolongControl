namespace JiaoLongControl.Server.Core.Models;

public class Config
{
    // 最小化启动
    public bool MinimizedAfterBooting { get; set; } = false;

    // 是否启动
    public bool EnableAdvancedFanControlSystem { get; set; } = false;

    // 自动启动风扇控制
    public bool BootStartAdvancedFanControlSystem { get; set; } = false;

    // 风扇控制配置
    public List<FanPoint> AdvancedFanControlSystemConfig { get; set; } = new()
    {
        new FanPoint { temp = 60, speed = 1500 },
        new FanPoint { temp = 80, speed = 3000 },
        new FanPoint { temp = 100, speed = 5800 }
    };
}

public class FanPoint
{
    public int temp { get; set; }
    public int speed { get; set; }
}