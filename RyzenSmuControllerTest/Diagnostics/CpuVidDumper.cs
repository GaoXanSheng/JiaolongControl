using JiaoLongControl.Server.Core.Drivers;
using JiaoLongControl.Server.Core.Native;
using RyzenSmuControllerTest.Infrastructure;

namespace RyzenSmuControllerTest.Diagnostics;

/// <summary>
/// 通过 MSR 直接读取 VID 电压（--cpu-vid / --vid-per-core）。
/// CPU-Z 读核心电压的机制：通过 MSR 读当前 VID 再换算 (V = 1.550 - VID * 0.00625)。
/// 候选寄存器：0xC0010293 (FIDVID_STATUS, VID=[15:6])、0xC0010071 (COFVID_STATUS)。
/// 复用主工程链接进来的 PawnIO.ReadMsr（AMDFamily17.bin 加载链路），不在本项目重复 P/Invoke；
/// 线程亲和性同样复用主工程 Kernel32。
/// </summary>
internal static class CpuVidDumper
{
    private const uint MsrFidvidStatus = 0xC0010293;
    private const int MaxAffinityCores = 64;

    /// <summary>暴露 PawnIO 的受保护构造，供 MSR 读取路径直接实例化。</summary>
    private sealed class MsrPawnIO : PawnIO
    {
        public MsrPawnIO() { }
    }

    public static bool DumpCurrentVidFromMsr()
    {
        ConsoleLog.Log(">>> 通过 MSR 读取当前 VID (CPU-Z 机制) ...");
        using var pawn = new MsrPawnIO();
        try
        {
            foreach (uint msr in new uint[] { MsrFidvidStatus, 0xC0010071 })
            {
                ConsoleLog.Log($"    MSR 0x{msr:X8}:");
                for (int i = 0; i < 5; i++)
                {
                    ulong raw = pawn.ReadMsr(msr);
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
        ConsoleLog.Log("");
        return true;
    }

    public static bool DumpVidPerCore()
    {
        ConsoleLog.Log(">>> 各逻辑核心 FIDVID_STATUS 电压 (设置线程亲和性后读取) ...");
        using var pawn = new MsrPawnIO();
        bool anySucceeded = false;
        try
        {
            int coreCount = Math.Min(Environment.ProcessorCount, MaxAffinityCores);
            for (int core = 0; core < coreCount; core++)
            {
                IntPtr mask = new IntPtr(1L << core);
                IntPtr prev = Kernel32.SetThreadAffinityMask(Kernel32.GetCurrentThread(), mask);
                if (prev == IntPtr.Zero)
                    continue;

                try
                {
                    ulong raw = pawn.ReadMsr(MsrFidvidStatus);
                    uint vid = (uint)((raw >> 6) & 0xFF);
                    double volts = 1.550 - vid * 0.00625;
                    ConsoleLog.Log($"    Core {core,2}: VID={vid,3} -> {volts:F3} V   (0x{raw:X12})");
                    anySucceeded = true;
                }
                finally
                {
                    Kernel32.SetThreadAffinityMask(Kernel32.GetCurrentThread(), prev);
                }

                Thread.Sleep(50);
            }
        }
        catch (Exception ex)
        {
            ConsoleLog.Error($"    读取失败: {ex.Message}");
            return false;
        }
        ConsoleLog.Log("");
        return anySucceeded;
    }
}
