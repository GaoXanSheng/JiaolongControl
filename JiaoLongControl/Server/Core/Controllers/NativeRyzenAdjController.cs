using System.Runtime.InteropServices;
using JiaoLongControl.Server.Core.Utils;

namespace JiaoLongControl.Server.Core.Controllers;

[Obsolete("测试")]
public class NativeRyzenAdjController
{
    public enum RyzenAdjError : int
    {
        Success = 0,
        FamUnsupported = -1, // ADJ_ERR_FAM_UNSUPPORTED
        SmuTimeout = -2, // ADJ_ERR_SMU_TIMEOUT
        SmuUnsupported = -3, // ADJ_ERR_SMU_UNSUPPORTED
        SmuRejected = -4, // ADJ_ERR_SMU_REJECTED
        MemoryAccess = -5 // ADJ_ERR_MEMORY_ACCESS
    }

    private const string DllName = "libryzenadj.dll";

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern IntPtr init_ryzenadj();

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern void cleanup_ryzenadj(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern int set_coall(IntPtr ry, uint value);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern int set_stapm_limit(IntPtr ry, uint value);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern int set_fast_limit(IntPtr ry, uint value);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern int set_slow_limit(IntPtr ry, uint value);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern int set_tctl_temp(IntPtr ry, uint value);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern int init_table(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern float get_tctl_temp_value(IntPtr ry);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern float get_stapm_value(IntPtr ry);

    private IntPtr _ryHandle = IntPtr.Zero;
    private bool _disposed = false;
    private IntPtr _dllHandle = IntPtr.Zero;

    public NativeRyzenAdjController()
    {
        string resourceBase = "JiaoLongControl.Server.Resources.Drivers";
        string fullDllPath = EmbeddedResourceHelper.ExtractResourceToExeDir($"{resourceBase}.{DllName}", DllName);
        _dllHandle = Kernel32.LoadLibrary(fullDllPath);
        if (_dllHandle == IntPtr.Zero)
        {
            int err = Marshal.GetLastWin32Error();
            throw new Exception($"Unable Load DLLs ({DllName})。ErrorCode: {err}");
        }

        _ryHandle = init_ryzenadj();
        if (_ryHandle == IntPtr.Zero)
        {
            throw new Exception("libryzenadj 初始化失败！指针返回 NULL。\n可能原因：1. 没用管理员权限运行 2. 缺少驱动 3. CPU 不支持。");
        }
    }

    public void SetCurveOptimizerAll(int offset)
    {
        if (offset > 0)
            throw new ArgumentException("Curve Optimizer 降压值必须 <= 0！");
        uint value = unchecked((uint)offset);
        int result = set_coall(_ryHandle, value);
        CheckResult(result, "Set Curve Optimizer All");
    }

    private void CheckResult(int statusCode, string actionName)
    {
        if (statusCode != (int)RyzenAdjError.Success)
        {
            RyzenAdjError error = (RyzenAdjError)statusCode;
            throw new Exception($"{actionName} 失败！错误原因: {error} (错误码: {statusCode})");
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (_dllHandle != IntPtr.Zero)
            {
                Kernel32.FreeLibrary(_dllHandle);
                _dllHandle = IntPtr.Zero;
            }

            if (_ryHandle != IntPtr.Zero)
            {
                cleanup_ryzenadj(_ryHandle);
                _ryHandle = IntPtr.Zero;
            }

            _disposed = true;
        }
    }

    ~NativeRyzenAdjController()
    {
        Dispose(false);
    }
}