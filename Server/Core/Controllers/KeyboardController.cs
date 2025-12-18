using JiaoLongControl.Server.Core.Constants;
using JiaoLongControl.Server.Core.Models;
using JiaoLongControl.Server.Core.Services;
using System;
using System.Text.Json;

namespace JiaoLongControl.Server.Core.Controllers
{
    [System.Runtime.InteropServices.ComVisible(true)]
    public class KeyboardController
    {
        public string GetColor()
        {
            Tuple<int, int, int> tuple = MethodServices.GetValue<Tuple<int, int, int>>(MethodName.RGBKeyboardColor);
            var colorInfo = new ColorInfo
            {
                red = tuple.Item1,
                green = tuple.Item2,
                blue = tuple.Item3
            };
            return JsonSerializer.Serialize(colorInfo);
        }

        public bool SetColor(byte r, byte g, byte b)
        {
            return MethodServices.SetValue(MethodName.RGBKeyboardColor, new byte[3] { r, g, b });
        }

        public RGBKeyboardMode GetMode()
        {
            return MethodServices.GetValue<RGBKeyboardMode>(MethodName.RGBKeyboardMode);
        }

        public bool SetMode(RGBKeyboardMode mode)
        {
            return MethodServices.SetValue(MethodName.RGBKeyboardMode, mode);
        }

        public RGBKeyboardBrightnessLevel GetLightBrightness()
        {
            return MethodServices.GetValue<RGBKeyboardBrightnessLevel>(MethodName.RGBKeyboardBrightness);
        }

        public bool SetLightBrightness(RGBKeyboardBrightnessLevel br)
        {
            return MethodServices.SetValue(MethodName.RGBKeyboardBrightness, (byte)br);
        }
    }
}