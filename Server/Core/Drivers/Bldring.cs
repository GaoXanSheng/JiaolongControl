using System;
using System.Runtime.InteropServices;
using JiaoLongControl.Server.Core.Models;
using JiaoLongControl.Server.Core.Utils;

namespace JiaoLongControl.Server.Core.Drivers
{
    public class Bldring : IDisposable
    {
        private string dllName = "JiaoLongDriver64.dll";
        private string sysName = "JiaoLongDriver64.sys";

        [DllImport("JiaoLongDriver64.dll", EntryPoint = "InitializeBldring", CallingConvention = CallingConvention.StdCall)]
        private static extern bool InitializeBldring();

        [DllImport("JiaoLongDriver64.dll", EntryPoint = "ShutdownBldring", CallingConvention = CallingConvention.StdCall)]
        private static extern void ShutdownBldring();

        [DllImport("JiaoLongDriver64.dll", EntryPoint = "GetBLDPortVal", CallingConvention = CallingConvention.StdCall)]
        private static extern bool GetBLDPortVal(ushort wPortAddr, ref byte pdwPortVal, byte bSize);

        [DllImport("JiaoLongDriver64.dll", EntryPoint = "SetBLDPortVal", CallingConvention = CallingConvention.StdCall)]
        private static extern bool SetBLDPortVal(ushort wPortAddr, byte dwPortVal, byte bSize);

        [DllImport("JiaoLongDriver64.dll", EntryPoint = "InstallBldringDriver", CallingConvention = CallingConvention.StdCall)]
        private static extern uint InstallBldringDriver(string sysPath, bool force);

        [DllImport("kernel32")]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32", SetLastError = true)]
        private static extern bool FreeLibrary(IntPtr hModule);

        private IntPtr driverHandle;
        public bool State { get; set; }

        public Bldring()
        {
            string resourceBase = "JiaoLongControl.Server.Resources.Drivers";
            EmbeddedResourceHelper.ExtractResourceToExeDir($"{resourceBase}.{dllName}", dllName);
            EmbeddedResourceHelper.ExtractResourceToExeDir($"{resourceBase}.{sysName}", sysName);
            driverHandle = LoadLibrary(dllName);
            State = InitializeBldring();
            if (!State)
            {
                InstallBldringDriver(System.IO.Path.GetFullPath(sysName), true);
                State = InitializeBldring();
            }
        }
        protected bool EC_RAM_WRITE(ushort iIndex, byte data)
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
            return WRITE_PORT(ECMemoryTable.EC_DATA_PORT, data);
        }
        protected byte EC_RAM_READ(ushort iIndex)
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
        
        private bool WRITE_PORT(ushort iIndex, byte data)
        {
            return SetBLDPortVal(iIndex, data, 1);
        }
        
        private byte READ_PORT(ushort iIndex)
        {
            byte data = 0;
            GetBLDPortVal(iIndex, ref data, 1);
            return data;
        }
        
        public void Dispose()
        {
            if (driverHandle != IntPtr.Zero)
            {
                ShutdownBldring();
                FreeLibrary(driverHandle);
                driverHandle = IntPtr.Zero;
            }
        }
    }
}