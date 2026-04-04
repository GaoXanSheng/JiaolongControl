using System.Runtime.InteropServices;
using JiaoLongControl.Server.Core.Drivers;
using JiaoLongControl.Server.Core.Models;
using JiaoLongControl.Server.Core.Services;
using JiaoLongControl.Server.Core.Utils;

namespace JiaoLongControl.Server.Core.Controllers;

[ComVisible(true)]
[ClassInterface(ClassInterfaceType.AutoDual)]
public class FanController : Blding64
{
    public CommandResult GetFanSpeed()
    {
        Tuple<int, int> CPUGPUFanSpeed = MethodServices.GetValue<Tuple<int, int>>(MethodName.CPUGPUFanSpeed);
        var fanSpeedInfo = new FanSpeedInfo
        {
            CPUFanSpeed = CPUGPUFanSpeed.Item1,
            GPUFanSpeed = CPUGPUFanSpeed.Item2
        };
        return new CommandResult(true, "获取成功", fanSpeedInfo);
    }

    public CommandResult SetFanSpeed(byte fanSpeed)
    {
        // ACPI表的风扇调速比EC的风扇调速优先级更高，所以如果开启了ACPI表的风扇调速，就无法通过EC来设置风扇速度，因此需要先关闭ACPI表的风扇调速开关
        if (GetMaxFanSpeedSwitch().Success)
        {
            SetMaxFanSpeedSwitch(false);
        }

        if (IsInitialized)
        {
            GpuFanSetSpeed(fanSpeed);
            CpuFanSetSpeed(fanSpeed);
            return new CommandResult(true, "设置成功");
        }

        return new CommandResult(false, "设置失败");
    }

    public CommandResult RemoveFanSpeed()
    {
        if (IsInitialized)
        {
            RemoveFanSpeed();
            return new CommandResult(true, "设置成功");
        }

        return new CommandResult(false, "设置失败");
    }

    [Obsolete("不推荐使用SetMaxFanSpeedSwitch")]
    public bool SetMaxFanSpeedSwitch(bool maxFanSpeedSwitch)
    {
        return MethodServices.SetValue(MethodName.MaxFanSpeedSwitch, (byte)(maxFanSpeedSwitch ? 1 : 0));
    }

    public CommandResult GetMaxFanSpeedSwitch()
    {
        var res = MethodServices.GetValue<byte>(MethodName.MaxFanSpeedSwitch) == 1;
        return new CommandResult(res, "获取成功");
    }
}