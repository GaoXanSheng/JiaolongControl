using JiaoLongControl.Server.Core.Models;
using JiaoLongControl.Server.Core.Services;

namespace JiaoLongControl.Server.Core.Controllers
{
    [System.Runtime.InteropServices.ComVisible(true)]
    public class LogoLightController
    {
        public bool Get()
        {
            return MethodServices.GetValue<byte>(MethodName.Ambientlight) == (byte)ResultState.ON;
        }

        public bool Set(ResultState state)
        {
            return MethodServices.SetValue(MethodName.Ambientlight, state);
        }
    }
}