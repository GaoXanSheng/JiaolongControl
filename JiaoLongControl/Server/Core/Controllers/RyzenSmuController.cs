using System.Management;
using System.Runtime.InteropServices;
using JiaoLongControl.Server.Core.Drivers;
using JiaoLongControl.Server.Core.Services;
using JiaoLongControl.Server.Core.Utils;
using Microsoft.Win32;

namespace JiaoLongControl.Server.Core.Controllers;

public enum RyzenSmuFamily
{
    AM5_V1,         // Dragon Range 
    FP7_FP8,        // Rembrandt / Phoenix / HawkPoint
    FP7_FP8_Strix,  // Strix Point / Krackan Point / Strix Halo 
    FP6             // Cezanne / Lucienne / Renoir
}

[ComVisible(true)]
[ClassInterface(ClassInterfaceType.AutoDual)]
public class RyzenSmuController : PawnIO
{
    /// <summary>SMU 忙碌/响应轮询上限（沿用历史值 8096）。</summary>
    private const int SmuPollLimit = 8096;

    private static readonly Log Logger = Log.For<RyzenSmuController>();

    /// <summary>STAPM/FAST/SLOW 限值指令的家族序列表（TrySend 依序尝试直到成功）。</summary>
    private static readonly ((uint, bool)[] Fp, (uint, bool)[] Am5)
        StapmLimitCmds = (new[] { (0x14u, true), (0x31u, false) }, new[] { (0x4Fu, true) }),
        StapmTimeCmds  = (new[] { (0x18u, true), (0x36u, false) }, new[] { (0x53u, true) }),
        FastLimitCmds  = (new[] { (0x15u, true), (0x32u, false) }, new[] { (0x3Eu, true) }),
        SlowLimitCmds  = (new[] { (0x16u, true), (0x33u, false) }, new[] { (0x5Fu, true), (0xCBu, false) }),
        SlowTimeCmds   = (new[] { (0x17u, true), (0x35u, false) }, new[] { (0x60u, true) });

    public RyzenSmuController()
    {
        try
        {
            (string brand, string identifier) = GetCpuIdentityFast();
            CpuIdentity cpu = SmuFamilyResolver.FromBrandAndIdentifier(brand, identifier);
            CurrentFamily = SmuFamilyResolver.Resolve(cpu, out string evidence);
            Logger.Info($"SMU 平台判定: {CurrentFamily} [{evidence}] 品牌: \"{cpu.BrandName}\"");
        }
        catch (Exception ex)
        {
            // 判定异常不再静默吞掉: 保持默认 AM5_V1, 但必须记录原因
            Logger.Error("SMU 平台判定异常, 保持默认 AM5_V1", ex);
        }
    }

    public RyzenSmuFamily CurrentFamily { get; set; } = RyzenSmuFamily.AM5_V1;

    /// <summary>FP 家族（FP6/FP7_FP8/Strix）与 AM5 指令集不同，按当前平台判定家族。</summary>
    private bool IsFpFamily => CurrentFamily is not RyzenSmuFamily.AM5_V1;

    /// <summary>
    /// 快速获取 CPU 身份: 注册表同键一次读出品牌串与 Identifier（即 CPUID Family/Model 的十进制文本）。
    /// 注册表不可用时回退 WMI（Description 与 Identifier 同格式）。
    /// </summary>
    private static (string Brand, string Identifier) GetCpuIdentityFast()
    {
        string brand = "";
        string identifier = "";
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            brand = key?.GetValue("ProcessorNameString")?.ToString() ?? "";
            identifier = key?.GetValue("Identifier")?.ToString() ?? "";
        }
        catch
        {
            // 注册表不可用时走 WMI 回退
        }

