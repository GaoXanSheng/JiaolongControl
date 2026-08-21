using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;
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
    private const string Amd17BlobName = "AMDFamily17.bin";
    private const string PawnIOUrl = "https://pawnio.eu/";

    private readonly object _initLock = new();
    private readonly object _executeLock = new();
    private IntPtr _dllHandle = IntPtr.Zero;
    private IntPtr _executorHandle = IntPtr.Zero;
    private IntPtr _amd17ExecutorHandle = IntPtr.Zero;
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

    /// <summary>
    /// 读取指定 MSR 寄存器。由 AMDFamily17.bin 模块提供 ioctl_read_msr，不依赖 RyzenSMU.bin。
    /// </summary>
    public ulong ReadMsr(uint msrIndex)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(PawnIO));

        lock (_initLock)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(PawnIO));

            EnsureDriver();

            if (_amd17ExecutorHandle == IntPtr.Zero)
            {
                string amd17Path = Path.Combine(AppContext.BaseDirectory, "Drivers", "PawnIO", Amd17BlobName);
                if (!File.Exists(amd17Path))
                    throw new FileNotFoundException($"缺少 {Amd17BlobName} 脚本文件，请检查安装是否完整");

                if (pawnio_open(out _amd17ExecutorHandle) != 0)
                    throw new Exception("未检测到 PawnIO 驱动服务。请从 https://pawnio.eu/ 下载安装 PawnIO 后重启应用。");

                byte[] blobData = File.ReadAllBytes(amd17Path);
                if (pawnio_load(_amd17ExecutorHandle, blobData, (UIntPtr)blobData.Length) != 0)
                    throw new Exception("加载 AMDFamily17 脚本失败");
            }
        }

        lock (_executeLock)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(PawnIO));

            ulong[] outputs = new ulong[1];
            int result = pawnio_execute(
                _amd17ExecutorHandle,
                "ioctl_read_msr",
                new ulong[] { msrIndex },
                (UIntPtr)1,
                outputs,
                (UIntPtr)1,
                out _
            );

            if (result != 0)
                throw new Exception($"Execute ioctl_read_msr failed, ErrorCode: 0x{result:X}");

            return outputs[0];
        }
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
    
    private void EnsureDriver()
    {
        if (_executorHandle != IntPtr.Zero)
            return;

        // 内核驱动由用户从官网（https://pawnio.eu/）安装，本软件只连接系统已运行的 PawnIO 服务

        // 按官方用例定位 PawnIOLib.dll：
        // 1) 应用目录下 Drivers\PawnIO\（兼容旧版部署/手动放置）；
        // 2) 注册表 Uninstall\PawnIO 的 InstallLocation（官方用例主路径）；
        // 3) 回退 %ProgramFiles%\PawnIO（官方安装器不允许修改安装路径）；
        // 4) 最后交给系统 DLL 搜索（仅当 PawnIO 目录已加入 PATH 等搜索路径时有效）。
        _dllHandle = LoadPawnIOLib(out _);

        if (_dllHandle == IntPtr.Zero)
        {
            int err = Marshal.GetLastWin32Error();
            throw new Exception($"未找到 {DllName}（ErrorCode: {err}）。请从 {PawnIOUrl} 下载安装 PawnIO 后重启应用。");
        }

        if (pawnio_open(out _executorHandle) != 0)
        {
            throw new Exception($"未检测到 PawnIO 驱动服务。请从 {PawnIOUrl} 下载安装 PawnIO 后重启应用。");
        }
    }

    private void InitCore()
    {
        EnsureDriver();

        string scriptPath = Path.Combine(AppContext.BaseDirectory, "Drivers", "PawnIO", ScriptBlobName);
        if (!File.Exists(scriptPath))
            throw new FileNotFoundException($"缺少 {ScriptBlobName} 脚本文件，请检查安装是否完整");

        byte[] blobData = File.ReadAllBytes(scriptPath);
        if (pawnio_load(_executorHandle, blobData, (UIntPtr)blobData.Length) != 0)
            throw new Exception("加载 RyzenSMU 脚本失败");
    }

    /// <summary>
    /// 按官方用例（https://github.com/namazso/PawnIO.Modules/wiki/Using-PawnIO-Modules）定位并加载 PawnIOLib.dll。
    /// 候选顺序：应用目录 Drivers\PawnIO → 注册表 InstallLocation → %ProgramFiles%\PawnIO → 系统 DLL 搜索。
    /// </summary>
    /// <param name="loadedFrom">实际加载成功的路径（失败时为空）。</param>
    private IntPtr LoadPawnIOLib(out string loadedFrom)
    {
        List<string> candidates = new()
        {
            Path.Combine(AppContext.BaseDirectory, "Drivers", "PawnIO", DllName),
        };

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PawnIO");
            string? installLocation = key?.GetValue("InstallLocation")?.ToString();
            if (!string.IsNullOrWhiteSpace(installLocation))
            {
                installLocation = installLocation.Trim().Trim('"');
                if (installLocation.Length > 0)
                    candidates.Add(Path.Combine(installLocation, DllName));
            }
        }
        catch
        {
        }

        candidates.Add(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "PawnIO",
            DllName));

        foreach (string path in candidates)
        {
            if (!File.Exists(path))
                continue;

            IntPtr handle = Kernel32.LoadLibrary(path);
            if (handle != IntPtr.Zero)
            {
                loadedFrom = path;
                return handle;
            }
        }

        // 兜底：交给系统 DLL 搜索路径（官网安装目录通常不在其中，仅个别环境有效）
        IntPtr fallback = Kernel32.LoadLibrary(DllName);
        loadedFrom = fallback != IntPtr.Zero ? DllName : string.Empty;
        return fallback;
    }

    private void CleanupHandles()
    {
        if (_amd17ExecutorHandle != IntPtr.Zero)
        {
            try
            {
                pawnio_close(_amd17ExecutorHandle);
            }
            catch
            {
            }

            _amd17ExecutorHandle = IntPtr.Zero;
        }

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
