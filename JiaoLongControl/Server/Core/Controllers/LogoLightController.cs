using System.Runtime.InteropServices;
using JiaoLongControl.Server.Core.Models;
using JiaoLongControl.Server.Core.Services;
using JiaoLongControl.Server.Core.Utils;

namespace JiaoLongControl.Server.Core.Controllers
{
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.AutoDual)]
    public class LogoLightController
    {
        public CommandResult Get()
        {
            var res =  MethodServices.GetValue<byte>(MethodName.Ambientlight) == (byte)ResultState.ON;
            return new CommandResult(res, res ? "LOGO灯打开状态" : "LOGO灯关闭状态");
        }

        public CommandResult Set(ResultState state)
        {
            var res =  MethodServices.SetValue(MethodName.Ambientlight, state);
            return new CommandResult(res, res ? "设置成功" : "设置失败");
        }
    }
}