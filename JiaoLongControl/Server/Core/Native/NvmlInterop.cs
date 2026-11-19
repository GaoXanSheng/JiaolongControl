using System.IO;
using System.Runtime.InteropServices;

namespace JiaoLongControl.Server.Core.Native;

/// <summary>
/// NVIDIA NVML (nvml.dll, 随驱动分发) 的最小互操作层。
/// nvidia-smi 本身就是 NVML 的前端, 本层替代原先经命令行子进程实现的
/// 锁频/功耗控制, 免去进程创建与文本解析, 错误码也结构化。
/// 锁频与功耗写入需要管理员权限 (程序清单已保证)。
/// </summary>
internal static class NvmlInterop
{
    private const int NvmlClockSm = 1;
    private const int NvmlClockMem = 2;

    private const string DllName = "nvml.dll";

    private static readonly object InitLock = new();
    private static bool _dllResolved;
    private static bool _initialized;

    // --- 原生导出 (nvmlReturn_t = int, 全部 cdecl) ---

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int nvmlInit_v2();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int nvmlShutdown();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int nvmlDeviceGetHandleByIndex_v2(uint index, out IntPtr device);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int nvmlDeviceSetGpuLockedClocks(IntPtr device, uint minGpuClockMHz, uint maxGpuClockMHz);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int nvmlDeviceResetGpuLockedClocks(IntPtr device);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int nvmlDeviceSetMemoryLockedClocks(IntPtr device, uint minMemClockMHz, uint maxMemClockMHz);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int nvmlDeviceResetMemoryLockedClocks(IntPtr device);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int nvmlDeviceGetMaxClockInfo(IntPtr device, int type, out uint clock);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr nvmlErrorString(int result);

    // --- 生命周期 ---

    /// <summary>首次调用时解析 nvml.dll 并初始化 NVML, 幂等。整个进程保持初始化。</summary>
    public static void EnsureInitialized()
    {
        lock (InitLock)
        {
            if (_initialized)
                return;
            ResolveDll();
            Check(nvmlInit_v2(), "nvmlInit_v2");
            _initialized = true;
        }
    }

    /// <summary>尽力关闭 NVML (卸载模块无意义, 句柄绑定由 DllImport 维护)。</summary>
    public static void Release()
    {
        lock (InitLock)
        {
            if (!_initialized)
                return;
            try { nvmlShutdown(); } catch { }
            _initialized = false;
        }
    }

    /// <summary>
    /// 新版驱动把 nvml.dll 放进 System32, 标准搜索即可命中;
    /// 旧版驱动只装在 NVSMI 目录, 需以绝对路径预加载。加载进进程后,
    /// 下方 DllImport("nvml.dll") 的绑定会命中已加载模块。
    /// </summary>
    private static void ResolveDll()
    {
        if (_dllResolved)
            return;
        if (Kernel32.LoadLibrary(DllName) == IntPtr.Zero)
        {
            string legacy = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "NVIDIA Corporation", "NVSMI", DllName);
            if (!File.Exists(legacy) || Kernel32.LoadLibrary(legacy) == IntPtr.Zero)
                throw new InvalidOperationException("未找到 nvml.dll, 请确认已安装 NVIDIA 显卡驱动");
        }
        _dllResolved = true;
    }

    /// <summary>锁频/锁显存共用核心：范围颠倒自动交换，errName 仅用于失败消息。</summary>
    private static void SetLockedClockRange(int gpuIndex, int minMhz, int maxMhz, SetLockedClocks api, string errName)
    {
        if (minMhz > maxMhz)
            (minMhz, maxMhz) = (maxMhz, minMhz);
        EnsureInitialized();
        var device = GetDevice(gpuIndex);
        Check(api(device, (uint)Math.Max(0, minMhz), (uint)Math.Max(0, maxMhz)), errName);
    }

    private static void ResetLockedClockRange(int gpuIndex, ResetLockedClocks api, string errName)
    {
        EnsureInitialized();
        var device = GetDevice(gpuIndex);
        Check(api(device), errName);
    }

    /// <summary>
    /// 下限模式: 核心频率锁定在 [minMhz, 驱动最大SM频率],
    /// 负载下可自然睿频到上限, 不会降频到 minMhz 以下。
    /// minMhz 超过驱动上限时收敛为钉死在上限。
    /// </summary>
    public static void SetGpuClockFloor(int gpuIndex, int minMhz)
    {
        int max = (int)GetMaxClock(gpuIndex, NvmlClockSm);
        SetGpuLockedClocks(gpuIndex, Math.Min(minMhz, max), max);
    }

    /// <summary>显式范围锁核频 (min=max 即钉死)。</summary>
    public static void SetGpuLockedClocks(int gpuIndex, int minMhz, int maxMhz)
        => SetLockedClockRange(gpuIndex, minMhz, maxMhz, nvmlDeviceSetGpuLockedClocks, "nvmlDeviceSetGpuLockedClocks");

    public static void ResetGpuLockedClocks(int gpuIndex)
        => ResetLockedClockRange(gpuIndex, nvmlDeviceResetGpuLockedClocks, "nvmlDeviceResetGpuLockedClocks");

    /// <summary>下限模式锁显存频率, 语义同 <see cref="SetGpuClockFloor"/>。</summary>
    public static void SetMemoryClockFloor(int gpuIndex, int minMhz)
    {
        int max = (int)GetMaxClock(gpuIndex, NvmlClockMem);
        SetMemoryLockedClocks(gpuIndex, Math.Min(minMhz, max), max);
    }

    /// <summary>显式范围锁显存频率 (min=max 即钉死)。</summary>
    public static void SetMemoryLockedClocks(int gpuIndex, int minMhz, int maxMhz)
        => SetLockedClockRange(gpuIndex, minMhz, maxMhz, nvmlDeviceSetMemoryLockedClocks, "nvmlDeviceSetMemoryLockedClocks");

    public static void ResetMemoryLockedClocks(int gpuIndex)
        => ResetLockedClockRange(gpuIndex, nvmlDeviceResetMemoryLockedClocks, "nvmlDeviceResetMemoryLockedClocks");

    /// <summary>
    /// 驱动当前生效的最大 SM 频率 (MHz)。应用曲线偏移后该读数是否随偏移上移,
    /// 是「锁频上限是否动态含偏移」的观测点 — 决定锁最低频率与超频能否共存。
    /// </summary>
    public static int GetMaxSmClockMhz(int gpuIndex)
    {
        return (int)GetMaxClock(gpuIndex, NvmlClockSm);
    }

    // --- 内部工具 ---

    private static IntPtr GetDevice(int gpuIndex)
    {
        EnsureInitialized();
        var device = IntPtr.Zero;
        Check(nvmlDeviceGetHandleByIndex_v2((uint)Math.Max(0, gpuIndex), out device),
            "nvmlDeviceGetHandleByIndex_v2");
        return device;
    }

    private static uint GetMaxClock(int gpuIndex, int clockType)
    {
        var device = GetDevice(gpuIndex);
        Check(nvmlDeviceGetMaxClockInfo(device, clockType, out uint mhz), "nvmlDeviceGetMaxClockInfo");
        return mhz;
    }

    private static void Check(int result, string api)
    {
        if (result == 0)
            return;
        string text = "";
        try { text = Marshal.PtrToStringAnsi(nvmlErrorString(result)) ?? ""; } catch { }
        throw new InvalidOperationException($"NVML {api} 失败 (代码 {result}): {text}");
    }

    // --- 锁频控制 ---

    private delegate int SetLockedClocks(IntPtr device, uint minMhz, uint maxMhz);

    private delegate int ResetLockedClocks(IntPtr device);
}
