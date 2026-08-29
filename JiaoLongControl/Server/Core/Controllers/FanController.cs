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
            CPUFanSpeed = CPUGPUFanSpeed.Item2,
            GPUFanSpeed = CPUGPUFanSpeed.Item1
        };
        return new CommandResult(true, "获取成功", fanSpeedInfo);
    }

    public CommandResult SetFanSpeed(byte fanSpeed)
    {
        if ((bool)GetMaxFanSpeedSwitch().Data)
        {
            // 此处为兼容性设置，为避免官方控制台冲突设计
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
            base.RemoveFanSpeed();
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
        return new CommandResult(res, "获取成功",res);
    }
}