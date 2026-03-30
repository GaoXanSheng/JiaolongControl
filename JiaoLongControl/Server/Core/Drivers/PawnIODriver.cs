using System.IO;
using System.Runtime.InteropServices;
using JiaoLongControl.Server.Core.Utils;

namespace JiaoLongControl.Server.Core.Drivers;

public class PawnIODriver : IDisposable
{
    private IntPtr _executorHandle = IntPtr.Zero;
    private IntPtr _dllHandle = IntPtr.Zero;

    [DllImport("PawnIOLib.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern int pawnio_open(out IntPtr handle);

    [DllImport("PawnIOLib.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern int pawnio_load(IntPtr handle, byte[] blob, UIntPtr size);

    [DllImport("PawnIOLib.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern int pawnio_execute(
        IntPtr handle,
        [MarshalAs(UnmanagedType.LPStr)] string name,
        ulong[] input,
        UIntPtr inSize,
        ulong[] output,
        UIntPtr outSize,
        out UIntPtr returnSize
    );

    [DllImport("PawnIOLib.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern int pawnio_close(IntPtr handle);

    const string SysName = "PawnIO.sys";
    const string DllName = "PawnIOLib.dll";
    private const string ScriptBlobName = "RyzenSMU.bin";
    private const string ServiceName = "PawnIO";

    public PawnIODriver()
    {
        string resourceBase = "JiaoLongControl.Server.Resources.Drivers";
        string fullDllPath = EmbeddedResourceHelper.ExtractResourceToExeDir($"{resourceBase}.{DllName}", DllName);
        string fullSysPath = EmbeddedResourceHelper.ExtractResourceToExeDir($"{resourceBase}.{SysName}", SysName);
        string scriptBlobPath =
            EmbeddedResourceHelper.ExtractResourceToExeDir($"{resourceBase}.{ScriptBlobName}", ScriptBlobName);
        if ((File.Exists(fullSysPath) && File.Exists(fullDllPath) && File.Exists(scriptBlobPath)) == false)
        {
            throw new FileNotFoundException($"No Driver File Found: {fullSysPath}");
        }

        _dllHandle = Kernel32.LoadLibrary(fullDllPath);
        if (_dllHandle == IntPtr.Zero)
        {
            int err = Marshal.GetLastWin32Error();
            throw new Exception($"Unable Load DLLs ({DllName})。ErrorCode: {err}");
        }

        DriverLoader.LoadDriver(ServiceName, fullSysPath);
        if (pawnio_open(out _executorHandle) != 0) throw new Exception("PawnIO 驱动打开失败，请检查驱动是否已安装及权限。");

        byte[] blobData = File.ReadAllBytes(scriptBlobPath);
        if (pawnio_load(_executorHandle, blobData, (UIntPtr)blobData.Length) != 0)
            throw new Exception("PawnIO 脚本加载失败。");
    }

    public ulong[] Execute(string functionName, ulong[] inputs, int expectedOutputCount)
    {
        ulong[] outputs = new ulong[expectedOutputCount];
        int result = pawnio_execute(
            _executorHandle,
            functionName,
            inputs,
            (UIntPtr)inputs.Length,
            outputs,
            (UIntPtr)outputs.Length,
            out _
        );

        if (result != 0) throw new Exception($"执行 {functionName} 失败，错误码: {result:X}");
        return outputs;
    }

    public void Dispose()
    {
        if (_executorHandle != IntPtr.Zero) pawnio_close(_executorHandle);
        if (_dllHandle != IntPtr.Zero) Kernel32.FreeLibrary(_dllHandle);
        DriverLoader.UnloadDriver(ServiceName);
    }
}