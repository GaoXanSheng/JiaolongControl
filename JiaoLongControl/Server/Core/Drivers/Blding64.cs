using System.IO;
using System.Runtime.InteropServices;
using JiaoLongControl.Server.Core.Models;
using JiaoLongControl.Server.Core.Native;
using JiaoLongControl.Server.Core.Utils;

namespace JiaoLongControl.Server.Core.Drivers;

public class Blding64 : IDisposable
{
    private const string DllFileName = "JiaoLongDriver64.dll";
    private const string SysFileName = "JiaoLongDriver64.sys";
    private const string ServiceName = "JiaoLongDriver64";
    private readonly object _ioLock = new();
    private IntPtr _dllHandle = IntPtr.Zero;
    public bool IsInitialized { get; set; }

    [DllImport(DllFileName, EntryPoint = "InitializeBldring", CallingConvention = CallingConvention.StdCall)]
    private static extern bool InitializeBldring();

    [DllImport(DllFileName, EntryPoint = "ShutdownBldring", CallingConvention = CallingConvention.StdCall)]
    private static extern void ShutdownBldring();

    [DllImport(DllFileName, EntryPoint = "GetBLDPortVal", CallingConvention = CallingConvention.StdCall)]
    private static extern bool GetBLDPortVal(ushort wPortAddr, ref byte pdwPortVal, byte bSize);

    [DllImport(DllFileName, EntryPoint = "SetBLDPortVal", CallingConvention = CallingConvention.StdCall)]
    private static extern bool SetBLDPortVal(ushort wPortAddr, byte dwPortVal, byte bSize);

    private bool ReadPort(ushort portAddr, out byte value)
    {
        value = 0;
        return GetBLDPortVal(portAddr, ref value, 1);
    }

    private bool WritePort(ushort portAddr, byte value)
    {
        return SetBLDPortVal(portAddr, value, 1);
    }

    public Blding64()
    {
        try
        {
            string resourceBase = "JiaoLongControl.Server.Resources.Drivers";
            string fullDllPath =
                EmbeddedResourceHelper.ExtractResourceToExeDir($"{resourceBase}.{DllFileName}", DllFileName);
            string fullSysPath =
                EmbeddedResourceHelper.ExtractResourceToExeDir($"{resourceBase}.{SysFileName}", SysFileName);

            if (!File.Exists(fullSysPath))
                throw new FileNotFoundException($"Driver file not found: {fullSysPath}");

            _dllHandle = Kernel32.LoadLibrary(fullDllPath);
            if (_dllHandle == IntPtr.Zero)
            {
                int err = Marshal.GetLastWin32Error();
                throw new Exception($"Failed to load DLL ({DllFileName}), ErrorCode: {err}");
            }
            DriverLoader.LoadDriver(ServiceName, fullSysPath);
            IsInitialized = InitializeBldring();
            if (!IsInitialized)
                throw new Exception("DLL initialization failed");
            EC_init();
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    private void EC_init()
    {
        byte EC_CHIP_ID1 = EC_RAM_READ(0x2000);
        if (EC_CHIP_ID1 == 0x55)
        {
            byte val = EC_RAM_READ(0x1060);
            val = (byte)(val | 0x80);
            EC_RAM_WRITE(0x1060, val);
        }
    }
    public void CpuFanSetSpeed(byte speed)
    {
        EC_RAM_WRITE(ECMemoryTable.Fan1_RPM_SET, speed);
        var mask = EC_RAM_READ(0xB20) | 0x02;
        EC_RAM_WRITE(0xB20, (byte)mask);
    }
    public void GpuFanSetSpeed(byte speed)
    {
        EC_RAM_WRITE(ECMemoryTable.Fan2_RPM_SET, speed);
        var mask = EC_RAM_READ(0xB20) | 0x08;
        EC_RAM_WRITE(0xB20, (byte)mask);
    }

    public void RemoveFanSpeed()
    {
        GpuFanSetSpeed(0);
        CpuFanSetSpeed(0);
        EC_RAM_WRITE(0xB20, 0x00);
    }

    private bool EC_RAM_WRITE(ushort iIndex, byte data)
    {
        lock (_ioLock)
        {
            byte highByte = (byte)(iIndex >> 8);
            byte lowByte = (byte)(iIndex & 0xFF);

            if (!WritePort(ECMemoryTable.EC_ADDR_PORT, 0x2E)) return false;
            if (!WritePort(ECMemoryTable.EC_DATA_PORT, 0x11)) return false;
            if (!WritePort(ECMemoryTable.EC_ADDR_PORT, 0x2F)) return false;
            if (!WritePort(ECMemoryTable.EC_DATA_PORT, highByte)) return false;

            if (!WritePort(ECMemoryTable.EC_ADDR_PORT, 0x2E)) return false;
            if (!WritePort(ECMemoryTable.EC_DATA_PORT, 0x10)) return false;
            if (!WritePort(ECMemoryTable.EC_ADDR_PORT, 0x2F)) return false;
            if (!WritePort(ECMemoryTable.EC_DATA_PORT, lowByte)) return false;

            if (!WritePort(ECMemoryTable.EC_ADDR_PORT, 0x2E)) return false;
            if (!WritePort(ECMemoryTable.EC_DATA_PORT, 0x12)) return false;
            if (!WritePort(ECMemoryTable.EC_ADDR_PORT, 0x2F)) return false;

            return WritePort(ECMemoryTable.EC_DATA_PORT, data);
        }
    }

    private byte EC_RAM_READ(ushort iIndex)
    {
        lock (_ioLock)
        {
            byte highByte = (byte)(iIndex >> 8);
            byte lowByte = (byte)(iIndex & 0xFF);

            WritePort(ECMemoryTable.EC_ADDR_PORT, 0x2E);
            WritePort(ECMemoryTable.EC_DATA_PORT, 0x11);
            WritePort(ECMemoryTable.EC_ADDR_PORT, 0x2F);
            WritePort(ECMemoryTable.EC_DATA_PORT, highByte);

            WritePort(ECMemoryTable.EC_ADDR_PORT, 0x2E);
            WritePort(ECMemoryTable.EC_DATA_PORT, 0x10);
            WritePort(ECMemoryTable.EC_ADDR_PORT, 0x2F);
            WritePort(ECMemoryTable.EC_DATA_PORT, lowByte);

            WritePort(ECMemoryTable.EC_ADDR_PORT, 0x2E);
            WritePort(ECMemoryTable.EC_DATA_PORT, 0x12);
            WritePort(ECMemoryTable.EC_ADDR_PORT, 0x2F);

            ReadPort(ECMemoryTable.EC_DATA_PORT, out byte data);
            return data;
        }
    }

    public void Dispose()
    {
        if (IsInitialized)
        {
            try
            {
                ShutdownBldring();
            }
            catch
            {
            }

            IsInitialized = false;
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

    ~Blding64() => Dispose();
}