        if (string.IsNullOrWhiteSpace(brand))
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name, Description FROM Win32_Processor");
            foreach (ManagementObject obj in searcher.Get())
            {
                brand = obj["Name"]?.ToString() ?? "";
                identifier = obj["Description"]?.ToString() ?? "";
                break;
            }
        }

        return (brand, identifier);
    }

    private (uint Msg, uint Rsp, uint Arg) GetMailboxAddrs(bool isMp1)
    {
        return CurrentFamily switch
        {
            RyzenSmuFamily.FP6 => (
                isMp1 ? 0x3B10528u : 0x03B10A20u,
                isMp1 ? 0x3B10564u : 0x03B10A80u,
                isMp1 ? 0x3B10998u : 0x03B10A88u),
            RyzenSmuFamily.FP7_FP8 => (
                isMp1 ? 0x3B10528u : 0x03B10A20u,
                isMp1 ? 0x3B10578u : 0x03B10A80u,
                isMp1 ? 0x3B10998u : 0x03B10A88u),
            RyzenSmuFamily.FP7_FP8_Strix => (
                isMp1 ? 0x3B10928u : 0x03B10A20u,
                isMp1 ? 0x3B10978u : 0x03B10A80u,
                isMp1 ? 0x3B10998u : 0x03B10A88u),
            _ => (
                isMp1 ? 0x3B10530u : 0x03B10524u,
                isMp1 ? 0x3B1057Cu : 0x03B10570u,
                isMp1 ? 0x3B109C4u : 0x03B10A40u),
        };
    }

    private CommandResult Send(uint cmd, uint arg, bool isMp1, string name)
    {
        var (addrMsg, addrRsp, addrArg) = GetMailboxAddrs(isMp1);

        try
        {
            uint rsp = 0;
            for (int i = 0; i < SmuPollLimit; ++i)
            {
                rsp = (uint)Execute("ioctl_read_smu_register", new ulong[] { addrRsp }, 1)[0];
                if (rsp != 0) break;
            }
            if (rsp == 0) return new CommandResult(false, $"{name} 设置失败: SMU 忙碌超时");
            
            Execute("ioctl_write_smu_register", new ulong[] { addrRsp, 0 }, 0);
            Execute("ioctl_write_smu_register", new ulong[] { addrArg, arg }, 0);
            for (uint i = 1; i < 6; i++)
            {
                Execute("ioctl_write_smu_register", new ulong[] { addrArg + (i * 4), 0 }, 0);
            }

            Execute("ioctl_write_smu_register", new ulong[] { addrMsg, cmd }, 0);
            
            rsp = 0;
            for (int i = 0; i < SmuPollLimit; ++i)
            {
                rsp = (uint)Execute("ioctl_read_smu_register", new ulong[] { addrRsp }, 1)[0];
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

    private CommandResult TrySend(uint arg, string name, params (uint cmd, bool isMp1)[] commands)
    {
        CommandResult? lastResult = null;
        foreach (var (cmd, isMp1) in commands)
        {
            lastResult = Send(cmd, arg, isMp1, $"{name} [{(isMp1 ? "MP1" : "RSMU")} 0x{cmd:X2}]");
            if (lastResult.Success)
            {
                return lastResult;
            }
        }
        return lastResult ?? new CommandResult(false, $"{name} 设置失败: 无可用指令");
    }

    /// <summary>按家族二选一取指令后经 Send 下发（消息文案与原实现一致）。</summary>
    private CommandResult SendForFamily(uint fpCmd, uint am5Cmd, uint arg, bool isMp1, string name)
        => Send(IsFpFamily ? fpCmd : am5Cmd, arg, isMp1, name);

    /// <summary>FP6 / FP7_FP8+Strix / AM5 三家族指令各不相同时按当前家族取指令。</summary>
    private CommandResult SendPerFamily(uint fp6Cmd, uint fp7Cmd, uint am5Cmd, uint arg, bool isMp1, string name)
        => Send(CurrentFamily switch {
            RyzenSmuFamily.FP6 => fp6Cmd,
            RyzenSmuFamily.FP7_FP8 or RyzenSmuFamily.FP7_FP8_Strix => fp7Cmd,
            _ => am5Cmd
        }, arg, isMp1, name);

    private CommandResult TrySendForFamily(((uint, bool)[] Fp, (uint, bool)[] Am5) cmds, uint arg, string name)
        => TrySend(arg, name, IsFpFamily ? cmds.Fp : cmds.Am5);

    /// <summary>
    /// 调试用：向指定邮箱发送原始 SMU 命令（测试工具 --smu-cmd 使用）
    /// </summary>
    public CommandResult SendRaw(uint cmd, uint arg, bool isMp1, string name)
        => Send(cmd, arg, isMp1, name);

    /// <summary>
    /// 调试用：读取邮箱 6 个参数槽（Get 类命令执行后 SMU 写回的返回值，测试工具 --smu-cmd-read 使用）
    /// </summary>
    public ulong[] ReadMailboxArgs(bool isMp1)
    {
        uint addrArg = GetMailboxAddrs(isMp1).Arg;
        var args = new ulong[6];
        for (uint i = 0; i < 6; i++)
        {
            args[i] = Execute("ioctl_read_smu_register", new ulong[] { addrArg + i * 4 }, 1)[0];
        }
        return args;
    }

    #region (Power Telemetry)

    public CommandResult GetSmuTelemetry()
    {
        var cpu = HardwareMonitorService.ReadCpu();
        return new CommandResult(true, "获取成功", new
        {
            Ppt = Math.Round(cpu.PackagePower, 1),
            Temp = (double)cpu.Temperature,
            FreqMhz = cpu.FrequencyMhz,
            Usage = cpu.Usage,
        });
    }

    #endregion

    #region (Power Limits - PPT)

    public CommandResult SetStapmLimit(double watts)
        => TrySendForFamily(StapmLimitCmds, (uint)(watts * 1000), "STAPM Limit");

    public CommandResult SetStapmTime(uint seconds)
        => TrySendForFamily(StapmTimeCmds, seconds, "STAPM Time");

    public CommandResult SetFastLimit(double watts)
        => TrySendForFamily(FastLimitCmds, (uint)(watts * 1000), "Fast Limit");

    public CommandResult SetSlowLimit(double watts)
        => TrySendForFamily(SlowLimitCmds, (uint)(watts * 1000), "Slow Limit");

    public CommandResult SetSlowTime(uint seconds)
        => TrySendForFamily(SlowTimeCmds, seconds, "Slow Time");

    public CommandResult SetPptLimitRsmu(double watts)
        => SendPerFamily(0x33u, 0x31u, 0x56u, (uint)(watts * 1000), false, "PPT Limit (RSMU)");

    #endregion

    #region (Current & Temp Limits)

    public CommandResult SetVrmCurrentMp1(uint milliamps)
        => SendForFamily(0x1Au, 0x3Cu, milliamps, true, "VRM Current (MP1)");

    public CommandResult SetVrmCurrentRsmu(uint milliamps)
        => SendForFamily(0x38u, 0x57u, milliamps, false, "VRM Current (RSMU)");

    public CommandResult SetEdcLimitMp1(uint milliamps)
        => SendForFamily(0x1Cu, 0x3Du, milliamps, true, "EDC Limit (MP1)");

    public CommandResult SetEdcLimitRsmu(uint milliamps)
        => SendForFamily(0x3Au, 0x58u, milliamps, false, "EDC Limit (RSMU)");

    public CommandResult SetTempLimitMp1(uint celsius)
        => SendForFamily(0x19u, 0x3Fu, celsius, true, "Temp Limit (MP1)");

    public CommandResult SetTempLimitRsmu(uint celsius)
        => SendForFamily(0x37u, 0x59u, celsius, false, "Temp Limit (RSMU)");

    #endregion

    #region (PBO & Overclocking)

    public CommandResult SetPboScalar(uint value)
        => SendPerFamily(0x3Fu, 0x3Eu, 0x5Bu, value, false, "PBO Scalar");

    public CommandResult SetOcClk(int mhz)
        => SendForFamily(0x19u, 0x5Fu, (uint)mhz, false, "OC Clock");

    public CommandResult SetPerCoreOcClk(uint coreIdx, uint mhz)
        => SendForFamily(0x1Au, 0x60u, (coreIdx << 8) | (mhz & 0xFF), false, "Per Core OC Clock");

    public CommandResult SetOcVolt(uint millivolts)
        => SendForFamily(0x1Bu, 0x61u, millivolts, false, "OC Voltage");

    public CommandResult EnableOc()
        => SendForFamily(0x17u, 0x5Du, 0, false, "Enable OC Mode");

    public CommandResult DisableOc()
        => SendForFamily(0x18u, 0x5Eu, 0, false, "Disable OC Mode");

    #endregion

    #region (Curve Optimizer)

    public CommandResult SetCurveOptimizerAll(int value)
    {
        uint arg = (uint)value & 0xFFFFFu;
        return CurrentFamily switch {
            RyzenSmuFamily.FP6 => TrySend(arg, "Curve Optimizer All", (0x55, true), (0xB1, false)),
            RyzenSmuFamily.FP7_FP8 or RyzenSmuFamily.FP7_FP8_Strix
                => TrySend(arg, "Curve Optimizer All", (0x4C, true), (0x5D, false)),
            _ => TrySend(arg, "Curve Optimizer All", (0x36, true), (0x07, false))
        };
    }

    public CommandResult SetCurveOptimizerPerCore(uint coreIdx, int value)
    {
        uint marginArg = (uint)((value < 0 ? 0x100000 : 0) + value) & 0xFFFFu;
        return CurrentFamily switch {
            RyzenSmuFamily.FP6 => TrySend((coreIdx << 20) | ((uint)value & 0xFFFFFu), $"Curve Optimizer Core {coreIdx}", (0x54, true), (0x52, false)),
            RyzenSmuFamily.FP7_FP8 or RyzenSmuFamily.FP7_FP8_Strix
                => TrySend((coreIdx << 20) | ((uint)value & 0xFFFFFu), $"Curve Optimizer Core {coreIdx}", (0x4B, true), (0x53, false)),
            _ => TrySend((((coreIdx / 8) << 28) | ((coreIdx % 8) << 20)) | marginArg, $"Curve Optimizer Core {coreIdx}", (0x35, true), (0x06, false))
        };
    }

    #endregion
}