using System.Runtime.InteropServices;
using RyzenSmuControllerTest.Infrastructure;
using RyzenSmuControllerTest.Infrastructure.Native;

namespace RyzenSmuControllerTest.Diagnostics;

/// <summary>
/// 通过 MSR 直接读取 VID 电压（--cpu-vid / --vid-per-core）。
/// CPU-Z 读核心电压的机制：通过 MSR 读当前 VID 再换算 (V = 1.550 - VID * 0.00625)。
/// 候选寄存器：0xC0010293 (FIDVID_STATUS, VID=[15:6])、0xC0010071 (COFVID_STATUS)。
/// 刻意不走主控制器：复制一份 PawnIO 加载与 P/Invoke 逻辑，单独验证底层读取链路。
/// 模块文件与主工程相同（AMDFamily17.bin，由 csproj 链接）。
/// </summary>
internal static class CpuVidDumper
{
    private const uint MsrFidvidStatus = 0xC0010293;
    private const int MaxAffinityCores = 64;

    public static bool DumpCurrentVidFromMsr()
    {
        ConsoleLog.Log(">>> 通过 MSR 读取当前 VID (CPU-Z 机制) ...");
        if (!TryOpenAmd17Executor(out IntPtr executor))
            return false;

        try
        {
            foreach (uint msr in new uint[] { MsrFidvidStatus, 0xC0010071 })
            {
                ConsoleLog.Log($"    MSR 0x{msr:X8}:");
                for (int i = 0; i < 5; i++)
                {
                    if (!TryReadMsrViaAmd17(executor, msr, out ulong raw))
                    {
                        ConsoleLog.Error($"      读取 MSR 0x{msr:X8} 失败");
                        return false;
                    }

                    uint vid = (uint)((raw >> 6) & 0xFF);
                    double volts = 1.550 - vid * 0.00625;
                    ConsoleLog.Log($"      0x{raw:X16}  VID={vid,4} -> Vcore = {volts:F3} V");
                    Thread.Sleep(200);
                }
            }
        }
        catch (Exception ex)
        {
            ConsoleLog.Error($"    读取失败: {ex.Message}");
            return false;
        }
        finally
        {
            PawnIoNative.pawnio_close(executor);
            ConsoleLog.Log("");
        }

        return true;
    }

    public static bool DumpVidPerCore()
    {
        ConsoleLog.Log(">>> 各逻辑核心 FIDVID_STATUS 电压 (设置线程亲和性后读取) ...");
        if (!TryOpenAmd17Executor(out IntPtr executor))
            return false;

        bool anySucceeded = false;
        try
        {
            int coreCount = Math.Min(Environment.ProcessorCount, MaxAffinityCores);
            for (int core = 0; core < coreCount; core++)
            {
                IntPtr mask = new IntPtr(1L << core);
                IntPtr prev = Kernel32Native.SetThreadAffinityMask(Kernel32Native.GetCurrentThread(), mask);
                if (prev == IntPtr.Zero)
                    continue;

                try
                {
                    if (TryReadMsrViaAmd17(executor, MsrFidvidStatus, out ulong raw))
                    {
                        uint vid = (uint)((raw >> 6) & 0xFF);
                        double volts = 1.550 - vid * 0.00625;
                        ConsoleLog.Log($"    Core {core,2}: VID={vid,3} -> {volts:F3} V   (0x{raw:X12})");
                        anySucceeded = true;
                    }
                    else
                    {
                        ConsoleLog.Error($"    Core {core,2}: 读取失败");
                    }
                }
                finally
                {
                    Kernel32Native.SetThreadAffinityMask(Kernel32Native.GetCurrentThread(), prev);
                }

                Thread.Sleep(50);
            }
        }
        catch (Exception ex)
        {
            ConsoleLog.Error($"    读取失败: {ex.Message}");
            return false;
        }
        finally
        {
            PawnIoNative.pawnio_close(executor);
            ConsoleLog.Log("");
        }

        return anySucceeded;
    }

    private static bool TryOpenAmd17Executor(out IntPtr executor)
    {
        executor = IntPtr.Zero;

        const string dllName = "PawnIOLib.dll";
        string dllPath = Path.Combine(AppContext.BaseDirectory, dllName);
        if (!File.Exists(dllPath))
            dllPath = Path.Combine(@"C:\Program Files\PawnIO", dllName);
        if (!File.Exists(dllPath))
        {
            ConsoleLog.Error($"    未找到 {dllName}，请确认 PawnIO 已安装");
            return false;
        }

        if (Kernel32Native.LoadLibrary(dllPath) == IntPtr.Zero)
        {
            ConsoleLog.Error($"    加载 {dllName} 失败 (0x{Marshal.GetLastWin32Error():X})");
            return false;
        }

        if (PawnIoNative.pawnio_open(out executor) != 0)
        {
            ConsoleLog.Error("    pawnio_open 失败，请以管理员身份运行");
            return false;
        }

        string binPath = Path.Combine(AppContext.BaseDirectory, "Drivers", "PawnIO", "AMDFamily17.bin");
        try
        {
            byte[] blob = File.ReadAllBytes(binPath);
            if (PawnIoNative.pawnio_load(executor, blob, (UIntPtr)blob.Length) != 0)
            {
                ConsoleLog.Error("    加载 AMDFamily17.bin 失败");
                PawnIoNative.pawnio_close(executor);
                executor = IntPtr.Zero;
                return false;
            }
        }
        catch (Exception ex)
        {
            ConsoleLog.Error($"    读取 AMDFamily17.bin 失败: {ex.Message}");
            PawnIoNative.pawnio_close(executor);
            executor = IntPtr.Zero;
            return false;
        }

        return true;
    }

    private static bool TryReadMsrViaAmd17(IntPtr executor, uint msrIndex, out ulong value)
    {
        value = 0;
        ulong[] input = [msrIndex];
        var output = new ulong[1];
        int status = PawnIoNative.pawnio_execute(
            executor, "ioctl_read_msr", input, (UIntPtr)1, output, (UIntPtr)1, out UIntPtr returnSize);
        if (status != 0 || returnSize != (UIntPtr)1)
            return false;
        value = output[0];
        return true;
    }
}
