using System;
using System.Runtime.InteropServices;
using JiaoLongControl.Server.Core.Drivers;
using JiaoLongControl.Server.Core.Native;
using JiaoLongControl.Server.Core.Utils;

namespace JiaoLongControl.Server.Core.Controllers;

public enum RyzenSmuFamily
{
    AM5_V1,     // Dragon Range
    FP7_FP8,    // Rembrandt / Phoenix / HawkPoint 
    FP6         // Cezanne / Lucienne / Renoir
}

[ComVisible(true)]
[ClassInterface(ClassInterfaceType.AutoDual)]
public class RyzenSmuController : PawnIO 
{
    public RyzenSmuFamily CurrentFamily { get; set; } = RyzenSmuFamily.AM5_V1;

    public RyzenSmuController()
    {
        // 自动检测 CPU 型号以匹配不同代数的 SMU 内存地址和指令集
        try
        {
            using var searcher = new System.Management.ManagementObjectSearcher("SELECT Name FROM Win32_Processor");
            foreach (System.Management.ManagementObject obj in searcher.Get())
            {
                string cpuName = obj["Name"]?.ToString() ?? "";
                
                // 常用 CPU 自动归类识别
                if (cpuName.Contains("7945") || cpuName.Contains("7845") || cpuName.Contains("7745"))
                    CurrentFamily = RyzenSmuFamily.AM5_V1;
                else if (cpuName.Contains("7735") || cpuName.Contains("6800") || cpuName.Contains("6900") || cpuName.Contains("7840") || cpuName.Contains("7940") || cpuName.Contains("8840") || cpuName.Contains("8845"))
                    CurrentFamily = RyzenSmuFamily.FP7_FP8;
                else if (cpuName.Contains("5800") || cpuName.Contains("5900") || cpuName.Contains("5600") || cpuName.Contains("4800") || cpuName.Contains("4600"))
                    CurrentFamily = RyzenSmuFamily.FP6;
                else
                    CurrentFamily = RyzenSmuFamily.AM5_V1; // 默认回退到最新架构
                
                break;
            }
        }
        catch { }
    }

