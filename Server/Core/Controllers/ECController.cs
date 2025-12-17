using JiaoLongControl.Server.Core.Constants;
using JiaoLongControl.Server.Core.Drivers;

// 引用上一轮生成的 WinIo

namespace JiaoLongControl.Server.Core.Controllers
{
    // 直接继承 WinIo，复用其端口读写能力
    public class ECController : WinIo
    {
        public ECController() : base()
        {
            if (State)
            {
                EC_init();
            }
        }

        public void EC_init()
        {
            byte EC_CHIP_ID1 = EC_RAM_READ(0x2000);
            if (EC_CHIP_ID1 == 0x55)
            {
                byte val = EC_RAM_READ(0x1060);
                val = (byte)(val | 0x80);
                EC_RAM_WRITE(0x1060, val); // enable EC RAM
            }
        }

        public void Fan1SetSpeed(byte speed)
        {
            EC_RAM_WRITE((ushort)ECMemoryTable.Fan1_RPM_SET, speed);
            var mask = EC_RAM_READ(0xB20) | 0x02;
            EC_RAM_WRITE(0xB20, (byte)mask);
        }

        public void Fan2SetSpeed(byte speed)
        {
            EC_RAM_WRITE((ushort)ECMemoryTable.Fan2_RPM_SET, speed);
            var mask = EC_RAM_READ(0xB20) | 0x08;
            EC_RAM_WRITE(0xB20, (byte)mask);
        }
    }
}