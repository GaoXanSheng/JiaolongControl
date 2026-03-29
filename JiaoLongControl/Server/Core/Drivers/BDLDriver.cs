using System.IO;
using System.Runtime.InteropServices;
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

        public BDLDriver()
        {
            try
            {
                string resourceBase = "JiaoLongControl.Server.Resources.Drivers";
                string fullDllPath =
                    EmbeddedResourceHelper.ExtractResourceToExeDir($"{resourceBase}.{DllFileName}", DllFileName);
                string fullSysPath =
                    EmbeddedResourceHelper.ExtractResourceToExeDir($"{resourceBase}.{SysFileName}", SysFileName);

                if (!File.Exists(fullSysPath))
                    throw new FileNotFoundException($"No Driver File Found: {fullSysPath}");

                _dllHandle = Kernel32.LoadLibrary(fullDllPath);
                if (_dllHandle == IntPtr.Zero)
                {
                    int err = Marshal.GetLastWin32Error();
                    throw new Exception($"Unable Load DLLs ({DllFileName})。ErrorCode: {err}");
                }

                DriverLoader.LoadDriver(ServiceName, fullSysPath);

                State = InitializeBldring();

                if (!State)
                {
                    throw new Exception("DLL Initialization Failed");
                }
            }
            catch (Exception ex)
            {
                Dispose();
                throw new Exception($"Driver Initialization Exception: {ex.Message}", ex);
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

            GC.SuppressFinalize(this);
        }

        ~BDLDriver()
        {
            Dispose();
        }
    }
}