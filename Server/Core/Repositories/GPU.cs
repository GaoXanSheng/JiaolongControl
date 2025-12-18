using JiaoLongControl.Server.Core.Models;
using JiaoLongControl.Server.Core.Services;

namespace JiaoLongControl.Server.Core.Repositories
{
    public class GPU
    {
        public static bool Set(GpuMode mode)
        {
            return MethodServices.SetValue(MethodName.GpuMode, mode);
        }

        public static GpuMode Get()
        {
            return MethodServices.GetValue<GpuMode>(MethodName.GpuMode);
        }
    }
}