using JiaoLongControl.Server.Core.Constants;
using JiaoLongControl.Server.Core.Services;

namespace JiaoLongControl.Server.Core.Repositories
{
    public class LogoLight
    {
        public static bool Set(ResultState m)
        {
            return MethodServices.SetValue(MethodName.Ambientlight, m);
        }

        public static ResultState Get()
        {
            return MethodServices.GetValue<ResultState>(MethodName.Ambientlight);
        }
    }
}