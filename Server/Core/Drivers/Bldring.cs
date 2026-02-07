using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.ComponentModel;
using JiaoLongControl.Server.Core.Models;
using JiaoLongControl.Server.Core.Utils;

namespace JiaoLongControl.Server.Core.Drivers
{
    public class BDLDriver : IDisposable
    {
        private const string DllFileName = "JiaoLongDriver64.dll";
        private const string SysFileName = "JiaoLongDriver64.sys";
        private const string ServiceName = "JiaoLongDriver64";

        private readonly object _ioLock = new object();
        private IntPtr _dllHandle = IntPtr.Zero;
        public bool State { get; private set; }

        [DllImport(DllFileName, EntryPoint = "InitializeBldring", CallingConvention = CallingConvention.StdCall)]
        private static extern bool InitializeBldring();

        [DllImport(DllFileName, EntryPoint = "ShutdownBldring", CallingConvention = CallingConvention.StdCall)]
        private static extern void ShutdownBldring();

        [DllImport(DllFileName, EntryPoint = "GetBLDPortVal", CallingConvention = CallingConvention.StdCall)]
        private static extern bool GetBLDPortVal(ushort wPortAddr, ref byte pdwPortVal, byte bSize);

        [DllImport(DllFileName, EntryPoint = "SetBLDPortVal", CallingConvention = CallingConvention.StdCall)]
        private static extern bool SetBLDPortVal(ushort wPortAddr, byte dwPortVal, byte bSize);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FreeLibrary(IntPtr hModule);

        public BDLDriver()
        {
            if (!IsAdministrator())
                throw new UnauthorizedAccessException("初始化失败：权限不足");

            try
            {
                string resourceBase = "JiaoLongControl.Server.Resources.Drivers";
                string fullDllPath =
                    EmbeddedResourceHelper.ExtractResourceToExeDir($"{resourceBase}.{DllFileName}", DllFileName);
                string fullSysPath =
                    EmbeddedResourceHelper.ExtractResourceToExeDir($"{resourceBase}.{SysFileName}", SysFileName);

                if (!File.Exists(fullSysPath))
                    throw new FileNotFoundException($"未找到驱动文件: {fullSysPath}");

                _dllHandle = LoadLibrary(fullDllPath);
                if (_dllHandle == IntPtr.Zero)
                {
                    int err = Marshal.GetLastWin32Error();
                    throw new Exception($"无法加载 DLL ({DllFileName})。错误码: {err}。请检查是否安装了 VC++ 运行时库。");
                }

                DriverLoader.LoadDriver(ServiceName, fullSysPath);

                State = InitializeBldring();

                if (!State)
                {
                    throw new Exception("DLL初始化失败");
                }
            }
            catch (Exception ex)
            {
                Dispose();
                throw new Exception($"驱动初始化异常: {ex.Message}", ex);
            }
        }

        protected bool EC_RAM_WRITE(ushort iIndex, byte data)
        {
            lock (_ioLock)
            {
                byte highByte = (byte)(iIndex >> 8);
                byte lowByte = (byte)(iIndex & 0xFF);

                if (!WRITE_PORT(ECMemoryTable.EC_ADDR_PORT, 0x2E)) return false;
                if (!WRITE_PORT(ECMemoryTable.EC_DATA_PORT, 0x11)) return false;
                if (!WRITE_PORT(ECMemoryTable.EC_ADDR_PORT, 0x2F)) return false;
                if (!WRITE_PORT(ECMemoryTable.EC_DATA_PORT, highByte)) return false;

                if (!WRITE_PORT(ECMemoryTable.EC_ADDR_PORT, 0x2E)) return false;
                if (!WRITE_PORT(ECMemoryTable.EC_DATA_PORT, 0x10)) return false;
                if (!WRITE_PORT(ECMemoryTable.EC_ADDR_PORT, 0x2F)) return false;
                if (!WRITE_PORT(ECMemoryTable.EC_DATA_PORT, lowByte)) return false;

                if (!WRITE_PORT(ECMemoryTable.EC_ADDR_PORT, 0x2E)) return false;
                if (!WRITE_PORT(ECMemoryTable.EC_DATA_PORT, 0x12)) return false;
                if (!WRITE_PORT(ECMemoryTable.EC_ADDR_PORT, 0x2F)) return false;

                return WRITE_PORT(ECMemoryTable.EC_DATA_PORT, data);
            }
        }

