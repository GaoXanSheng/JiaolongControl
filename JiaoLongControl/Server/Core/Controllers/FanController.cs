using JiaoLongControl.Server.Core.Models;
using JiaoLongControl.Server.Core.Services;
using System.Text.Json;

namespace JiaoLongControl.Server.Core.Controllers
{
    [System.Runtime.InteropServices.ComVisible(true)]
    public class FanController
    {
        public string GetFanSpeed()
        {
            Tuple<int, int> CPUGPUFanSpeed = MethodServices.GetValue<Tuple<int, int>>(MethodName.CPUGPUFanSpeed);
            var fanSpeedInfo = new FanSpeedInfo
            {
                CPUFanSpeed = CPUGPUFanSpeed.Item1,
                GPUFanSpeed = CPUGPUFanSpeed.Item2
            };
            return JsonSerializer.Serialize(fanSpeedInfo);
        }

        public bool SetFanSpeed(byte fanSpeed)
        {
            // ACPI表的风扇调速比EC的风扇调速优先级更高，所以如果开启了ACPI表的风扇调速，就无法通过EC来设置风扇速度，因此需要先关闭ACPI表的风扇调速开关
            if (GetMaxFanSpeedSwitch())
            {
                SetMaxFanSpeedSwitch(false);
            }
            using (ECController ec = new ECController())
            {
                if (ec.State)
                {
                    ec.Fan1SetSpeed(fanSpeed);
                    ec.Fan2SetSpeed(fanSpeed);
                    return true;
                }
            }

            return false;
        }

        public bool RemoveFanSpeed()
        {
            using (ECController ec = new ECController())
            {
                if (ec.State)
                {
                    ec.RemoveFanSpeed();
                    return true;
                }
            }

            return false;
        }

        public bool SetMaxFanSpeedSwitch(bool maxFanSpeedSwitch)
        {
            return MethodServices.SetValue(MethodName.MaxFanSpeedSwitch, (byte)(maxFanSpeedSwitch ? 1 : 0));
        }

        public bool GetMaxFanSpeedSwitch()
        {
            return MethodServices.GetValue<byte>(MethodName.MaxFanSpeedSwitch) == 1;
        }
    }
}