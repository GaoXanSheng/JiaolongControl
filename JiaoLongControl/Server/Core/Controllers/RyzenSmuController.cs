using System.Runtime.InteropServices;
using JiaoLongControl.Server.Core.Drivers;
using JiaoLongControl.Server.Core.Native;
using JiaoLongControl.Server.Core.Utils;

namespace JiaoLongControl.Server.Core.Controllers;

[ComVisible(true)]
[ClassInterface(ClassInterfaceType.AutoDual)]
public class RyzenSmuController : PawnIO 
{
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

    #region (Power Telemetry)

    // Lazy-initialized LHM computer instance — shared across calls to avoid re-init cost
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

            // --- Use LibreHardwareMonitor for accurate AMD Ryzen sensor readings ---
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