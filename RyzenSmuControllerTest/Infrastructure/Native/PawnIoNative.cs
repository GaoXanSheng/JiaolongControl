using System.Runtime.InteropServices;

namespace RyzenSmuControllerTest.Infrastructure.Native;

/// <summary>
/// PawnIOLib 裸 P/Invoke。仅 --cpu-vid / --vid-per-core 使用：直接加载 AMDFamily17.bin
/// 验证 MSR 读取机制（CPU-Z 机制），不经由主工程 PawnIO 包装（那条路径面向 RyzenSMU.bin 业务流）。
/// </summary>
internal static class PawnIoNative
{
    [DllImport("PawnIOLib.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern int pawnio_open(out IntPtr handle);

    [DllImport("PawnIOLib.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern int pawnio_load(IntPtr handle, byte[] blob, UIntPtr size);

    [DllImport("PawnIOLib.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern int pawnio_execute(
        IntPtr handle,
        [MarshalAs(UnmanagedType.LPStr)] string name,
        ulong[] input,
        UIntPtr inSize,
        ulong[] output,
        UIntPtr outSize,
        out UIntPtr returnSize
    );

    [DllImport("PawnIOLib.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern int pawnio_close(IntPtr handle);
}
