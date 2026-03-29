using System.ComponentModel;
using System.Runtime.InteropServices;

namespace JiaoLongControl.Server.Core.Utils;

public class DriverLoader
{
    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    static extern IntPtr OpenSCManager(string lpMachineName, string lpDatabaseName, uint dwDesiredAccess);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    static extern IntPtr CreateService(IntPtr hSCManager, string lpServiceName, string lpDisplayName,
        uint dwDesiredAccess, uint dwServiceType, uint dwStartType, uint dwErrorControl,
        string lpBinaryPathName, string lpLoadOrderGroup, IntPtr lpdwTagId, string lpDependencies,
        string lpServiceStartName, string lpPassword);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    static extern IntPtr OpenService(IntPtr hSCManager, string lpServiceName, uint dwDesiredAccess);

    [DllImport("advapi32.dll", SetLastError = true)]
    static extern bool StartService(IntPtr hService, uint dwNumServiceArgs, string[] lpServiceArgVectors);

    [DllImport("advapi32.dll", SetLastError = true)]
    static extern bool ControlService(IntPtr hService, uint dwControl, ref SERVICE_STATUS lpServiceStatus);

    [DllImport("advapi32.dll", SetLastError = true)]
    static extern bool DeleteService(IntPtr hService);

    [DllImport("advapi32.dll", SetLastError = true)]
    static extern bool CloseServiceHandle(IntPtr hSCObject);

    [StructLayout(LayoutKind.Sequential)]
    struct SERVICE_STATUS
    {
        public uint dwServiceType;
        public uint dwCurrentState;
        public uint dwControlsAccepted;
        public uint dwWin32ExitCode;
        public uint dwServiceSpecificExitCode;
        public uint dwCheckPoint;
        public uint dwWaitHint;
    }

    const uint SC_MANAGER_ALL_ACCESS = 0xF003F;
    const uint SERVICE_ALL_ACCESS = 0xF01FF;
    const uint SERVICE_KERNEL_DRIVER = 0x00000001;
    const uint SERVICE_DEMAND_START = 0x00000003;
    const uint SERVICE_ERROR_NORMAL = 0x00000001;
    const uint SERVICE_CONTROL_STOP = 0x00000001;

    public static void LoadDriver(string serviceName, string sysPath)
    {
        IntPtr scmHandle = OpenSCManager(null, null, SC_MANAGER_ALL_ACCESS);
        if (scmHandle == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error());

        try
        {
            IntPtr serviceHandle = OpenService(scmHandle, serviceName, SERVICE_ALL_ACCESS);

            if (serviceHandle == IntPtr.Zero)
            {
                serviceHandle = CreateService(
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
                if (!StartService(serviceHandle, 0, null))
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
                CloseServiceHandle(serviceHandle);
            }
        }
        finally
        {
            CloseServiceHandle(scmHandle);
        }
    }

    public static void UnloadDriver(string serviceName)
    {
        IntPtr scmHandle = OpenSCManager(null, null, SC_MANAGER_ALL_ACCESS);
        if (scmHandle == IntPtr.Zero) return;

        try
        {
            IntPtr serviceHandle = OpenService(scmHandle, serviceName, SERVICE_ALL_ACCESS);
            if (serviceHandle != IntPtr.Zero)
            {
                try
                {
                    SERVICE_STATUS status = new SERVICE_STATUS();
                    ControlService(serviceHandle, SERVICE_CONTROL_STOP, ref status);
                    DeleteService(serviceHandle);
                }
                finally
                {
                    CloseServiceHandle(serviceHandle);
                }
            }
        }
        finally
        {
            CloseServiceHandle(scmHandle);
        }
    }
}