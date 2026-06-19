namespace JiaoLongControl.Server.Core.Models;

public class SmuPageConfig : PageConfigBase
{
    public int StapmLimit { get; set; }
    public int StapmTime { get; set; }
    public int FastLimit { get; set; }
    public int SlowLimit { get; set; }
    public int SlowTime { get; set; }
    public int PptLimitRsmu { get; set; }
    public int VrmCurrentMp1 { get; set; }
    public int VrmCurrentRsmu { get; set; }
    public int TdcLimitMp1 { get; set; }
    public int TdcLimitRsmu { get; set; }
    public int EdcLimitMp1 { get; set; }
    public int EdcLimitRsmu { get; set; }
    public int TempLimitMp1 { get; set; }
    public int TempLimitRsmu { get; set; }
    public int PboScalar { get; set; }
    public int OcClk { get; set; }
    public int OcVolt { get; set; }
    public int CurveOptimizerAll { get; set; }
}
