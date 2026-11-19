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
        private CommandResult SetCpuPowerItem(CPUPower kind, byte value)
        {
            var res = MethodServices.SetValue(MethodName.CPUPower, new byte[2] { (byte)kind, value });
            return new CommandResult(res, res ? "设置成功" : "设置失败");
        }

        public CommandResult SetCpuShortPower(byte sp) => SetCpuPowerItem(CPUPower.ShortPower, sp);

        public CommandResult SetCpuLongPower(byte lp) => SetCpuPowerItem(CPUPower.LongPower, lp);

        public CommandResult SetCPUTempWall(byte tw) => SetCpuPowerItem(CPUPower.CpuTempWallState, tw);

        public CommandResult GetCpuUsage()
        {
            var cpu = HardwareMonitorService.ReadCpu();
            return new CommandResult(true, "获取成功", cpu.Usage);
        }

        public CommandResult GetCpuInfo()
        {
            var cpu = HardwareMonitorService.ReadCpuInfo();
            return new CommandResult(true, "获取成功", new
            {
                Name = string.IsNullOrEmpty(cpu.Name) ? "Unknown" : cpu.Name,
                Cores = cpu.Cores,
                Threads = cpu.Threads,
                BaseFreqMhz = cpu.BaseFrequencyMhz
            });
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
            var cpu = HardwareMonitorService.ReadCpuInfo();
            return new CommandResult(true, "获取成功", cpu.Cores);
        }
    }
}
