using JiaoLongControl.Server.Core.Constants;
using JiaoLongControl.Server.Core.Services;

namespace JiaoLongControl.Server.Core.Controllers
{
    [System.Runtime.InteropServices.ComVisible(true)]
    public class CpuController
    {
        public bool SetCpuShortPower(byte sp)
        {
            return MethodServices.SetValue(MethodName.CPUPower, new byte[2]
            {
                (byte)CPUPower.SPLState,
                sp
            });
        }

        public bool SetCpuLongPower(byte lp)
        {
            return MethodServices.SetValue(MethodName.CPUPower, new byte[2]
            {
                (byte)CPUPower.SPPTState,
                lp
            });
        }

        public bool SetCustomMode(bool open)
        {
            if (open)
            {
                return MethodServices.SetValue(MethodName.CPUPower, CPUPower.OpenState);
            }
            else
            {
                return MethodServices.SetValue(MethodName.CPUPower, CPUPower.CloseState);
            }
        }

        public bool GetCustomMode()
        {
            var res = MethodServices.GetValue<CPUPower>(MethodName.CPUPower);
            return res == CPUPower.OpenState;
        }

        public bool SetCPUTempWall(byte tw)
        {
            return MethodServices.SetValue(MethodName.CPUPower, new byte[2]
            {
                (byte)CPUPower.CPUTempWallState,
                tw
            });
        }
    }
}