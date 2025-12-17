using JiaoLongControl.Server.Core.Constants;
using JiaoLongControl.Server.Core.Services;

namespace JiaoLongControl.Server.Core.Repositories
{
    public class CPU
    {
        public static bool SetCpuShortPower(byte LongPower)
        {
            return MethodServices.SetValue(MethodName.CPUPower, new byte[2]
            {
                (byte)CPUPower.SPLState,
                LongPower
            });
        }

        public static bool OpenCustomMode(bool Open)
        {
            if (Open) return MethodServices.SetValue(MethodName.CPUPower, CPUPower.OpenState);
            return MethodServices.SetValue(MethodName.CPUPower, CPUPower.CloseState);
        }

        public static bool GetCustomMode()
        {
            var res = MethodServices.GetValue<CPUPower>(MethodName.CPUPower);
            return res == CPUPower.OpenState;
        }

        public static bool SetCpuLongPower(byte ShortPower)
        {
            return MethodServices.SetValue(MethodName.CPUPower, new byte[2]
            {
                (byte)CPUPower.SPPTState,
                ShortPower
            });
        }

        public static bool SetCPUTempWall(byte tempwall)
        {
            return MethodServices.SetValue(MethodName.CPUPower, new byte[2]
            {
                (byte)CPUPower.CPUTempWallState,
                tempwall
            });
        }
    }
}