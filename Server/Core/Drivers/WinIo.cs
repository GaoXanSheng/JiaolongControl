using System.Runtime.InteropServices;
using JiaoLongControl.Server.Core.Models;
using JiaoLongControl.Server.Core.Utils;

namespace JiaoLongControl.Server.Core.Drivers
{
    public class WinIo : IDisposable
    {
        private string dllName = "WinIo64.dll";
        private string sysName = "WinIo64.sys";
        
        [DllImport("WinIo64.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern bool InitializeWinIo();
        [DllImport("WinIo64.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern void ShutdownWinIo();
        [DllImport("WinIo64.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern bool GetPortVal(ushort wPortAddr, ref byte pdwPortVal, byte bSize);
        [DllImport("WinIo64.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern bool SetPortVal(ushort wPortAddr, byte dwPortVal, byte bSize);
        [DllImport("kernel32")]
        private static extern IntPtr LoadLibrary(string lpFileName);
        [DllImport("kernel32", SetLastError = true)]
        private static extern bool FreeLibrary(IntPtr hModule);

        private IntPtr winioHandle;
        public bool State { get; set; }

        public WinIo()
        {
            string resourceBase = "JiaoLongControl.Server.Resources.Drivers";
            EmbeddedResourceHelper.ExtractResourceToExeDir($"{resourceBase}.{dllName}", dllName);
            EmbeddedResourceHelper.ExtractResourceToExeDir($"{resourceBase}.{sysName}", sysName);
            
            winioHandle = LoadLibrary(dllName);
            State = InitializeWinIo();
        }


        /// <summary>
        /// 向 EC RAM 写入数据。
        /// </summary>
        /// <param name="iIndex">索引。</param>
        /// <param name="data">要写入的数据。</param>
        /// <returns>是否写入成功。</returns>
        protected bool EC_RAM_WRITE(ushort iIndex, byte data)
        {
            byte highByte = (byte)(iIndex >> 8);
            byte lowByte = (byte)(iIndex & 0xFF);
            WRITE_PORT(ECMemoryTable.EC_ADDR_PORT, 0x2E);
            WRITE_PORT(ECMemoryTable.EC_DATA_PORT, 0x11);
            WRITE_PORT(ECMemoryTable.EC_ADDR_PORT, 0x2F);
            WRITE_PORT(ECMemoryTable.EC_DATA_PORT, highByte); // High byte

            WRITE_PORT(ECMemoryTable.EC_ADDR_PORT, 0x2E);
            WRITE_PORT(ECMemoryTable.EC_DATA_PORT, 0x10);
            WRITE_PORT(ECMemoryTable.EC_ADDR_PORT, 0x2F);
            WRITE_PORT(ECMemoryTable.EC_DATA_PORT, lowByte); // Low byte

            WRITE_PORT(ECMemoryTable.EC_ADDR_PORT, 0x2E);
            WRITE_PORT(ECMemoryTable.EC_DATA_PORT, 0x12);
            WRITE_PORT(ECMemoryTable.EC_ADDR_PORT, 0x2F);
            return WRITE_PORT(ECMemoryTable.EC_DATA_PORT, data);
        }

        /// <summary>
        /// 从 EC RAM 读取数据。
        /// </summary>
        /// <param name="iIndex">索引。</param>
        /// <returns>读取到的数据。</returns>
        protected byte EC_RAM_READ(ushort iIndex)
        {
            byte highByte = (byte)(iIndex >> 8);
            byte lowByte = (byte)(iIndex & 0xFF);
            WRITE_PORT(ECMemoryTable.EC_ADDR_PORT, 0x2E);
            WRITE_PORT(ECMemoryTable.EC_DATA_PORT, 0x11);
            WRITE_PORT(ECMemoryTable.EC_ADDR_PORT, 0x2F);
            WRITE_PORT(ECMemoryTable.EC_DATA_PORT, highByte); // High byte

            WRITE_PORT(ECMemoryTable.EC_ADDR_PORT, 0x2E);
            WRITE_PORT(ECMemoryTable.EC_DATA_PORT, 0x10);
            WRITE_PORT(ECMemoryTable.EC_ADDR_PORT, 0x2F);
            WRITE_PORT(ECMemoryTable.EC_DATA_PORT, lowByte); // Low byte

            WRITE_PORT(ECMemoryTable.EC_ADDR_PORT, 0x2E);
            WRITE_PORT(ECMemoryTable.EC_DATA_PORT, 0x12);
            WRITE_PORT(ECMemoryTable.EC_ADDR_PORT, 0x2F);
            return READ_PORT(ECMemoryTable.EC_DATA_PORT);
        }

        /// <summary>
        /// 向端口写入数据。
        /// </summary>
        /// <param name="iIndex">端口索引。</param>
        /// <param name="data">要写入的数据。</param>
        /// <returns>是否写入成功。</returns>
        private bool WRITE_PORT(ushort iIndex, byte data)
        {
            return SetPortVal(iIndex, data, 1);
        }

        /// <summary>
        /// 从端口读取数据。
        /// </summary>
        /// <param name="iIndex">端口索引。</param>
        /// <returns>读取到的数据。</returns>
        private byte READ_PORT(ushort iIndex)
        {
            byte data = 0;
            GetPortVal(iIndex, ref data, 1);
            return data;
        }

        /// <summary>
        /// 释放资源，卸载 WinIo64.dll。
        /// </summary>
        public void Dispose()
        {
            ShutdownWinIo();
            FreeLibrary(winioHandle);
        }
    }
}