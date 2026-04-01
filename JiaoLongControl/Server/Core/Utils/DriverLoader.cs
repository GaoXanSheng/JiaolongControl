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

    public static void LoadDriver(string serviceName, string sysPath)
    {
        IntPtr scmHandle = Advapi32.OpenSCManager(null, null, SC_MANAGER_ALL_ACCESS);
        if (scmHandle == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error());
        try
        {
            IntPtr serviceHandle = Advapi32.OpenService(scmHandle, serviceName, SERVICE_ALL_ACCESS);
            if (serviceHandle == IntPtr.Zero)
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
                        throw new Exception("Driver signature verification failed");
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
        if (scmHandle == IntPtr.Zero) return;

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