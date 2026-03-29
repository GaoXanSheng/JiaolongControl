using System.Runtime.InteropServices;
using JiaoLongControl.Server.Core.Models;
using JiaoLongControl.Server.Core.Services;
using JiaoLongControl.Server.Core.Utils;

namespace JiaoLongControl.Server.Core.Controllers
{
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.AutoDual)]
    public class KeyboardController
    {
        public CommandResult GetColor()
        {
            Tuple<int, int, int> tuple = MethodServices.GetValue<Tuple<int, int, int>>(MethodName.RGBKeyboardColor);
            var colorInfo = new ColorInfo
            {
                red = tuple.Item1,
                green = tuple.Item2,
                blue = tuple.Item3
            };
           return new CommandResult(true,"获取成功",colorInfo);
        }

        public CommandResult SetColor(byte r, byte g, byte b)
        {
            var res =  MethodServices.SetValue(MethodName.RGBKeyboardColor, new byte[3] { r, g, b });
            return new CommandResult(res, res ? "设置成功" : "设置失败");
        }

        public CommandResult GetMode()
        {
            return new CommandResult(true, "获取成功", MethodServices.GetValue<RGBKeyboardMode>(MethodName.RGBKeyboardMode));
        }

        public CommandResult SetMode(RGBKeyboardMode mode)
        {
            var res =  MethodServices.SetValue(MethodName.RGBKeyboardMode, mode);
            return new CommandResult(res, res ? "设置成功" : "设置失败");
        }

        public CommandResult GetLightBrightness()
        {
            return new CommandResult(true, "获取成功", MethodServices.GetValue<RGBKeyboardBrightnessLevel>(MethodName.RGBKeyboardBrightness));
        }

        public CommandResult SetLightBrightness(RGBKeyboardBrightnessLevel br)
        {
            var res =  MethodServices.SetValue(MethodName.RGBKeyboardBrightness, (byte)br);
            return new CommandResult(res, res ? "设置成功" : "设置失败");
        }
    }
}