using System.Text.Json.Nodes;
using JiaoLongControl.Server.Core.Models;
using JiaoLongControl.Server.Core.Controllers;
using JiaoLongControl.Server.Core.Services;

namespace JiaoLongControl.Server.Core.Repositories
{
    public class Fan
    {
        public static string SetFanSpeed(string inputSpeed)
        {
            if (string.IsNullOrWhiteSpace(inputSpeed)) return "Input Speed Error";
            string trimmedInput = inputSpeed.Length >= 2 ? inputSpeed.Substring(0, 2) : inputSpeed;
            if (!byte.TryParse(trimmedInput, out byte outSpeed)) return "Input Speed Error";

            using (ECController ec = new ECController())
            {
                if (ec.State)
                {
                    ec.Fan1SetSpeed(outSpeed);
                    ec.Fan2SetSpeed(outSpeed);
                    return "Fan Speed Set OK";
                }
            }
            return "Fan Speed Set Error";
        }

        public static bool SetMaxFanSpeedSwitch(string set)
        {
            return MethodServices.SetValue(MethodName.MaxFanSpeedSwitch, (byte)(set == "1" ? 1 : 0));
        }

        public static byte GetMaxFanSpeedSwitch()
        {
            return MethodServices.GetValue<byte>(MethodName.MaxFanSpeedSwitch);
        }

        public static JsonObject GetFanSpeed()
        {
            var res = new JsonObject();
            Tuple<int, int> CPUGPUFanSpeed = MethodServices.GetValue<Tuple<int, int>>(MethodName.CPUGPUFanSpeed);
            res["CPUFanSpeed"] = CPUGPUFanSpeed.Item1;
            res["GPUFanSpeed"] = CPUGPUFanSpeed.Item2;
            return res;
        }
    }
}