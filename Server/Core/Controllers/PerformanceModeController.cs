using JiaoLongControl.Server.Core.Constants;
using JiaoLongControl.Server.Core.Models;
using JiaoLongControl.Server.Core.Services;

namespace JiaoLongControl.Server.Core.Controllers
{
    [System.Runtime.InteropServices.ComVisible(true)]
    public class PerformanceModeController
    {
        public SystemPerMode Get()
        {
            return MethodServices.GetValue<SystemPerMode>(MethodName.SystemPerMode);
        }

        public bool Set(SystemPerMode mode)
        {
            return MethodServices.SetValue(MethodName.SystemPerMode, mode);
        }
    }
}