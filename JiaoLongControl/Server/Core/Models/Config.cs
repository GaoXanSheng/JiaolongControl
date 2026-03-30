namespace JiaoLongControl.Server.Core.Models;

public class Config
{
    // 最小化启动
    public bool BootMinimized { get; set; } = false;

    // 自动启动风扇控制
    public bool BootAdvancedFanControlSystem { get; set; } = false;

    // 自动启动CPU配置
    public bool BootAdvancedCPUSystem { get; set; } = false;
    public bool BootAdvancedGPUSystem { get; set; } = false;
    public CpuConfg AdvancedCPUSystemConfig { get; set; } = new();

    public NvidiaGpuConfig NvidiaGpuConfig { get; set; } = new();

    public FanPageStore FanPageStore { get; set; } = new();

    // 风扇控制配置
    public List<FanPoint> AdvancedFanControlSystemConfig { get; set; } = new()
    {
        new FanPoint { temp = 60, speed = 1500 },
        new FanPoint { temp = 65, speed = 2104 },
        new FanPoint { temp = 70, speed = 2778 },
        new FanPoint { temp = 75, speed = 3158 },
        new FanPoint { temp = 80, speed = 3365 },
        new FanPoint { temp = 86, speed = 3607 },
        new FanPoint { temp = 91, speed = 3849 },
        new FanPoint { temp = 94, speed = 4828 },
        new FanPoint { temp = 97, speed = 5415 },
        new FanPoint { temp = 100, speed = 5800 }
    };
}

public class FanPageStore
{
    public int FanSpeed { get; set; } = 1500;
}
public class NvidiaGpuConfig
{
    public int GpuClock { get; set; } = 0;
    public int MemoryClock { get; set; } = 100;
    public int PowerLimit { get; set; } = 140;
}

public class CpuConfg
{
    public byte CpuShortPower { get; set; } = 65;
    public byte CpuLongPower { get; set; } = 70;
    public byte CpuTempWall { get; set; } = 90;
    public uint CpuMaxFrequency { get; set; } = 0;
    public bool CpuTurbo { get; set; } = true;
}

public class FanPoint
{
    public int temp { get; set; }
    public int speed { get; set; }
}