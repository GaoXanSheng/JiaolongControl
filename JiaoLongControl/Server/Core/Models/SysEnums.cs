namespace JiaoLongControl.Server.Core.Models
{
    public enum CPUPower : byte
    {
        CloseState = 0,
        OpenState,
        SPLState,
        SPPTState,
        CpuTempWallState,
        Unknow = 255
    }

    public static class ECMemoryTable
    {
        public const ushort EC_ADDR_PORT = 0x4E;
        public const ushort EC_DATA_PORT = 0x4F;
        public const ushort Fan1_RPM_Level = 0xC836;
        public const ushort Fan2_RPM_Level = 0xC837;
        public const ushort Fan1_RPM = 0xC834;
        public const ushort Fan2_RPM = 0xC835;
        public const ushort Fan1_RPM_SET = 0xC83C;
        public const ushort Fan2_RPM_SET = 0xC83D;
        public const ushort EC_Version = 0xC411;
    }

    public class FanCurvePoint
    {
        public int Temp { get; set; }
        public int Speed { get; set; }
    }

    public enum GpuMode : byte
    {
        HybridMode = 0,
        DiscreteMode = 1,
        Unknow = 255
    }

    public enum MethodName
    {
        SystemPerMode = 8,
        GpuMode,
        RgbKeyboardStatus,
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