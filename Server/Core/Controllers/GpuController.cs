using JiaoLongControl.Server.Core.Models;
using JiaoLongControl.Server.Core.Services;

namespace JiaoLongControl.Server.Core.Controllers
{
    [System.Runtime.InteropServices.ComVisible(true)]
    public class GpuController
    {

        public GpuMode Get()
        {

            return MethodServices.GetValue<GpuMode>(MethodName.GpuMode);
        }

        public bool Set(GpuMode mode)
        {
            return MethodServices.SetValue(MethodName.GpuMode, mode);
        }
    }
}