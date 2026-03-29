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
            var res =  MethodServices.SetValue(MethodName.CPUPower, new byte[2]
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
                res =  MethodServices.SetValue(MethodName.CPUPower, CPUPower.OpenState);
            }
            else
            {
                res =  MethodServices.SetValue(MethodName.CPUPower, CPUPower.CloseState);
            }
            return new CommandResult(res, res ? "设置成功" : "设置失败");
        }

        public CommandResult GetCustomMode()
        {
            var res = MethodServices.GetValue<CPUPower>(MethodName.CPUPower);
            return new CommandResult(res == CPUPower.OpenState, res == CPUPower.OpenState ? "已开启" : "已关闭");
        }

        public CommandResult GetCPUThermometer()
        {
            var res =  MethodServices.GetValue<byte>(MethodName.CPUThermometer);
            return new CommandResult(true, $"读取成功", res);
        }

        public CommandResult SetCPUTempWall(byte tw)
        {
            var res=  MethodServices.SetValue(MethodName.CPUPower, new byte[2]
            {
                (byte)CPUPower.CpuTempWallState,
                tw
            });
            return new CommandResult(res, res ? "设置成功" : "设置失败");
        }
    }
}