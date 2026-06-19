using System.Runtime.InteropServices;

namespace JiaoLongControl.Server.Core.Native;

/// <summary>
/// 直接通过 nvapi64.dll 的 nvapi_QueryInterface 调用 GPU 时钟/功耗控制函数
/// </summary>
public static class NvapiClockPower
{
    private delegate IntPtr QueryInterface_t(uint id);

    // NVAPI 函数 ID (CRC32)
    private const uint Id_Initialize = 0x0150E828;
    private const uint Id_Unload = 0xD22BDD7E;
    private const uint Id_EnumPhysicalGPUs = 0xE5AC921F;
    private const uint Id_GPU_SetPStates20 = 0x0F4DAE28;
    private const uint Id_GPU_SetPowerLimit = 0x01E657B5;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int NvApiInit_t();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int NvApiEnumPhysicalGPUs_t([In, Out] NVPhysicalGpuHandle[] handles, [In, Out] uint[] count);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int NvApiSetPStates20_t(NVPhysicalGpuHandle handle, NVPStates20Set set);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int NvApiSetPowerLimit_t(NVPhysicalGpuHandle handle, NVPowerLimitSet limit);

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    private struct NVPhysicalGpuHandle { public IntPtr ptr; }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    private struct NVPStates20Set
    {
        public uint version;
        public uint flags;
        public uint numClocks;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 512)]
        public byte[] clockData;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    private struct NVPowerLimitSet
    {
        public uint version;
        public uint power_mW;
    }

    private static readonly IntPtr Module;
    private static readonly QueryInterface_t? QueryInterface;

    static NvapiClockPower()
    {
        Module = Kernel32.LoadLibrary("nvapi64.dll");
        if (Module == IntPtr.Zero) return;

        var qiPtr = Kernel32.GetProcAddress(Module, "nvapi_QueryInterface");
        if (qiPtr == IntPtr.Zero) return;

        QueryInterface = Marshal.GetDelegateForFunctionPointer<QueryInterface_t>(qiPtr);
    }

    public static bool IsAvailable => Module != IntPtr.Zero && QueryInterface != null;

    private static bool InitAndGetGpu(int gpuIndex, out NVPhysicalGpuHandle handle)
    {
        handle = default;
        if (!IsAvailable) return false;

        var initPtr = QueryInterface!(Id_Initialize);
        if (initPtr == IntPtr.Zero) return false;
        var init = Marshal.GetDelegateForFunctionPointer<NvApiInit_t>(initPtr);
        if (init() != 0) return false;

        var enumPtr = QueryInterface!(Id_EnumPhysicalGPUs);
        if (enumPtr == IntPtr.Zero) return false;

        var handles = new NVPhysicalGpuHandle[1];
        var count = new uint[1];
        var enumFn = Marshal.GetDelegateForFunctionPointer<NvApiEnumPhysicalGPUs_t>(enumPtr);
        if (enumFn(handles, count) != 0 || count[0] == 0) return false;

        uint idx = gpuIndex >= 0 && gpuIndex < count[0] ? (uint)gpuIndex : 0;
        handle = handles[idx];
        return true;
    }

    private static void Shutdown()
    {
        var unloadPtr = QueryInterface!(Id_Unload);
        if (unloadPtr == IntPtr.Zero) return;
        var unload = Marshal.GetDelegateForFunctionPointer<NvApiInit_t>(unloadPtr);
        unload();
    }

    public static bool SetGpuClock(int freqMhz, int gpuIndex = -1)
    {
        if (!InitAndGetGpu(gpuIndex, out var handle)) { Shutdown(); return false; }
        try
        {
            var fnPtr = QueryInterface!(Id_GPU_SetPStates20);
            if (fnPtr == IntPtr.Zero) return false;
            var fn = Marshal.GetDelegateForFunctionPointer<NvApiSetPStates20_t>(fnPtr);

            var set = new NVPStates20Set
            {
                version = 0x20002,
                flags = 0x5,
                numClocks = 2,
                clockData = new byte[512]
            };

            BitConverter.GetBytes((uint)0).CopyTo(set.clockData, 0);
            BitConverter.GetBytes((uint)(freqMhz * 1000)).CopyTo(set.clockData, 4);

            BitConverter.GetBytes((uint)4).CopyTo(set.clockData, 8);
            BitConverter.GetBytes((uint)(freqMhz * 1000)).CopyTo(set.clockData, 12);

            return fn(handle, set) == 0;
        }
        finally { Shutdown(); }
    }

    public static bool SetMemoryClock(int freqMhz, int gpuIndex = -1)
    {
        if (!InitAndGetGpu(gpuIndex, out var handle)) { Shutdown(); return false; }
        try
        {
            var fnPtr = QueryInterface!(Id_GPU_SetPStates20);
            if (fnPtr == IntPtr.Zero) return false;
            var fn = Marshal.GetDelegateForFunctionPointer<NvApiSetPStates20_t>(fnPtr);

            var set = new NVPStates20Set
            {
                version = 0x20002,
                flags = 0x5,
                numClocks = 1,
                clockData = new byte[512]
            };

            BitConverter.GetBytes((uint)4).CopyTo(set.clockData, 0);
            BitConverter.GetBytes((uint)(freqMhz * 1000)).CopyTo(set.clockData, 4);

            return fn(handle, set) == 0;
        }
        finally { Shutdown(); }
    }

    public static bool ResetGpuClock(int gpuIndex = -1)
    {
        if (!InitAndGetGpu(gpuIndex, out var handle)) { Shutdown(); return false; }
        try
        {
            var fnPtr = QueryInterface!(Id_GPU_SetPStates20);
            if (fnPtr == IntPtr.Zero) return false;
            var fn = Marshal.GetDelegateForFunctionPointer<NvApiSetPStates20_t>(fnPtr);

            var set = new NVPStates20Set
            {
                version = 0x20002,
                flags = 0x4,
                numClocks = 0,
                clockData = new byte[512]
            };

            return fn(handle, set) == 0;
        }
        finally { Shutdown(); }
    }

    public static bool ResetMemoryClock(int gpuIndex = -1)
    {
        return ResetGpuClock(gpuIndex);
    }

    public static bool SetPowerLimit(int watts, int gpuIndex = -1)
    {
        if (!InitAndGetGpu(gpuIndex, out var handle)) { Shutdown(); return false; }
        try
        {
            var fnPtr = QueryInterface!(Id_GPU_SetPowerLimit);
            if (fnPtr == IntPtr.Zero) return false;
            var fn = Marshal.GetDelegateForFunctionPointer<NvApiSetPowerLimit_t>(fnPtr);

            var set = new NVPowerLimitSet
            {
                version = 0x10002,
                power_mW = (uint)(watts * 1000)
            };

            return fn(handle, set) == 0;
        }
        finally { Shutdown(); }
    }
}
