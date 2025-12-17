using System.Text.Json.Nodes;
using JiaoLongControl.Server.Core.Constants;
using JiaoLongControl.Server.Core.Services;

namespace JiaoLongControl.Server.Core.Repositories
{
    public class Keyboard
    {
        public class Color
        {
            public static JsonObject Get()
            {
                var json = new JsonObject();
                Tuple<int, int, int> tuple = MethodServices.GetValue<Tuple<int, int, int>>(MethodName.RGBKeyboardColor);
                json["red"] = tuple.Item1;
                json["green"] = tuple.Item2;
                json["blue"] = tuple.Item3;
                return json;
            }

            public static bool Set(byte red, byte green, byte blue)
            {
                return MethodServices.SetValue(MethodName.RGBKeyboardColor, new byte[3] { red, green, blue });
            }
        }

        public class Mode
        {
            public static bool Set()
            {
                return MethodServices.SetValue(MethodName.RGBKeyboardMode, RGBKeyboardMode.Mode_RGBFixedMode);
            }
            public static RGBKeyboardMode Get()
            {
                return MethodServices.GetValue<RGBKeyboardMode>(MethodName.RGBKeyboardMode);
            }
        }

        public class LightBrightness
        {
            public static RGBKeyboardBrightnessLevel Get()
            {
                return MethodServices.GetValue<RGBKeyboardBrightnessLevel>(MethodName.RGBKeyboardBrightness);
            }
            public static bool Set(byte b)
            {
                if (b > 0 && b <= 4) return MethodServices.SetValue(MethodName.RGBKeyboardBrightness, b);
                return false;
            }
        }
    }
}