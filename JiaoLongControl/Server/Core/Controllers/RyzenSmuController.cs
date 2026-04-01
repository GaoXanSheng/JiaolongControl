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

    private CommandResult Send(uint cmd, uint arg, bool isMp1, string name)
    {
        uint addrMsg = isMp1 ? 0x3B10530u : 0x03B10524u; 
        uint addrRsp = isMp1 ? 0x3B1057Cu : 0x03B10570u; 
        uint addrArg = isMp1 ? 0x3B109C4u : 0x03B10A40u;
        try
        {
            uint rsp = 0;
            for (int i = 0; i < 8096; ++i)
            {
                rsp = _pawn.ReadSmuRegister(addrRsp);
                if (rsp != 0) break;
            }
            if (rsp == 0) return new CommandResult(false, $"{name} 设置失败: SMU 忙碌超时");
            _pawn.WriteSmuRegister(addrRsp, 0);
            _pawn.WriteSmuRegister(addrArg, arg);
            _pawn.WriteSmuRegister(addrMsg, cmd);
            rsp = 0;
            for (int i = 0; i < 8096; ++i)
            {
                rsp = _pawn.ReadSmuRegister(addrRsp);
                if (rsp != 0) break;
            }
            if (rsp == 0) return new CommandResult(false, $"{name} 设置失败: SMU 响应超时");
            if (rsp == 1) return new CommandResult(true, $"{name} 设置成功");
            if (rsp == 0xFD) return new CommandResult(false, $"{name} 设置失败: 条件不满足");
            if (rsp == 0xFC) return new CommandResult(false, $"{name} 设置失败: 指令被拒绝(繁忙)");
            if (rsp == 0xFE) return new CommandResult(false, $"{name} 设置失败: 未知指令");

            return new CommandResult(false, $"{name} 设置失败: SMU 错误码 0x{rsp:X}");
        }
        catch (Exception ex)
        {
            return new CommandResult(false, $"{name} 设置失败: 底层驱动异常 {ex.Message}");
        }
    }
#region (Power Limits - PPT)
    public CommandResult SetStapmLimit(double watts) => Send(0x4F, (uint)(watts * 1000), true, "SetStapmLimit");
    public CommandResult SetStapmTime(uint seconds) => Send(0x53, seconds, true, "STAPM Time");
    public CommandResult SetFastLimit(double watts) => Send(0x3E, (uint)(watts * 1000), true, "Fast Limit");
    public CommandResult SetSlowLimit(double watts) => Send(0x5F, (uint)(watts * 1000), true, "Slow Limit");
    public CommandResult SetSlowTime(uint seconds) => Send(0x60, seconds, true, "Slow Time");
    public CommandResult SetPptLimitRsmu(double watts) => Send(0x56, (uint)(watts * 1000), false, "PPT Limit (RSMU)");

    #endregion

    #region (Current & Temp Limits)

    public CommandResult SetVrmCurrentMp1(uint milliamps) => Send(0x3C, milliamps, true, "VRM Current (MP1)");
    public CommandResult SetVrmCurrentRsmu(uint milliamps) => Send(0x57, milliamps, false, "VRM Current (RSMU)");
    
    public CommandResult SetEdcLimitMp1(uint milliamps) => Send(0x3D, milliamps, true, "EDC Limit (MP1)");
    public CommandResult SetEdcLimitRsmu(uint milliamps) => Send(0x58, milliamps, false, "EDC Limit (RSMU)");

    public CommandResult SetTempLimitMp1(uint celsius) => Send(0x3F, celsius, true, "Temp Limit (MP1)");
    public CommandResult SetTempLimitRsmu(uint celsius) => Send(0x59, celsius, false, "Temp Limit (RSMU)");
    
    #endregion
    #region (PBO & Overclocking)
    public CommandResult SetPboScalar(uint value) => Send(0x5B, value, false, "PBO Scalar");
    public CommandResult SetOcClk(int mhz) => Send(0x5F, (uint)mhz, false, "OC Clock");
    public CommandResult SetPerCoreOcClk(uint coreIdx, uint mhz) => Send(0x60, (coreIdx << 8) | (mhz & 0xFF), false, "Per Core OC Clock");
    
    public CommandResult SetOcVolt(uint millivolts) => Send(0x61, millivolts, false, "OC Voltage");
    
    public CommandResult EnableOc() => Send(0x5D, 0, false, "Enable OC Mode");
    public CommandResult DisableOc() => Send(0x5E, 0, false, "Disable OC Mode");

    #endregion
    #region (Curve Optimizer)
    public CommandResult SetCurveOptimizerAll(int value) 
        => Send(0x07, (uint)(value & 0xFFFFFFFF), false, "Curve Optimizer All");
    public CommandResult SetCurveOptimizerPerCore(uint coreIdx, int value)
        => Send(0x06, (coreIdx << 8) | (uint)(value & 0xFF), false, $"Curve Optimizer Core {coreIdx}");
    #endregion
    public void Dispose() => _pawn.Dispose();
}