using System.Text.Json.Nodes;
using JiaoLongControl.Server.Core.Models;
using JiaoLongControl.Server.Core.Repositories;

namespace JiaoLongControl.Server.Core.Controllers
{
    public class CliProgramEnumerationType
    {
        private JsonObject callBack = new JsonObject();
        public string EumType(string typeName, string methodName, string[] args)
        {
            callBack.Clear();
            callBack["typeName"] = typeName;
            callBack["methodName"] = methodName;
            callBack["result"] = false;

            try
            {
                switch (typeName)
                {
                    case "CPU":
                        HandleCpu(methodName, args);
                        break;
                    case "GetHardwareMonitorInfo":
                        // 复用 FanControlService 中的静态实例，避免重复创建对象
                        callBack["result"] = Services.FanControlService.ComputerInfo.GetHardwareMonitorInfo();
                        break;
                    case "Fan":
                        HandleFan(methodName, args);
                        break;
                    case "GPUMode":
                        HandleGpu(methodName, args);
                        break;
                    case "LogoLight":
                        HandleLogo(methodName, args);
                        break;
                    case "Keyboard":
                        HandleKeyboard(methodName, args);
                        break;
                    case "PerformaceMode":
                        HandlePerf(methodName, args);
                        break;
                    default:
                        callBack["msg"] = "未知类型";
                        break;
                }
            }
            catch (Exception e)
            {
                callBack["result"] = false;
                callBack["msg"] = e.Message;
            }

            return callBack.ToString();
        }

        private void HandleCpu(string method, string[] args)
        {
            switch(method)
            {
                case "SetCpuShortPower":
                    if(args.Length > 0 && byte.TryParse(args[0], out byte sp)) 
                        callBack["result"] = CPU.SetCpuShortPower(sp).ToString();
                    break;
                case "SetCpuLongPower":
                    if(args.Length > 0 && byte.TryParse(args[0], out byte lp)) 
                        callBack["result"] = CPU.SetCpuLongPower(lp).ToString();
                    break;
                case "OpenCustomMode":
                    if(args.Length > 0 && bool.TryParse(args[0], out bool open)) 
                        callBack["result"] = CPU.OpenCustomMode(open).ToString();
                    break;
                case "GetCustomMode":
                    callBack["result"] = CPU.GetCustomMode().ToString();
                    break;
                case "SetCPUTempWall":
                    if(args.Length > 0 && byte.TryParse(args[0], out byte tw)) 
                        callBack["result"] = CPU.SetCPUTempWall(tw).ToString();
                    break;
            }
        }

        private void HandleFan(string method, string[] args)
        {
            switch(method)
            {
                case "GetFanSpeed":
                    callBack["result"] = Fan.GetFanSpeed();
                    break;
                case "SetFanSpeed":
                    if(args.Length > 0) callBack["result"] = Fan.SetFanSpeed(args[0]);
                    break;
                case "SetMaxFanSpeedSwitch":
                    if(args.Length > 0) callBack["result"] = Fan.SetMaxFanSpeedSwitch(args[0]);
                    break;
                case "GetMaxFanSpeedSwitch":
                    callBack["result"] = Fan.GetMaxFanSpeedSwitch();
                    break;
            }
        }

        private void HandleGpu(string method, string[] args)
        {
            if (method == "Get") callBack["result"] = GPU.Get().ToString();
            else if (method == "Set" && args.Length > 0 && Enum.TryParse(args[0], out GpuMode m))
                callBack["result"] = GPU.Set(m).ToString();
        }

        private void HandleLogo(string method, string[] args)
        {
            if (method == "Get") callBack["result"] = LogoLight.Get().ToString();
            else if (method == "Set" && args.Length > 0 && Enum.TryParse(args[0], out ResultState m))
                callBack["result"] = LogoLight.Set(m).ToString();
        }

        private void HandlePerf(string method, string[] args)
        {
             if (method == "Get") callBack["result"] = PerformaceMode.Get().ToString();
            else if (method == "Set" && args.Length > 0 && Enum.TryParse(args[0], out SystemPerMode m))
                callBack["result"] = PerformaceMode.Set(m).ToString();
        }

        private void HandleKeyboard(string method, string[] args)
        {
            switch(method)
            {
                case "ColorGet": callBack["result"] = Keyboard.Color.Get(); break;
                case "ColorSet":
                    if(args.Length > 2 && byte.TryParse(args[0], out byte r) && byte.TryParse(args[1], out byte g) && byte.TryParse(args[2], out byte b))
                        callBack["result"] = Keyboard.Color.Set(r, g, b);
                    break;
                case "ModeGet": callBack["result"] = Keyboard.Mode.Get().ToString(); break;
                case "ModeSet": callBack["result"] = Keyboard.Mode.Set(); break;
                case "LightBrightnessGet": callBack["result"] = Keyboard.LightBrightness.Get().ToString(); break;
                case "LightBrightnessSet":
                    if(args.Length > 0 && byte.TryParse(args[0], out byte br))
                        callBack["result"] = Keyboard.LightBrightness.Set(br);
                    break;
            }
        }
    }
}