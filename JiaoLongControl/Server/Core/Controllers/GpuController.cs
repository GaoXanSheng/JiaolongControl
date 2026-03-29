using System.Runtime.InteropServices;
using JiaoLongControl.Server.Core.Models;
using JiaoLongControl.Server.Core.Services;
using JiaoLongControl.Server.Core.Utils;

namespace JiaoLongControl.Server.Core.Controllers
{
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.AutoDual)]
    public class GpuController
    {

        public CommandResult Get()
        {
            var res =  MethodServices.GetValue<GpuMode>(MethodName.GpuMode);
            
            return new CommandResult(true,"获取成功", res);
        }

        public CommandResult Set(GpuMode mode)
        {
            var res =  MethodServices.SetValue(MethodName.GpuMode, mode);
            return new CommandResult(res, res ? "设置成功" : "设置失败");
        }
    }
    
}