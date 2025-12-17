using JiaoLongControl.Server.Core.Constants;
using JiaoLongControl.Server.Core.Services;

namespace JiaoLongControl.Server.Core.Repositories
{
    public class PerformaceMode
    {
        public static bool Set(SystemPerMode mode)
        {
            return MethodServices.SetValue(MethodName.SystemPerMode, mode);
        }

        public static SystemPerMode Get()
        {
            return MethodServices.GetValue<SystemPerMode>(MethodName.SystemPerMode);
        }
    }
}