        protected byte EC_RAM_READ(ushort iIndex)
        {
            lock (_ioLock)
            {
                byte highByte = (byte)(iIndex >> 8);
                byte lowByte = (byte)(iIndex & 0xFF);

                WRITE_PORT(ECMemoryTable.EC_ADDR_PORT, 0x2E);
                WRITE_PORT(ECMemoryTable.EC_DATA_PORT, 0x11);
                WRITE_PORT(ECMemoryTable.EC_ADDR_PORT, 0x2F);
                WRITE_PORT(ECMemoryTable.EC_DATA_PORT, highByte);

                WRITE_PORT(ECMemoryTable.EC_ADDR_PORT, 0x2E);
                WRITE_PORT(ECMemoryTable.EC_DATA_PORT, 0x10);
                WRITE_PORT(ECMemoryTable.EC_ADDR_PORT, 0x2F);
                WRITE_PORT(ECMemoryTable.EC_DATA_PORT, lowByte);

                WRITE_PORT(ECMemoryTable.EC_ADDR_PORT, 0x2E);
                WRITE_PORT(ECMemoryTable.EC_DATA_PORT, 0x12);
                WRITE_PORT(ECMemoryTable.EC_ADDR_PORT, 0x2F);

                return READ_PORT(ECMemoryTable.EC_DATA_PORT);
            }
        }

        private bool WRITE_PORT(ushort wPortAddr, byte dwPortVal)
        {
            return SetBLDPortVal(wPortAddr, dwPortVal, 1);
        }

        private byte READ_PORT(ushort wPortAddr)
        {
            byte data = 0;
            GetBLDPortVal(wPortAddr, ref data, 1);
            return data;
        }

        public void Dispose()
        {
            if (State)
            {
                try
                {
                    ShutdownBldring();
                }
                catch
                {
                    
                }

                State = false;
            }

            if (_dllHandle != IntPtr.Zero)
            {
                FreeLibrary(_dllHandle);
                _dllHandle = IntPtr.Zero;
            }

            try
            {
                DriverLoader.UnloadDriver(ServiceName);
            }
            catch
            {
            }

            GC.SuppressFinalize(this);
        }

        ~BDLDriver()
        {
            Dispose();
        }

        private bool IsAdministrator()
        {
            using (var identity = WindowsIdentity.GetCurrent())
            {
                return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        private static class DriverLoader
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
                if (scmHandle == IntPtr.Zero) throw new Win32Exception();

                try
                {
                    IntPtr serviceHandle = CreateService(scmHandle, serviceName, serviceName, SERVICE_ALL_ACCESS,
                        SERVICE_KERNEL_DRIVER, SERVICE_DEMAND_START, SERVICE_ERROR_NORMAL, sysPath, null, IntPtr.Zero,
                        null, null, null);

                    if (serviceHandle == IntPtr.Zero)
                    {
                        if (Marshal.GetLastWin32Error() == 1073)
                        {
                            serviceHandle = OpenService(scmHandle, serviceName, SERVICE_ALL_ACCESS);
                            if (serviceHandle == IntPtr.Zero) throw new Win32Exception();
                        }
                        else
                        {
                            throw new Win32Exception();
                        }
                    }

                    try
                    {
                        if (!StartService(serviceHandle, 0, null))
                        {
                            int err = Marshal.GetLastWin32Error();
                            if (err == 1056) return;
                            if (err == 1275) throw new Exception("数字签名验证失败");
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
    }
}