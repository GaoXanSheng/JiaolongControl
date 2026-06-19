namespace JiaoLongControl.Server.Core.Models;

public class CpuPageConfig : PageConfigBase
{
    public byte CpuLongPower { get; set; } = 45;
    public byte CpuShortPower { get; set; } = 55;
    public byte CpuTempWall { get; set; } = 95;
    public uint CpuMaxFrequency { get; set; } = 4800;
    public bool CpuTurbo { get; set; } = true;

    public CpuProfilePreset DefaultProfile { get; set; } = new()
    {
        CpuLongPower = 45, CpuShortPower = 55, CpuMaxFrequency = 4800, CpuTempWall = 95, CpuTurbo = true
    };
    public CpuProfilePreset PerformanceProfile { get; set; } = new()
    {
        CpuLongPower = 65, CpuShortPower = 90, CpuMaxFrequency = 5200, CpuTempWall = 98, CpuTurbo = true
    };
    public CpuProfilePreset SavingProfile { get; set; } = new()
    {
        CpuLongPower = 35, CpuShortPower = 45, CpuMaxFrequency = 4200, CpuTempWall = 75, CpuTurbo = false
    };
    public CpuProfilePreset CustomProfile { get; set; } = new();
}

public class CpuProfilePreset
{
    public byte CpuLongPower { get; set; }
    public byte CpuShortPower { get; set; }
    public byte CpuTempWall { get; set; }
    public uint CpuMaxFrequency { get; set; }
    public bool CpuTurbo { get; set; }
}
