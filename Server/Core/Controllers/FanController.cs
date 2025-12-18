using JiaoLongControl.Server.Core.Models;
using JiaoLongControl.Server.Core.Services;
using JiaoLongControl.Server.Core.Constants;
using System;
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