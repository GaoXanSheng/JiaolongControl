using System.IO;
using System.Runtime.InteropServices;
using JiaoLongControl.Server.Core.Native;
using JiaoLongControl.Server.Core.Utils;

namespace JiaoLongControl.Server.Core.Drivers;

public class PawnIO : IDisposable
{
    private const string DllName = "PawnIOLib.dll";
    private const string SysName = "PawnIO.sys";
    private const string ScriptBlobName = "RyzenSMU.bin";
    private const string ServiceName = "PawnIO";

    private IntPtr _dllHandle = IntPtr.Zero;
    private IntPtr _executorHandle = IntPtr.Zero;
    public bool IsInitialized { get; private set; }

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern int pawnio_open(out IntPtr handle);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern int pawnio_load(IntPtr handle, byte[] blob, UIntPtr size);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern int pawnio_execute(
        IntPtr handle,
        [MarshalAs(UnmanagedType.LPStr)] string name,
        ulong[] input,
        UIntPtr inSize,
        ulong[] output,
        UIntPtr outSize,
        out UIntPtr returnSize
    );

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern int pawnio_close(IntPtr handle);

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

        if (result != 0)
            throw new Exception($"Execute {functionName} failed, ErrorCode: 0x{result:X}");

        return outputs;
    }

    protected PawnIO()
    {
        try
        {
            string driverFolderPath = Path.Combine(AppContext.BaseDirectory, "Drivers", "PawnIO");
            string fullDllPath = Path.Combine(driverFolderPath, DllName);
            string fullSysPath = Path.Combine(driverFolderPath, SysName);
            string scriptBlobPath = Path.Combine(driverFolderPath, ScriptBlobName);

            if (!File.Exists(fullSysPath) || !File.Exists(fullDllPath) || !File.Exists(scriptBlobPath))
                throw new FileNotFoundException($"Driver files not found");

            _dllHandle = Kernel32.LoadLibrary(fullDllPath);
            if (_dllHandle == IntPtr.Zero)
            {
                int err = Marshal.GetLastWin32Error();
                throw new Exception($"Failed to load DLL ({DllName}), ErrorCode: {err}");
            }

            DriverLoader.LoadDriver(ServiceName, fullSysPath);

            if (pawnio_open(out _executorHandle) != 0)
                throw new Exception("Failed to open PawnIO driver");

            byte[] blobData = File.ReadAllBytes(scriptBlobPath);
            if (pawnio_load(_executorHandle, blobData, (UIntPtr)blobData.Length) != 0)
                throw new Exception("Failed to load PawnIO script");

            IsInitialized = true;
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        if (_executorHandle != IntPtr.Zero)
        {
            try
            {
                pawnio_close(_executorHandle);
            }
            catch
            {
            }

            _executorHandle = IntPtr.Zero;
        }

        if (_dllHandle != IntPtr.Zero)
        {
            Kernel32.FreeLibrary(_dllHandle);
            _dllHandle = IntPtr.Zero;
        }

        try
        {
            DriverLoader.UnloadDriver(ServiceName);
        }
        catch
        {
        }

        IsInitialized = false;
        GC.SuppressFinalize(this);
    }

    ~PawnIO() => Dispose();
}