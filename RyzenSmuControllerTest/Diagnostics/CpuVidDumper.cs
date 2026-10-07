using System.Runtime.InteropServices;
using RyzenSmuControllerTest.Infrastructure;
using RyzenSmuControllerTest.Infrastructure.Native;

namespace RyzenSmuControllerTest.Diagnostics;

/// <summary>
/// 通过 MSR 直接读取 VID 电压（--cpu-vid / --vid-per-core）。
/// CPU-Z 读核心电压的机制：通过 MSR 读当前 VID 再换算 (V = 1.550 - VID * 0.00625)。
/// 候选寄存器：0xC0010293 (FIDVID_STATUS, VID=[15:6])、0xC0010071 (COFVID_STATUS)。
/// 刻意不走主控制器：直接加载 LHM 的 AMDFamily17.bin 模块（含 ioctl_read_msr）独立验证机制本身。
/// </summary>
internal static class CpuVidDumper
{
    public static void DumpCurrentVidFromMsr()
    {
        ConsoleLog.Log(">>> 通过 MSR 读取当前 VID (CPU-Z 机制) ...");

        const string dllName = "PawnIOLib.dll";
        string dllPath = Path.Combine(AppContext.BaseDirectory, dllName);
        if (!File.Exists(dllPath))
            dllPath = Path.Combine(@"C:\Program Files\PawnIO", dllName);
        if (!File.Exists(dllPath))
        {
            ConsoleLog.Error($"    未找到 {dllName}，请确认 PawnIO 已安装");
            return;
        }

        var hModule = Kernel32Native.LoadLibrary(dllPath);
        if (hModule == IntPtr.Zero)
        {
            ConsoleLog.Error($"    加载 {dllName} 失败 (0x{Marshal.GetLastWin32Error():X})");
            return;
        }

        try
        {
            if (PawnIoNative.pawnio_open(out IntPtr handle) != 0)
            {
                ConsoleLog.Error($"    pawnio_open 失败，请以管理员身份运行");
                return;
            }

            try
            {
                string binPath = Path.Combine(AppContext.BaseDirectory, "Drivers", "PawnIO", "AMDFamily17.bin");
                byte[] blob = File.ReadAllBytes(binPath);
                if (PawnIoNative.pawnio_load(handle, blob, (UIntPtr)blob.Length) != 0)
                {
                    ConsoleLog.Error("    加载 AMDFamily17.bin 失败");
                    return;
                }

                uint[] msrs = [0xC0010293, 0xC0010071];
                foreach (uint msr in msrs)
                {
                    ConsoleLog.Log($"    MSR 0x{msr:X8}:");
                    for (int i = 0; i < 5; i++)
                    {
                        ulong raw = ReadMsrViaAmd17(handle, msr);
                        uint vid = (uint)((raw >> 6) & 0xFF);
                        double volts = 1.550 - vid * 0.00625;
                        ConsoleLog.Log($"      0x{raw:X16}  VID={vid,4} ({vid * 0.00625:F3}V) -> Vcore = {volts:F3} V");
                        Thread.Sleep(200);
                    }
                }
            }
            finally
            {
                PawnIoNative.pawnio_close(handle);
            }
        }
        finally
        {
            Kernel32Native.FreeLibrary(hModule);
        }
        ConsoleLog.Log("");
    }

    public static void DumpVidPerCore()
    {
        ConsoleLog.Log(">>> 各逻辑核心 FIDVID_STATUS 电压 (设置线程亲和性后读取) ...");
        if (!TryOpenAmd17Executor(out IntPtr executor))
            return;

        try
        {
            int coreCount = Environment.ProcessorCount;
            for (int core = 0; core < coreCount; core++)
            {
                IntPtr mask = new IntPtr(1L << core);
                IntPtr prev = Kernel32Native.SetThreadAffinityMask(Kernel32Native.GetCurrentThread(), mask);
                if (prev == IntPtr.Zero)
                    continue;

                ulong raw = ReadMsrViaAmd17(executor, 0xC0010293);
                Kernel32Native.SetThreadAffinityMask(Kernel32Native.GetCurrentThread(), prev);

                uint vid = (uint)((raw >> 6) & 0xFF);
                double volts = 1.550 - vid * 0.00625;
                ConsoleLog.Log($"    Core {core,2}: VID={vid,3} -> {volts:F3} V   (0x{raw:X12})");
                Thread.Sleep(50);
            }
        }
        finally
        {
            PawnIoNative.pawnio_close(executor);
        }
        ConsoleLog.Log("");
    }

    // 打开 AMDFamily17.bin executor（含 ioctl_read_msr），返回 executor 句柄；失败返回 false
    private static bool TryOpenAmd17Executor(out IntPtr executor)
    {
        executor = IntPtr.Zero;

        string dllPath = Path.Combine(AppContext.BaseDirectory, "PawnIOLib.dll");
        if (!File.Exists(dllPath))
            dllPath = Path.Combine(@"C:\Program Files\PawnIO", "PawnIOLib.dll");
        if (!File.Exists(dllPath))
        {
            ConsoleLog.Error($"    未找到 PawnIOLib.dll，请确认 PawnIO 已安装");
            return false;
        }

        // 仅用于验证实验；句柄由进程退出时释放，测试工具无需严格清理
        Kernel32Native.LoadLibrary(dllPath);

        if (PawnIoNative.pawnio_open(out executor) != 0)
        {
            ConsoleLog.Error($"    pawnio_open 失败，请以管理员身份运行");
            return false;
        }

        string binPath = Path.Combine(AppContext.BaseDirectory, "Drivers", "PawnIO", "AMDFamily17.bin");
        byte[] blob = File.ReadAllBytes(binPath);
        if (PawnIoNative.pawnio_load(executor, blob, (UIntPtr)blob.Length) != 0)
        {
            ConsoleLog.Error("    加载 AMDFamily17.bin 失败");
            PawnIoNative.pawnio_close(executor);
            executor = IntPtr.Zero;
            return false;
        }

        return true;
    }

    private static ulong ReadMsrViaAmd17(IntPtr executor, uint msrIndex)
    {
        ulong[] input = [msrIndex];
        var output = new ulong[1];
        PawnIoNative.pawnio_execute(executor, "ioctl_read_msr", input, (UIntPtr)1, output, (UIntPtr)1, out _);
        return output[0];
    }
}