    private CommandResult Send(uint cmd, uint arg, bool isMp1, string name)
    {
        uint addrMsg, addrRsp, addrArg;

        // 根据不同 CPU 架构分配底层 SMU 寄存器地址
        switch (CurrentFamily)
        {
            case RyzenSmuFamily.FP6:
                addrMsg = isMp1 ? 0x3B10528u : 0x03B10A20u;
                addrRsp = isMp1 ? 0x3B10564u : 0x03B10A80u;
                addrArg = isMp1 ? 0x3B10998u : 0x03B10A88u;
                break;
            case RyzenSmuFamily.FP7_FP8:
                addrMsg = isMp1 ? 0x3B10528u : 0x03B10A20u;
                addrRsp = isMp1 ? 0x3B10578u : 0x03B10A80u;
                addrArg = isMp1 ? 0x3B10998u : 0x03B10A88u;
                break;
            case RyzenSmuFamily.AM5_V1:
            default:
                addrMsg = isMp1 ? 0x3B10530u : 0x03B10524u;
                addrRsp = isMp1 ? 0x3B1057Cu : 0x03B10570u;
                addrArg = isMp1 ? 0x3B109C4u : 0x03B10A40u;
                break;
        }

        try
        {
            uint rsp = 0;
            for (int i = 0; i < 8096; ++i)
            {
                rsp = (uint)Execute("ioctl_read_smu_register", new ulong[] { addrRsp }, 1)[0];
                if (rsp != 0) break;
            }
            if (rsp == 0) return new CommandResult(false, $"{name} 设置失败: SMU 忙碌超时");
            
            Execute("ioctl_write_smu_register", new ulong[] { addrRsp, 0 }, 0);
            Execute("ioctl_write_smu_register", new ulong[] { addrArg, arg }, 0);
            Execute("ioctl_write_smu_register", new ulong[] { addrMsg, cmd }, 0);
            
            rsp = 0;
            for (int i = 0; i < 8096; ++i)
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

    #region (Power Limits - PPT)
    public CommandResult SetStapmLimit(double watts)
    {
        uint cmd = CurrentFamily switch { RyzenSmuFamily.FP6 => 0x14u, RyzenSmuFamily.FP7_FP8 => 0x14u, _ => 0x4Fu };
        return Send(cmd, (uint)(watts * 1000), true, "SetStapmLimit");
    }

    public CommandResult SetStapmTime(uint seconds)
    {
        uint cmd = CurrentFamily switch { RyzenSmuFamily.FP6 => 0x18u, RyzenSmuFamily.FP7_FP8 => 0x18u, _ => 0x53u };
        return Send(cmd, seconds, true, "STAPM Time");
    }

    public CommandResult SetFastLimit(double watts)
    {
        uint cmd = CurrentFamily switch { RyzenSmuFamily.FP6 => 0x15u, RyzenSmuFamily.FP7_FP8 => 0x15u, _ => 0x3Eu };
        return Send(cmd, (uint)(watts * 1000), true, "Fast Limit");
    }

    public CommandResult SetSlowLimit(double watts)
    {
        uint cmd = CurrentFamily switch { RyzenSmuFamily.FP6 => 0x16u, RyzenSmuFamily.FP7_FP8 => 0x16u, _ => 0x5Fu };
        return Send(cmd, (uint)(watts * 1000), true, "Slow Limit");
    }

    public CommandResult SetSlowTime(uint seconds)
    {
        uint cmd = CurrentFamily switch { RyzenSmuFamily.FP6 => 0x17u, RyzenSmuFamily.FP7_FP8 => 0x17u, _ => 0x60u };
        return Send(cmd, seconds, true, "Slow Time");
    }

    public CommandResult SetPptLimitRsmu(double watts)
    {
        uint cmd = CurrentFamily switch { RyzenSmuFamily.FP6 => 0x33u, RyzenSmuFamily.FP7_FP8 => 0x31u, _ => 0x56u };
        return Send(cmd, (uint)(watts * 1000), false, "PPT Limit (RSMU)");
    }
    #endregion

    #region (Current & Temp Limits)
    public CommandResult SetVrmCurrentMp1(uint milliamps)
    {
        uint cmd = CurrentFamily switch { RyzenSmuFamily.FP6 => 0x1Au, RyzenSmuFamily.FP7_FP8 => 0x1Au, _ => 0x3Cu };
        return Send(cmd, milliamps, true, "VRM Current (MP1)");
    }

    public CommandResult SetVrmCurrentRsmu(uint milliamps)
    {
        uint cmd = CurrentFamily switch { RyzenSmuFamily.FP6 => 0x38u, RyzenSmuFamily.FP7_FP8 => 0x38u, _ => 0x57u };
        return Send(cmd, milliamps, false, "VRM Current (RSMU)");
    }

    public CommandResult SetEdcLimitMp1(uint milliamps)
    {
        uint cmd = CurrentFamily switch { RyzenSmuFamily.FP6 => 0x1Cu, RyzenSmuFamily.FP7_FP8 => 0x1Cu, _ => 0x3Du };
        return Send(cmd, milliamps, true, "EDC Limit (MP1)");
    }

    public CommandResult SetEdcLimitRsmu(uint milliamps)
    {
        uint cmd = CurrentFamily switch { RyzenSmuFamily.FP6 => 0x3Au, RyzenSmuFamily.FP7_FP8 => 0x3Au, _ => 0x58u };
        return Send(cmd, milliamps, false, "EDC Limit (RSMU)");
    }

    public CommandResult SetTempLimitMp1(uint celsius)
    {
        uint cmd = CurrentFamily switch { RyzenSmuFamily.FP6 => 0x19u, RyzenSmuFamily.FP7_FP8 => 0x19u, _ => 0x3Fu };
        return Send(cmd, celsius, true, "Temp Limit (MP1)");
    }

    public CommandResult SetTempLimitRsmu(uint celsius)
    {
        uint cmd = CurrentFamily switch { RyzenSmuFamily.FP6 => 0x37u, RyzenSmuFamily.FP7_FP8 => 0x37u, _ => 0x59u };
        return Send(cmd, celsius, false, "Temp Limit (RSMU)");
    }
    #endregion

    #region (PBO & Overclocking)
    public CommandResult SetPboScalar(uint value)
    {
        uint cmd = CurrentFamily switch { RyzenSmuFamily.FP6 => 0x3Fu, RyzenSmuFamily.FP7_FP8 => 0x3Eu, _ => 0x5Bu };
        return Send(cmd, value, false, "PBO Scalar");
    }

    public CommandResult SetOcClk(int mhz)
    {
        uint cmd = CurrentFamily switch { RyzenSmuFamily.FP6 => 0x19u, RyzenSmuFamily.FP7_FP8 => 0x19u, _ => 0x5Fu };
        return Send(cmd, (uint)mhz, false, "OC Clock");
    }

    public CommandResult SetPerCoreOcClk(uint coreIdx, uint mhz)
    {
        uint cmd = CurrentFamily switch { RyzenSmuFamily.FP6 => 0x1Au, RyzenSmuFamily.FP7_FP8 => 0x1Au, _ => 0x60u };
        return Send(cmd, (coreIdx << 8) | (mhz & 0xFF), false, "Per Core OC Clock");
    }

    public CommandResult SetOcVolt(uint millivolts)
    {
        uint cmd = CurrentFamily switch { RyzenSmuFamily.FP6 => 0x1Bu, RyzenSmuFamily.FP7_FP8 => 0x1Bu, _ => 0x61u };
        return Send(cmd, millivolts, false, "OC Voltage");
    }

    public CommandResult EnableOc()
    {
        uint cmd = CurrentFamily switch { RyzenSmuFamily.FP6 => 0x17u, RyzenSmuFamily.FP7_FP8 => 0x17u, _ => 0x5Du };
        return Send(cmd, 0, false, "Enable OC Mode");
    }

    public CommandResult DisableOc()
    {
        uint cmd = CurrentFamily switch { RyzenSmuFamily.FP6 => 0x18u, RyzenSmuFamily.FP7_FP8 => 0x18u, _ => 0x5Eu };
        return Send(cmd, 0, false, "Disable OC Mode");
    }
    #endregion

    #region (Curve Optimizer)
    public CommandResult SetCurveOptimizerAll(int value)
    {
        uint cmd = CurrentFamily switch { RyzenSmuFamily.FP6 => 0xB1u, RyzenSmuFamily.FP7_FP8 => 0x5Du, _ => 0x07u };
        return Send(cmd, (uint)(value & 0xFFFFFFFF), false, "Curve Optimizer All");
    }

    public CommandResult SetCurveOptimizerPerCore(uint coreIdx, int value)
    {
        uint cmd = CurrentFamily switch { RyzenSmuFamily.FP6 => 0x52u, RyzenSmuFamily.FP7_FP8 => 0x53u, _ => 0x06u };
        return Send(cmd, (coreIdx << 8) | (uint)(value & 0xFF), false, $"Curve Optimizer Core {coreIdx}");
    }
    #endregion

    #region (Power Telemetry)
    private static LibreHardwareMonitor.Hardware.Computer? _lhmComputer;
    private static readonly object _lhmLock = new();

    private static LibreHardwareMonitor.Hardware.Computer GetOrCreateLhm()
    {
        if (_lhmComputer != null) return _lhmComputer;
        lock (_lhmLock)
        {
            if (_lhmComputer != null) return _lhmComputer;
            var computer = new LibreHardwareMonitor.Hardware.Computer
            {
                IsCpuEnabled = true,
            };
            computer.Open();
            _lhmComputer = computer;
            return computer;
        }
    }

    public CommandResult GetSmuTelemetry()
    {
        try
        {
            double ppt = 0;
            double tdc = 0;
            double edc = 0;
            double temp = 0;
            double freq = 0;
            int usage = 0;
            try
            {
                var computer = GetOrCreateLhm();

                foreach (var hardware in computer.Hardware)
                {
                    if (hardware.HardwareType != LibreHardwareMonitor.Hardware.HardwareType.Cpu)
                        continue;

                    hardware.Update();

                    foreach (var sensor in hardware.Sensors)
                    {
                        if (sensor.Value == null) continue;
                        float val = sensor.Value.Value;

                        switch (sensor.SensorType)
                        {
                            case LibreHardwareMonitor.Hardware.SensorType.Power:
                                // "Package" = total CPU package power (PPT equivalent)
                                if (sensor.Name.Contains("Package") && ppt == 0)
                                    ppt = Math.Round(val, 1);
                                // "Core" power for TDC estimation
                                if (sensor.Name.Contains("Core") && tdc == 0)
                                    tdc = Math.Round(val, 1);
                                break;

                            case LibreHardwareMonitor.Hardware.SensorType.Temperature:
                                // "Core Max" or "Tctl/Tdie" is the canonical Ryzen temp
                                if ((sensor.Name.Contains("Core") && sensor.Name.Contains("Max")) ||
                                     sensor.Name.Contains("Tctl") || sensor.Name.Contains("Tdie"))
                                {
                                    if (val > temp) temp = Math.Round(val, 1);
                                }
                                break;

                            case LibreHardwareMonitor.Hardware.SensorType.Frequency:
                                if (sensor.Name.Contains("Core #1") || sensor.Name.Contains("Bus Speed"))
                                    freq = Math.Round(val, 0);
                                break;

                            case LibreHardwareMonitor.Hardware.SensorType.Load:
                                if (sensor.Name.Contains("Total"))
                                    usage = (int)Math.Round(val);
                                break;
                        }
                    }

                    // EDC ~ TDC × 1.3 (AMD Ryzen peak current headroom)
                    if (tdc > 0)
                        edc = Math.Round(tdc * 1.3, 1);

                    break; // Only process first CPU
                }
            }
            catch (Exception lhmEx)
            {
                // LHM failed — fallback to WMI for temp/freq/usage
                try
                {
                    using var searcher = new System.Management.ManagementObjectSearcher(
                        @"root\WMI", "SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature");
                    double maxTemp = 0;
                    foreach (System.Management.ManagementObject obj in searcher.Get())
                    {
                        uint raw = Convert.ToUInt32(obj["CurrentTemperature"]);
                        double t = (raw - 2732) / 10.0;
                        if (t > maxTemp) maxTemp = t;
                    }
                    temp = Math.Round(maxTemp, 1);
                }
                catch { }

                try
                {
                    using var freqCounter = new System.Diagnostics.PerformanceCounter(
                        "Processor Information", "% Processor Performance", "_Total");
                    freqCounter.NextValue();
                    System.Threading.Thread.Sleep(100);
                    float perfPct = freqCounter.NextValue();
                    using var wmi = new System.Management.ManagementObjectSearcher(
                        "SELECT MaxClockSpeed FROM Win32_Processor");
                    foreach (System.Management.ManagementObject obj in wmi.Get())
                    {
                        freq = Math.Round(perfPct / 100.0 * Convert.ToUInt32(obj["MaxClockSpeed"]), 0);
                        break;
                    }
                }
                catch { }

                try
                {
                    using var usageCounter = new System.Diagnostics.PerformanceCounter(
                        "Processor", "% Processor Time", "_Total");
                    usageCounter.NextValue();
                    System.Threading.Thread.Sleep(100);
                    usage = (int)Math.Round(usageCounter.NextValue());
                }
                catch { }
            }

            return new CommandResult(true, "获取成功", new
            {
                Ppt = ppt,
                Tdc = tdc,
                Edc = edc,
                Temp = temp,
                FreqMhz = (int)freq,
                Usage = usage,
            });
        }
        catch (Exception ex)
        {
            return new CommandResult(false, $"遥测读取失败: {ex.Message}");
        }
    }
    #endregion
}