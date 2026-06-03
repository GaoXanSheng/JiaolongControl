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
    public bool BootSetRyzenSumCurveOptimizerAll { get; set; } = false;
    public bool FanCurveMerge { get; set; } = false;
    public CpuConfg AdvancedCPUSystemConfig { get; set; } = new();

    public NvidiaGpuConfig NvidiaGpuConfig { get; set; } = new();

    public FanPageStore FanPageStore { get; set; } = new();

    // 风扇控制配置
    public AdvancedFanControlSystemConfig AdvancedFanControlSystemConfig { get; set; }  = new();
    public RyzenSumConfig RyzenSumConfig { get; set; } = new();
}

public class AdvancedFanControlSystemConfig
{
    public List<FanPoint> CpuFan { get; set; }  = new List<FanPoint>()
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
    public List<FanPoint> GpuFan { get; set; }  = new List<FanPoint>()
    {
        new FanPoint { temp = 60, speed = 3000 },
        new FanPoint { temp = 65, speed = 4000 },
        new FanPoint { temp = 70, speed = 4800 },
        new FanPoint { temp = 75, speed = 5000 },
        new FanPoint { temp = 80, speed = 5400 },
        new FanPoint { temp = 87, speed = 5800 },
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

public class RyzenSumConfig
{
    public int StapmLimit { get; set; } = 0;
    public int StapmTime { get; set; } = 0;
    public int FastLimit { get; set; } = 0;
    public int SlowLimit { get; set; } = 0;
    public int SlowTime { get; set; } = 0;
    public int PptLimitRsmu { get; set; } = 0;
    public int VrmCurrentMp1 { get; set; } = 0;
    public int VrmCurrentRsmu { get; set; } = 0;
    public int TdcLimitMp1 { get; set; } = 0;
    public int TdcLimitRsmu { get; set; } = 0;
    public int EdcLimitMp1 { get; set; } = 0;
    public int EdcLimitRsmu { get; set; } = 0;
    public int TempLimitMp1 { get; set; } = 0;
    public int TempLimitRsmu { get; set; } = 0;
    public int PboScalar { get; set; } = 0;
    public int OcClk { get; set; } = 0;
    public int OcVolt { get; set; } = 0;
    public int CurveOptimizerAll { get; set; } = 0;
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