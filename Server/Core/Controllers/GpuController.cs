using JiaoLongControl.Server.Core.Constants;
using JiaoLongControl.Server.Core.Models;
using JiaoLongControl.Server.Core.Services;

namespace JiaoLongControl.Server.Core.Controllers
{
    [System.Runtime.InteropServices.ComVisible(true)]
    public class GpuController
    {

        public GPUMode Get()
        {

            return MethodServices.GetValue<GPUMode>(MethodName.GPUMode);
        }

        public bool Set(GPUMode mode)
        {
            return MethodServices.SetValue(MethodName.GPUMode, mode);
        }
    }
}