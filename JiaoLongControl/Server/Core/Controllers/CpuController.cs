using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using JiaoLongControl.Server.Core.Models;
using JiaoLongControl.Server.Core.Services;
using JiaoLongControl.Server.Core.Utils;

namespace JiaoLongControl.Server.Core.Controllers
{
    public struct CpuStatsInfo
    {
        public int Temperature { get; set; }
        public int Usage { get; set; }
        public int FrequencyMhz { get; set; }
        public double Voltage { get; set; }
        public int PowerWatts { get; set; } // Set to 0 if unsupported
    }

    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.AutoDual)]
    public class CpuController : IDisposable
    {
        private readonly PerformanceCounter _cpuCounter;
        private readonly PerformanceCounter _cpuFreqCounter;

        public CpuController()
        {
            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            _cpuCounter.NextValue();
            _cpuFreqCounter = new PerformanceCounter("Processor Information", "% Processor Performance", "_Total");
            _cpuFreqCounter.NextValue();
        }

        public CommandResult SetCpuShortPower(byte sp)
        {
            var res = MethodServices.SetValue(MethodName.CPUPower, new byte[2]
            {
                (byte)CPUPower.SPLState,
                sp
            });
            return new CommandResult(res, res ? "设置成功" : "设置失败");
        }

        public CommandResult SetCpuLongPower(byte lp)
        {
            var res = MethodServices.SetValue(MethodName.CPUPower, new byte[2]
            {
                (byte)CPUPower.SPPTState,
                lp
            });
            return new CommandResult(res, res ? "设置成功" : "设置失败");
        }

        public CommandResult SetCustomMode(bool open)
        {
            var res = false;
            if (open)
            {
                res = MethodServices.SetValue(MethodName.CPUPower, CPUPower.OpenState);
            }
            else
            {
                res = MethodServices.SetValue(MethodName.CPUPower, CPUPower.CloseState);
            }
            return new CommandResult(res, res ? "设置成功" : "设置失败");
        }

        public CommandResult GetCustomMode()
        {
            var res = MethodServices.GetValue<CPUPower>(MethodName.CPUPower);
            return new CommandResult(res == CPUPower.OpenState, res == CPUPower.OpenState ? "已开启" : "已关闭");
        }

        public CommandResult GetCpuUsage()
        {
            try
            {
                return new CommandResult(true, "获取成功", (int)_cpuCounter.NextValue());
            }
            catch (Exception ex)
            {
                return new CommandResult(false, ex.Message);
            }
        }

        public CommandResult GetCpuInfo()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT Name, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed FROM Win32_Processor");
                foreach (var obj in searcher.Get())
                {
                    return new CommandResult(true, "获取成功", new
                    {
                        Name = obj["Name"]?.ToString()?.Trim() ?? "Unknown",
                        Cores = Convert.ToInt32(obj["NumberOfCores"]),
                        Threads = Convert.ToInt32(obj["NumberOfLogicalProcessors"]),
                        BaseFreqMhz = Convert.ToInt32(obj["MaxClockSpeed"])
                    });
                }
                return new CommandResult(false, "无法获取 CPU 信息");
            }
            catch (Exception ex)
            {
                return new CommandResult(false, ex.Message);
            }
        }

        public CommandResult GetCpuFrequency()
        {
            try
            {
                // % Processor Performance 相对于基础频率的百分比
                float perfPercent = _cpuFreqCounter.NextValue();
                int freqMhz = (int)(perfPercent / 100 * GetBaseFrequency());
                return new CommandResult(true, "获取成功", freqMhz);
            }
            catch (Exception ex)
            {
                return new CommandResult(false, ex.Message);
            }
        }

        public CommandResult GetCpuVoltage()
        {
            try
            {
                // 优先使用 LibreHardwareMonitor 读取实时电压
                var voltage = ReadCpuVoltageFromLhm();
                if (voltage.HasValue)
                    return new CommandResult(true, "获取成功", Math.Round(voltage.Value, 3));

                // 回退到 WMI Win32_Processor.CurrentVoltage
                using var searcher = new ManagementObjectSearcher("SELECT CurrentVoltage FROM Win32_Processor");
                foreach (var obj in searcher.Get())
                {
                    ushort raw = Convert.ToUInt16(obj["CurrentVoltage"]);
                    if (raw > 0)
                    {
                        double volts = (raw & 0xFF) / 10.0;
                        return new CommandResult(true, "获取成功", Math.Round(volts, 3));
                    }
                }
                return new CommandResult(false, "CPU 电压不可用");
            }
            catch (Exception ex)
            {
                return new CommandResult(false, ex.Message);
            }
        }

        private static LibreHardwareMonitor.Hardware.Computer? _lhmComputer;
        private static readonly object _lhmLock = new();

        private static double? ReadCpuVoltageFromLhm()
        {
            try
            {
                if (_lhmComputer == null)
                {
                    lock (_lhmLock)
                    {
                        if (_lhmComputer == null)
                        {
                            var computer = new LibreHardwareMonitor.Hardware.Computer
                            {
                                IsCpuEnabled = true,
                            };
                            computer.Open();
                            _lhmComputer = computer;
                        }
                    }
                }

                foreach (var hardware in _lhmComputer.Hardware)
                {
                    if (hardware.HardwareType != LibreHardwareMonitor.Hardware.HardwareType.Cpu)
                        continue;

                    hardware.Update();

                    foreach (var sensor in hardware.Sensors)
                    {
                        if (sensor.SensorType == LibreHardwareMonitor.Hardware.SensorType.Voltage &&
                            sensor.Value.HasValue &&
                            (sensor.Name.Contains("Vcore") || sensor.Name.Contains("Core") && sensor.Name.Contains("VID")))
                        {
                            return sensor.Value.Value;
                        }
                    }

                    // 如果没找到 Vcore，取第一个电压传感器
                    foreach (var sensor in hardware.Sensors)
                    {
                        if (sensor.SensorType == LibreHardwareMonitor.Hardware.SensorType.Voltage &&
                            sensor.Value.HasValue)
                        {
                            return sensor.Value.Value;
                        }
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private uint GetBaseFrequency()
        {
            using var searcher = new ManagementObjectSearcher("SELECT MaxClockSpeed FROM Win32_Processor");
            foreach (var obj in searcher.Get())
                return Convert.ToUInt32(obj["MaxClockSpeed"]);
            return 3000;
        }

        public CommandResult GetCPUThermometer()
        {
            var res = MethodServices.GetValue<byte>(MethodName.CPUThermometer);
            return new CommandResult(true, $"读取成功", res);
        }

        public CommandResult GetCpuAllStats()
        {
            try
            {
                var stats = new CpuStatsInfo();
                
                // Temp
                stats.Temperature = MethodServices.GetValue<byte>(MethodName.CPUThermometer);
                
                // Usage
                stats.Usage = (int)_cpuCounter.NextValue();
                
                // Frequency
                float perfPercent = _cpuFreqCounter.NextValue();
                stats.FrequencyMhz = (int)(perfPercent / 100 * GetBaseFrequency());
                
                // Voltage
                var voltage = ReadCpuVoltageFromLhm();
                if (voltage.HasValue)
                {
                    stats.Voltage = Math.Round(voltage.Value, 3);
                }
                else
                {
                    using var searcher2 = new ManagementObjectSearcher("SELECT CurrentVoltage FROM Win32_Processor");
                    foreach (var obj2 in searcher2.Get())
                    {
                        ushort raw2 = Convert.ToUInt16(obj2["CurrentVoltage"]);
                        if (raw2 > 0)
                        {
                            stats.Voltage = Math.Round((raw2 & 0xFF) / 10.0, 3);
                            break;
                        }
                    }
                }
                
                return new CommandResult(true, "获取成功", stats);
            }
            catch (Exception ex)
            {
                return new CommandResult(false, ex.Message);
            }
        }

        public CommandResult SetCPUTempWall(byte tw)
        {
            var res = MethodServices.SetValue(MethodName.CPUPower, new byte[2]
            {
                (byte)CPUPower.CpuTempWallState,
                tw
            });
            return new CommandResult(res, res ? "设置成功" : "设置失败");
        }

        public CommandResult GetPhysicalCoreCount()
        {
            try
            {
                // NumberOfCores = physical cores (no hyperthreading)
                // NumberOfLogicalProcessors = logical cores (with HT/SMT)
                using var searcher = new ManagementObjectSearcher(
                    "SELECT NumberOfCores FROM Win32_Processor");
                int total = 0;
                foreach (var obj in searcher.Get())
                    total += Convert.ToInt32(obj["NumberOfCores"]);
                return new CommandResult(true, "获取成功", total);
            }
            catch (Exception ex)
            {
                return new CommandResult(false, ex.Message);
            }
        }

        public void Dispose()
        {
            _cpuCounter.Dispose();
            _cpuFreqCounter.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
