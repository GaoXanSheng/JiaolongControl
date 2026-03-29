using System.Runtime.InteropServices;
using JiaoLongControl.Server.Core.Models;
using JiaoLongControl.Server.Core.Services;
using JiaoLongControl.Server.Core.Utils;

namespace JiaoLongControl.Server.Core.Controllers
{
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.AutoDual)]
    public class PerformanceModeController
    {
        public CommandResult Get()
        {
            return new CommandResult(true, "获取成功",
                MethodServices.GetValue<SystemPerMode>(MethodName.SystemPerMode));
        }

        public CommandResult Set(SystemPerMode mode)
        {
            var res = MethodServices.SetValue(MethodName.SystemPerMode, mode);
            return new CommandResult(res, res ? "设置成功" : "设置失败");
        }
    }
}