using System.Runtime.InteropServices;

namespace RyzenSmuControllerTest.Infrastructure.Native;

/// <summary>
/// kernel32 P/Invoke，仅供 --cpu-vid / --vid-per-core 的独立验证路径使用。
/// 与主工程链接进来的 JiaoLongControl.Server.Core.Native.Kernel32 无关：那条路径刻意绕开主控制器。
/// </summary>
internal static class Kernel32Native
{
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr LoadLibrary(string lpFileName);

    [DllImport("kernel32.dll")]
    public static extern bool FreeLibrary(IntPtr hModule);

    [DllImport("kernel32.dll")]
    public static extern IntPtr GetCurrentThread();

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr SetThreadAffinityMask(IntPtr hThread, IntPtr dwThreadAffinityMask);
}
