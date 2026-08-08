using System.ComponentModel;
using System.Runtime.InteropServices;
using JiaoLongControl.Server.Core.Native;

namespace JiaoLongControl.Server.Core.Utils;

public class DriverLoader
{
    const uint SC_MANAGER_ALL_ACCESS = 0xF003F;
    const uint SERVICE_ALL_ACCESS = 0xF01FF;
    const uint SERVICE_KERNEL_DRIVER = 0x00000001;
    const uint SERVICE_DEMAND_START = 0x00000003;
    const uint SERVICE_ERROR_NORMAL = 0x00000001;
    const uint SERVICE_CONTROL_STOP = 0x00000001;
    const uint SERVICE_NO_CHANGE = 0xFFFFFFFF; 
    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ChangeServiceConfig(
        IntPtr hService,
        uint dwServiceType,
        uint dwStartType,
        uint dwErrorControl,
        string lpBinaryPathName,
        string lpLoadOrderGroup,
        IntPtr lpdwTagId,
        string lpDependencies,
        string lpServiceStartName,
        string lpPassword,
        string lpDisplayName);

    public static void LoadDriver(string serviceName, string sysPath)
    {
        IntPtr scmHandle = Advapi32.OpenSCManager(null, null, SC_MANAGER_ALL_ACCESS);
        if (scmHandle == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error());
        try
        {
            IntPtr serviceHandle = Advapi32.OpenService(scmHandle, serviceName, SERVICE_ALL_ACCESS);
            if (serviceHandle != IntPtr.Zero)
            {
                // 服务已存在：尝试将启动类型改为「手动」并解除可能的禁用状态。
                // 失败不阻塞后续 StartService（若服务本就在运行，StartService 会返回 1056 视为成功；
                // 若服务被禁用，StartService 返回 1058，由下方分支给出明确提示）。
                ChangeServiceConfig(
                    serviceHandle,
                    SERVICE_NO_CHANGE,
                    SERVICE_DEMAND_START,
                    SERVICE_NO_CHANGE,
                    null, null, IntPtr.Zero, null, null, null, null
                );
            }
            else
            {
                serviceHandle = Advapi32.CreateService(
                    scmHandle,
                    serviceName,
                    serviceName,
                    SERVICE_ALL_ACCESS,
                    SERVICE_KERNEL_DRIVER,
                    SERVICE_DEMAND_START,
                    SERVICE_ERROR_NORMAL,
                    sysPath,
                    null,
                    IntPtr.Zero,
                    null,
                    null,
                    null
                );
                if (serviceHandle == IntPtr.Zero)
                    throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            try
            {
                if (!Advapi32.StartService(serviceHandle, 0, null))
                {
                    int err = Marshal.GetLastWin32Error();

                    if (err == 1056)
                    {
                        // 已经在运行
                        return;
                    }
                    if (err == 1275)
                    {
                        throw new Exception("Driver signature verification failed. Please check if Windows 'Core Isolation / Memory Integrity' is blocking the driver.");
                    }
                    if (err == 1058)
                    {
                        // 服务被禁用：启动类型已在上方改为「手动」，重试一次
                        if (Advapi32.StartService(serviceHandle, 0, null))
                            return;
                        throw new Exception($"驱动服务 {serviceName} 被系统禁用，请在「服务」管理器中将其启动类型改为“手动”并启动，或检查安全软件设置。");
                    }
                    throw new Win32Exception(err);
                }
            }
            finally
            {
                Advapi32.CloseServiceHandle(serviceHandle);
            }
        }
        finally
        {
            Advapi32.CloseServiceHandle(scmHandle);
        }
    }

    public static void UnloadDriver(string serviceName)
    {
        IntPtr scmHandle = Advapi32.OpenSCManager(null, null, SC_MANAGER_ALL_ACCESS);
        if (scmHandle == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error());

        try
        {
            IntPtr serviceHandle = Advapi32.OpenService(scmHandle, serviceName, SERVICE_ALL_ACCESS);
            if (serviceHandle != IntPtr.Zero)
            {
                try
                {
                    Advapi32.SERVICE_STATUS status = new Advapi32.SERVICE_STATUS();
                    Advapi32.ControlService(serviceHandle, SERVICE_CONTROL_STOP, ref status);
                    Advapi32.DeleteService(serviceHandle);
                }
                finally
                {
                    Advapi32.CloseServiceHandle(serviceHandle);
                }
            }
        }
        finally
        {
            Advapi32.CloseServiceHandle(scmHandle);
        }
    }
}