namespace JiaoLongControl.Server.Core.Models
{
    public enum CPUPower : byte
    {
        CloseState = 0,
        OpenState,
        LongPower,
        ShortPower,
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

    /// <summary>
    /// EC 热键事件名 (来自官方 HID_EVENT20 事件通道的 EventDetail[1])。
    /// EventDetail: [0]=WMIEventType(HotKey=1), [1]=EventName, [2]=值 (风扇速度为 [2]&lt;&lt;8|[3] 双字节)。
    /// </summary>
    public enum EventName : byte
    {
        Reserved_1 = 1,
        Reserved_2 = 2,
        Reserved_3 = 3,
        AirPlaneMode = 4,
        RGBKeyboardBrightness = 5,
        TouchPadState = 6,
        FnState = 7,
        RGBKeyboardMode = 8,
        CapsLkState = 9,
        AmbientlightState = 10,
        CalculatorStart = 11,
        DefaultBrowserStart = 12,
        NumLockState = 13,
        ScrlockState = 14,
        SystemPerMode = 15,
        FN_J = 16,
        FN_F = 17,
        FN_0 = 18,
        FN_1 = 19,
        FN_2 = 20,
        FN_3 = 21,
        FN_4 = 22,
        FN_5 = 24,
        PanelRefreshRate = 25,
        CPUFanSpeed = 26,
        GPUFanSpeed = 32,
        WinKeyLock = 33,
        UnKnow = 255,
    }
}