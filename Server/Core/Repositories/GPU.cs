using JiaoLongControl.Server.Core.Constants;
using JiaoLongControl.Server.Core.Services;

namespace JiaoLongControl.Server.Core.Repositories
{
    public class GPU
    {
        public static bool Set(GPUMode mode)
        {
            return MethodServices.SetValue(MethodName.GPUMode, mode);
        }

        public static GPUMode Get()
        {
            return MethodServices.GetValue<GPUMode>(MethodName.GPUMode);
        }
    }
}