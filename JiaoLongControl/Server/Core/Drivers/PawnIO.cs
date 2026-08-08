using System.IO;
using System.Runtime.InteropServices;
using JiaoLongControl.Server.Core.Native;

namespace JiaoLongControl.Server.Core.Drivers;

/// <summary>
/// PawnIO 客户端。
/// 本软件不再附带/管理 PawnIO 内核驱动（PawnIO.sys、PawnIOLib.dll 由用户从官网安装）：
/// 仅负责加载客户端库与 RyzenSMU 脚本，连接系统中已安装并运行的 PawnIO 驱动。
/// 官方下载：https://pawnio.eu/
/// </summary>
public class PawnIO : IDisposable
{
    private const string DllName = "PawnIOLib.dll";
    private const string ScriptBlobName = "RyzenSMU.bin";
    private const string PawnIOUrl = "https://pawnio.eu/";

    private readonly object _initLock = new();
    private readonly object _executeLock = new();
    private IntPtr _dllHandle = IntPtr.Zero;
    private IntPtr _executorHandle = IntPtr.Zero;
    private bool _disposed;

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

    /// <summary>
    /// 构造时不做任何加载操作，避免在应用启动阶段因驱动问题导致整个程序无法打开（白屏/闪退）。
    /// 驱动连接在第一次 Execute 时惰性初始化。
    /// </summary>
    protected PawnIO()
    {
    }

    public ulong[] Execute(string functionName, ulong[] inputs, int expectedOutputCount)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(PawnIO));

        EnsureInitialized();
        ulong[] outputs = new ulong[expectedOutputCount];
        lock (_executeLock)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(PawnIO));

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
        }

        return outputs;
    }

    private void EnsureInitialized()
    {
        if (IsInitialized)
            return;

        lock (_initLock)
        {
            if (IsInitialized)
                return;
            if (_disposed)
                throw new ObjectDisposedException(nameof(PawnIO));

            try
            {
                InitCore();
                IsInitialized = true;
            }
            catch (Exception ex)
            {
                CleanupHandles();
                throw;
            }
        }
    }

    private void InitCore()
    {
        // 内核驱动由用户从官网（https://pawnio.eu/）安装，本软件只连接系统已运行的 PawnIO 服务

        string scriptPath = Path.Combine(AppContext.BaseDirectory, "Drivers", "PawnIO", ScriptBlobName);
        if (!File.Exists(scriptPath))
            throw new FileNotFoundException($"缺少 {ScriptBlobName} 脚本文件，请检查安装是否完整");

        // 优先加载应用目录下的 PawnIOLib.dll（兼容旧版部署/手动放置），否则交给系统搜索（官网安装目录）
        string localDll = Path.Combine(AppContext.BaseDirectory, "Drivers", "PawnIO", DllName);
        _dllHandle = File.Exists(localDll)
            ? Kernel32.LoadLibrary(localDll)
            : Kernel32.LoadLibrary(DllName);

        if (_dllHandle == IntPtr.Zero)
        {
            int err = Marshal.GetLastWin32Error();
            throw new Exception($"未找到 {DllName}（ErrorCode: {err}）。请从 {PawnIOUrl} 下载安装 PawnIO 后重启应用。");
        }

        if (pawnio_open(out _executorHandle) != 0)
        {
            throw new Exception($"未检测到 PawnIO 驱动服务。请从 {PawnIOUrl} 下载安装 PawnIO 后重启应用。");
        }

        byte[] blobData = File.ReadAllBytes(scriptPath);
        if (pawnio_load(_executorHandle, blobData, (UIntPtr)blobData.Length) != 0)
            throw new Exception("加载 RyzenSMU 脚本失败");
    }

    private void CleanupHandles()
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
    }

    public void Dispose()
    {
        // 先取 _initLock 再取 _executeLock（与 Execute 路径锁序一致，无死锁）：
        // 确保与进行中的首次初始化互斥
        lock (_initLock)
        {
            lock (_executeLock)
            {
                if (_disposed)
                    return;
                _disposed = true;

                CleanupHandles();
            }
        }

        // 不再管理任何内核服务（驱动由官网安装包负责），无需卸载

        IsInitialized = false;
        GC.SuppressFinalize(this);
    }

    ~PawnIO() => Dispose();
}
