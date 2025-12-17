namespace JiaoLongControl.Server.Core.Constants
{
    public enum CPUPower : byte
    {
        CloseState = 0,
        OpenState,
        SPLState,
        SPPTState,
        CPUTempWallState,
        Unknow = 255
    }

    public enum ECMemoryTable : ushort
    {
        EC_ADDR_PORT = 0x4E,
        EC_DATA_PORT = 0x4F,
        Fan1_RPM_Level = 0xC836,
        Fan2_RPM_Level = 0xC837,
        Fan1_RPM = 0XC834,
        Fan2_RPM = 0XC835,
        Fan1_RPM_SET = 0xC83C,
        Fan2_RPM_SET = 0xC83D,
        EC_Version = 0xC411,
    }

    public class FanCurvePoint
    {
        public int temp { get; set; }
        public int speed { get; set; }
    }

    public enum GPUMode : byte
    {
        HybridMode = 0,
        DiscreteMode = 1,
        Unknow = 255
    }

    public enum MethodName
    {
        SystemPerMode = 8,
        GPUMode,
        RGBKeyboardStatus,
        FnLock,
        TPLock,
        CPUGPUFanSpeed,
        GPUFanSpeed_NotUse,
        Ambientlight,
        RGBKeyboardMode,
        RGBKeyboardColor,
        RGBKeyboardBrightness,
        SystemAcType,
        MaxFanSpeedSwitch,
        MaxFanSpeed,
        CPUThermometer,
        CPUPower,
        UnKnow = 255
    }

    public enum MethodType
    {
        Get = 250,
        Set = 251,
    }

    public enum ResultState : byte
    {
        OFF,
        ON,
        Unknow = 255
    }

    public enum RGBKeyboardBrightnessLevel : byte
    {
        Level_0,
        Level_1,
        Level_2,
        Level_3,
        Unknow = 255
    }

    public enum RGBKeyboardMode : byte
    {
        Mode_Off = 0,
        Mode_RGBFixedMode = 2,
        Unknow = 255
    }

    public enum SystemPerMode : byte
    {
        BalanceMode,
        PerformanceMode,
        QuietMode,
        Unknow = 255
    }
}