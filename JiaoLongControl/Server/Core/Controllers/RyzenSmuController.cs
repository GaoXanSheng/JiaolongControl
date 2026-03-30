using System.Runtime.InteropServices;
using JiaoLongControl.Server.Core.Drivers;
using JiaoLongControl.Server.Core.Utils;

namespace JiaoLongControl.Server.Core.Controllers;

[ComVisible(true)]
[ClassInterface(ClassInterfaceType.AutoDual)]
public class RyzenSmuController : IDisposable
{
    private readonly PawnIODriver _pawn;

    public RyzenSmuController()
    {
        _pawn = new PawnIODriver();
    }

    public int GetCpuCodeName()
    {
        var res = _pawn.Execute("ioctl_get_code_name", Array.Empty<ulong>(), 1);
        return (int)res[0];
    }

    private CommandResult Send(uint cmd, uint arg, string name)
    {
        ulong[] inputs = new ulong[7];
        inputs[0] = cmd;
        inputs[1] = arg;

        try
        {
            _pawn.Execute("ioctl_send_smu_command", inputs, 6);
            return new CommandResult(true, $"{name} 设置成功");
        }
        catch (Exception ex)
        {
            return new CommandResult(false, $"{name} 设置失败: ${ex.Message}");
        }
    }

    #region (Power Limits - PPT)

    // STAPM Limit (长期功耗)
    public CommandResult SetStapmLimit(double watts) => Send(0x4F, (uint)(watts * 1000), "STAPM Limit");

    // STAPM Time (功耗持续时间)
    public CommandResult SetStapmTime(uint seconds) => Send(0x53, seconds, "STAPM Time");

    // Fast PPT (瞬间爆发功耗)
    public CommandResult SetFastLimit(double watts) => Send(0x3E, (uint)(watts * 1000), "Fast Limit");

    // Slow PPT (持续爆发功耗)
    public CommandResult SetSlowLimit(double watts) => Send(0x5F, (uint)(watts * 1000), "Slow Limit");

    // Slow Time (持续爆发时间)
    public CommandResult SetSlowTime(uint seconds) => Send(0x60, seconds, "Slow Time");

    // PPT Limit (RSMU 通道)
    public CommandResult SetPptLimitRsmu(double watts) => Send(0x56, (uint)(watts * 1000), "PPT Limit (RSMU)");

    #endregion

    #region (Current Limits - TDC/EDC/VRM)

    // VRM Current (MP1)
    public CommandResult SetVrmCurrentMp1(uint milliamps) => Send(0x3C, milliamps, "VRM Current (MP1)");

    // VRM Current (RSMU)
    public CommandResult SetVrmCurrentRsmu(uint milliamps) => Send(0x57, milliamps, "VRM Current (RSMU)");

    // TDC Limit (MP1)
    public CommandResult SetTdcLimitMp1(uint milliamps) => Send(0x3C, milliamps, "TDC Limit (MP1)");

    // TDC Limit (RSMU)
    public CommandResult SetTdcLimitRsmu(uint milliamps) => Send(0x57, milliamps, "TDC Limit (RSMU)");

    // EDC Limit (MP1)
    public CommandResult SetEdcLimitMp1(uint milliamps) => Send(0x3D, milliamps, "EDC Limit (MP1)");

    // EDC Limit (RSMU)
    public CommandResult SetEdcLimitRsmu(uint milliamps) => Send(0x58, milliamps, "EDC Limit (RSMU)");

    #endregion

    #region (Thermal Control)

    // Tctl Temp (MP1) 
    public CommandResult SetTempLimitMp1(uint celsius) => Send(0x3F, celsius, "Temp Limit (MP1)");

    // Tctl Temp (RSMU) 
    public CommandResult SetTempLimitRsmu(uint celsius) => Send(0x59, celsius, "Temp Limit (RSMU)");

    #endregion

    #region (PBO & Overclocking)

    // PBO Scalar 
    public CommandResult SetPboScalar(uint value) => Send(0x5B, value, "PBO Scalar");

    // OC Clock Offset (频率偏移)
    public CommandResult SetOcClk(int mhz) => Send(0x5F, (uint)mhz, "OC Clock");

    // Per Core OC Clock (单核频率偏移) 
    public CommandResult SetPerCoreOcClk(uint coreIdx, uint mhz) =>
        Send(0x60, (coreIdx << 8) | (mhz & 0xFF), "Per Core OC Clock");

    // OC Voltage (超频电压) 
    public CommandResult SetOcVolt(uint millivolts) => Send(0x61, millivolts, "OC Voltage");

    // Enable OC (解锁超频) 
    public CommandResult EnableOc() => Send(0x5D, 0, "Enable OC Mode");

    // Disable OC (关闭超频) 
    public CommandResult DisableOc() => Send(0x5E, 0, "Disable OC Mode");

    #endregion

    #region (Curve Optimizer)

    // Set CO All (全核降压) 负数为加压
    public CommandResult SetCurveOptimizerAll(int value) => Send(0x07, (uint)value, "Curve Optimizer All");

    // Set CO Per Core (单核降压)
    public CommandResult SetCurveOptimizerPerCore(uint coreIdx, int value)
        => Send(0x06, (coreIdx << 8) | (uint)(value & 0xFF), $"Curve Optimizer Core {coreIdx}");
    #endregion

    public void Dispose() => _pawn?.Dispose();
}