using System.Management;
using System.Runtime.InteropServices;
using JiaoLongControl.Server.Core.Models;
using JiaoLongControl.Server.Core.Services;
using JiaoLongControl.Server.Core.Utils;

namespace JiaoLongControl.Server.Core.Controllers
{
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.AutoDual)]
    public class CpuController
    {
        public CommandResult SetCpuShortPower(byte sp)
        {
            var res = MethodServices.SetValue(MethodName.CPUPower, new byte[2]
            {
                (byte)CPUPower.ShortPower,
                sp
            });
            return new CommandResult(res, res ? "设置成功" : "设置失败");
        }

        public CommandResult SetCpuLongPower(byte lp)
        {
            var res = MethodServices.SetValue(MethodName.CPUPower, new byte[2]
            {
                (byte)CPUPower.LongPower,
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

        public CommandResult SetCPUTempWall(byte tw)
        {
            var res = MethodServices.SetValue(MethodName.CPUPower, new byte[2]
            {
                (byte)CPUPower.CpuTempWallState,
                tw
            });
            return new CommandResult(res, res ? "设置成功" : "设置失败");
        }

        public CommandResult GetCustomMode()
        {
            var res = MethodServices.GetValue<CPUPower>(MethodName.CPUPower);
            return new CommandResult(res == CPUPower.OpenState, res == CPUPower.OpenState ? "已开启" : "已关闭");
        }

        public CommandResult GetCpuUsage()
        {
            var cpu = HardwareMonitorService.ReadCpu();
            return new CommandResult(true, "获取成功", cpu.Usage);
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
            var cpu = HardwareMonitorService.ReadCpu();
            return new CommandResult(true, "获取成功", cpu.FrequencyMhz);
        }

        public CommandResult GetCpuVoltage()
        {
            var cpu = HardwareMonitorService.ReadCpu();
            if (cpu.Voltage <= 0)
                return new CommandResult(false, "CPU 电压不可用");
            return new CommandResult(true, "获取成功", Math.Round(cpu.Voltage, 3));
        }

        public CommandResult GetCPUThermometer()
        {
            var res = MethodServices.GetValue<byte>(MethodName.CPUThermometer);
            return new CommandResult(true, $"读取成功", res);
        }

        public CommandResult GetPhysicalCoreCount()
        {
            try
            {
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
    }
}
