using System.Runtime.InteropServices;
using Microsoft.Win32;
using JiaoLongControl.Server.Core.Drivers;
using JiaoLongControl.Server.Core.Utils;

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
    public RyzenSmuFamily CurrentFamily { get; set; } = RyzenSmuFamily.AM5_V1;

    public RyzenSmuController()
    {
        try
        {
            string cpuName = GetCpuNameFast();
            if (string.IsNullOrWhiteSpace(cpuName))
            {
                using var searcher = new System.Management.ManagementObjectSearcher("SELECT Name FROM Win32_Processor");
                foreach (System.Management.ManagementObject obj in searcher.Get())
                {
                    cpuName = obj["Name"]?.ToString() ?? "";
                    break;
                }
            }

            if (cpuName.Contains("7945") || cpuName.Contains("7845") || cpuName.Contains("7745"))
                CurrentFamily = RyzenSmuFamily.AM5_V1;
            else if (cpuName.Contains("HX 370") || cpuName.Contains("AI 9") || cpuName.Contains("AI 7") || cpuName.Contains("365") || cpuName.Contains("370") || cpuName.Contains("Strix"))
                CurrentFamily = RyzenSmuFamily.FP7_FP8_Strix;
            else if (cpuName.Contains("7735") || cpuName.Contains("6800") || cpuName.Contains("6900") || cpuName.Contains("7840") || cpuName.Contains("7940") || cpuName.Contains("8840") || cpuName.Contains("8845"))
                CurrentFamily = RyzenSmuFamily.FP7_FP8;
            else if (cpuName.Contains("5800") || cpuName.Contains("5900") || cpuName.Contains("5600") || cpuName.Contains("4800") || cpuName.Contains("4600"))
                CurrentFamily = RyzenSmuFamily.FP6;
            else
                CurrentFamily = RyzenSmuFamily.AM5_V1;
        }
        catch { }
    }

    private static string GetCpuNameFast()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            return key?.GetValue("ProcessorNameString")?.ToString() ?? "";
        }
        catch
        {
            return "";
        }
    }

    private CommandResult Send(uint cmd, uint arg, bool isMp1, string name)
    {
        uint addrMsg, addrRsp, addrArg;

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
            case RyzenSmuFamily.FP7_FP8_Strix:
                addrMsg = isMp1 ? 0x3B10928u : 0x03B10A20u;
                addrRsp = isMp1 ? 0x3B10978u : 0x03B10A80u;
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
            
            // 写入主要参数
            Execute("ioctl_write_smu_register", new ulong[] { addrArg, arg }, 0);
            // 清空其余 5 个参数槽，确保无脏数据
            for (uint i = 1; i < 6; i++)
            {
                Execute("ioctl_write_smu_register", new ulong[] { addrArg + (i * 4), 0 }, 0);
            }

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

    private CommandResult TrySend(uint arg, string name, params (uint cmd, bool isMp1)[] commands)
    {
        CommandResult? lastResult = null;
        foreach (var (cmd, isMp1) in commands)
        {
            lastResult = Send(cmd, arg, isMp1, name);
            if (lastResult.Success)
            {
                return lastResult;
            }
        }
        return lastResult ?? new CommandResult(false, $"{name} 设置失败: 无可用指令");
    }

    #region (Power Limits - PPT)
    public CommandResult SetStapmLimit(double watts)
    {
        uint arg = (uint)(watts * 1000);
        return CurrentFamily switch {
            RyzenSmuFamily.FP6 => TrySend(arg, "STAPM Limit", (0x14, true), (0x31, false)),
            RyzenSmuFamily.FP7_FP8 => TrySend(arg, "STAPM Limit", (0x14, true), (0x31, false)),
            RyzenSmuFamily.FP7_FP8_Strix => TrySend(arg, "STAPM Limit", (0x14, true), (0x31, false)),
            _ => TrySend(arg, "STAPM Limit", (0x4F, true))
        };
    }

    public CommandResult SetStapmTime(uint seconds)
    {
        return CurrentFamily switch {
            RyzenSmuFamily.FP6 => TrySend(seconds, "STAPM Time", (0x18, true), (0x36, false)),
            RyzenSmuFamily.FP7_FP8 => TrySend(seconds, "STAPM Time", (0x18, true), (0x36, false)),
            RyzenSmuFamily.FP7_FP8_Strix => TrySend(seconds, "STAPM Time", (0x18, true), (0x36, false)),
            _ => TrySend(seconds, "STAPM Time", (0x53, true))
        };
    }

    public CommandResult SetFastLimit(double watts)
    {
        uint arg = (uint)(watts * 1000);
        return CurrentFamily switch {
            RyzenSmuFamily.FP6 => TrySend(arg, "Fast Limit", (0x15, true), (0x32, false)),
            RyzenSmuFamily.FP7_FP8 => TrySend(arg, "Fast Limit", (0x15, true), (0x32, false)),
            RyzenSmuFamily.FP7_FP8_Strix => TrySend(arg, "Fast Limit", (0x15, true), (0x32, false)),
            _ => TrySend(arg, "Fast Limit", (0x3E, true))
        };
    }

    public CommandResult SetSlowLimit(double watts)
    {
        uint arg = (uint)(watts * 1000);
        return CurrentFamily switch {
            RyzenSmuFamily.FP6 => TrySend(arg, "Slow Limit", (0x16, true), (0x33, false)),
            RyzenSmuFamily.FP7_FP8 => TrySend(arg, "Slow Limit", (0x16, true), (0x33, false)),
            RyzenSmuFamily.FP7_FP8_Strix => TrySend(arg, "Slow Limit", (0x16, true), (0x33, false)),
            _ => TrySend(arg, "Slow Limit", (0x5F, true), (0xCB, false))
        };
    }

    public CommandResult SetSlowTime(uint seconds)
    {
        return CurrentFamily switch {
            RyzenSmuFamily.FP6 => TrySend(seconds, "Slow Time", (0x17, true), (0x35, false)),
            RyzenSmuFamily.FP7_FP8 => TrySend(seconds, "Slow Time", (0x17, true), (0x35, false)),
            RyzenSmuFamily.FP7_FP8_Strix => TrySend(seconds, "Slow Time", (0x17, true), (0x35, false)),
            _ => TrySend(seconds, "Slow Time", (0x60, true))
        };
    }

    public CommandResult SetPptLimitRsmu(double watts)
    {
        uint cmd = CurrentFamily switch { 
            RyzenSmuFamily.FP6 => 0x33u, 
            RyzenSmuFamily.FP7_FP8 => 0x31u, 
            RyzenSmuFamily.FP7_FP8_Strix => 0x31u, 
            _ => 0x56u 
        };
        return Send(cmd, (uint)(watts * 1000), false, "PPT Limit (RSMU)");
    }
    #endregion

    #region (Current & Temp Limits)
    public CommandResult SetVrmCurrentMp1(uint milliamps)
    {
        uint cmd = CurrentFamily switch { 
            RyzenSmuFamily.FP6 => 0x1Au, 
            RyzenSmuFamily.FP7_FP8 => 0x1Au, 
            RyzenSmuFamily.FP7_FP8_Strix => 0x1Au, 
            _ => 0x3Cu 
        };
        return Send(cmd, milliamps, true, "VRM Current (MP1)");
    }

    public CommandResult SetVrmCurrentRsmu(uint milliamps)
    {
        uint cmd = CurrentFamily switch { 
            RyzenSmuFamily.FP6 => 0x38u, 
            RyzenSmuFamily.FP7_FP8 => 0x38u, 
            RyzenSmuFamily.FP7_FP8_Strix => 0x38u, 
            _ => 0x57u 
        };
        return Send(cmd, milliamps, false, "VRM Current (RSMU)");
    }

    public CommandResult SetEdcLimitMp1(uint milliamps)
    {
        uint cmd = CurrentFamily switch { 
            RyzenSmuFamily.FP6 => 0x1Cu, 
            RyzenSmuFamily.FP7_FP8 => 0x1Cu, 
            RyzenSmuFamily.FP7_FP8_Strix => 0x1Cu, 
            _ => 0x3Du 
        };
        return Send(cmd, milliamps, true, "EDC Limit (MP1)");
    }

    public CommandResult SetEdcLimitRsmu(uint milliamps)
    {
        uint cmd = CurrentFamily switch { 
            RyzenSmuFamily.FP6 => 0x3Au, 
            RyzenSmuFamily.FP7_FP8 => 0x3Au, 
            RyzenSmuFamily.FP7_FP8_Strix => 0x3Au, 
            _ => 0x58u 
        };
        return Send(cmd, milliamps, false, "EDC Limit (RSMU)");
    }

    public CommandResult SetTempLimitMp1(uint celsius)
    {
        uint cmd = CurrentFamily switch { 
            RyzenSmuFamily.FP6 => 0x19u, 
            RyzenSmuFamily.FP7_FP8 => 0x19u, 
            RyzenSmuFamily.FP7_FP8_Strix => 0x19u, 
            _ => 0x3Fu 
        };
        return Send(cmd, celsius, true, "Temp Limit (MP1)");
    }

    public CommandResult SetTempLimitRsmu(uint celsius)
    {
        uint cmd = CurrentFamily switch { 
            RyzenSmuFamily.FP6 => 0x37u, 
            RyzenSmuFamily.FP7_FP8 => 0x37u, 
            RyzenSmuFamily.FP7_FP8_Strix => 0x37u, 
            _ => 0x59u 
        };
        return Send(cmd, celsius, false, "Temp Limit (RSMU)");
    }
    #endregion

    #region (PBO & Overclocking)
    public CommandResult SetPboScalar(uint value)
    {
        uint cmd = CurrentFamily switch { 
            RyzenSmuFamily.FP6 => 0x3Fu, 
            RyzenSmuFamily.FP7_FP8 => 0x3Eu, 
            RyzenSmuFamily.FP7_FP8_Strix => 0x3Eu, 
            _ => 0x5Bu 
        };
        return Send(cmd, value, false, "PBO Scalar");
    }

    public CommandResult SetOcClk(int mhz)
    {
        uint cmd = CurrentFamily switch { 
            RyzenSmuFamily.FP6 => 0x19u, 
            RyzenSmuFamily.FP7_FP8 => 0x19u, 
            RyzenSmuFamily.FP7_FP8_Strix => 0x19u, 
            _ => 0x5Fu 
        };
        return Send(cmd, (uint)mhz, false, "OC Clock");
    }

    public CommandResult SetPerCoreOcClk(uint coreIdx, uint mhz)
    {
        uint cmd = CurrentFamily switch { 
            RyzenSmuFamily.FP6 => 0x1Au, 
            RyzenSmuFamily.FP7_FP8 => 0x1Au, 
            RyzenSmuFamily.FP7_FP8_Strix => 0x1Au, 
            _ => 0x60u 
        };
        return Send(cmd, (coreIdx << 8) | (mhz & 0xFF), false, "Per Core OC Clock");
    }

    public CommandResult SetOcVolt(uint millivolts)
    {
        uint cmd = CurrentFamily switch { 
            RyzenSmuFamily.FP6 => 0x1Bu, 
            RyzenSmuFamily.FP7_FP8 => 0x1Bu, 
            RyzenSmuFamily.FP7_FP8_Strix => 0x1Bu, 
            _ => 0x61u 
        };
        return Send(cmd, millivolts, false, "OC Voltage");
    }

    public CommandResult EnableOc()
    {
        uint cmd = CurrentFamily switch { 
            RyzenSmuFamily.FP6 => 0x17u, 
            RyzenSmuFamily.FP7_FP8 => 0x17u, 
            RyzenSmuFamily.FP7_FP8_Strix => 0x17u, 
            _ => 0x5Du 
        };
        return Send(cmd, 0, false, "Enable OC Mode");
    }

    public CommandResult DisableOc()
    {
        uint cmd = CurrentFamily switch { 
            RyzenSmuFamily.FP6 => 0x18u, 
            RyzenSmuFamily.FP7_FP8 => 0x18u, 
            RyzenSmuFamily.FP7_FP8_Strix => 0x18u, 
            _ => 0x5Eu 
        };
        return Send(cmd, 0, false, "Disable OC Mode");
    }
    #endregion

    #region (Curve Optimizer)
    public CommandResult SetCurveOptimizerAll(int value)
    {
        uint arg = (uint)value & 0xFFFFFu;
        return CurrentFamily switch {
            RyzenSmuFamily.FP6 => TrySend(arg, "Curve Optimizer All", (0x55, true), (0xB1, false)),
            RyzenSmuFamily.FP7_FP8 => TrySend(arg, "Curve Optimizer All", (0x4C, true), (0x5D, false)),
            RyzenSmuFamily.FP7_FP8_Strix => TrySend(arg, "Curve Optimizer All", (0x4C, true), (0x5D, false)),
            _ => TrySend(arg, "Curve Optimizer All", (0x36, true), (0x07, false))
        };
    }

    public CommandResult SetCurveOptimizerPerCore(uint coreIdx, int value)
    {
        uint coValue = (uint)value & 0xFFFFFu;
        uint arg = (coreIdx << 20) | coValue;
        return CurrentFamily switch {
            RyzenSmuFamily.FP6 => TrySend(arg, $"Curve Optimizer Core {coreIdx}", (0x54, true), (0x52, false)),
            RyzenSmuFamily.FP7_FP8 => TrySend(arg, $"Curve Optimizer Core {coreIdx}", (0x4B, true), (0x53, false)),
            RyzenSmuFamily.FP7_FP8_Strix => TrySend(arg, $"Curve Optimizer Core {coreIdx}", (0x4B, true), (0x53, false)),
            _ => TrySend(arg, $"Curve Optimizer Core {coreIdx}", (0x35, true), (0x06, false))
        };
    }
    #endregion

    #region (Power Telemetry)
    private static LibreHardwareMonitor.Hardware.Computer? _lhmComputer;
    private static readonly object _lhmLock = new();
    
    private const uint MsrFidvidStatus = 0xC0010293;

    public double? GetCoreVoltage()
    {
        try
        {
            IntPtr thread = Native.Kernel32.GetCurrentThread();
            IntPtr originalMask = Native.Kernel32.SetThreadAffinityMask(thread, new IntPtr(unchecked((long)-1)));
            if (originalMask == IntPtr.Zero)
                return ReadVidVoltage();

            try
            {
                double? minVolts = null;
                int coreCount = Environment.ProcessorCount;
                for (int i = 0; i < coreCount; i++)
                {
                    IntPtr prev = Native.Kernel32.SetThreadAffinityMask(thread, new IntPtr(1L << i));
                    if (prev == IntPtr.Zero)
                        continue; // 进程亲和性不允许该核心

                    double? volts = ReadVidVoltage();
                    if (volts.HasValue && (minVolts == null || volts.Value < minVolts.Value))
                        minVolts = volts.Value;
                }

                return minVolts;
            }
            finally
            {
                Native.Kernel32.SetThreadAffinityMask(thread, originalMask);
            }
        }
        catch
        {
            return null;
        }
    }

    private double? ReadVidVoltage()
    {
        try
        {
            ulong raw = ReadMsr(MsrFidvidStatus);
            uint vid = (uint)((raw >> 6) & 0xFF);
            double volts = 1.550 - vid * 0.00625;
            return volts is >= 0.4 and <= 1.8 ? volts : null;
        }
        catch
        {
            return null;
        }
    }

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
                                if (sensor.Name.Contains("Package") && ppt == 0)
                                    ppt = Math.Round(val, 1);
                                if (sensor.Name.Contains("Core") && tdc == 0)
                                    tdc = Math.Round(val, 1);
                                break;

                            case LibreHardwareMonitor.Hardware.SensorType.Temperature:
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

                    if (tdc > 0)
                        edc = Math.Round(tdc * 1.3, 1);

                    break;
                }
            }
            catch (Exception lhmEx)
            {
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