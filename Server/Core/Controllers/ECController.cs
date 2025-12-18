using JiaoLongControl.Server.Core.Models;
using JiaoLongControl.Server.Core.Drivers;

namespace JiaoLongControl.Server.Core.Controllers
{
    public class ECController : WinIo
    {
        public ECController() : base()
        {
            if (State)
            {
                EC_init();
            }
        }

        private void EC_init()
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
            EC_RAM_WRITE(ECMemoryTable.Fan1_RPM_SET, speed);
            var mask = EC_RAM_READ(0xB20) | 0x02;
            EC_RAM_WRITE(0xB20, (byte)mask);
        }

        public void Fan2SetSpeed(byte speed)
        {
            EC_RAM_WRITE(ECMemoryTable.Fan2_RPM_SET, speed);
            var mask = EC_RAM_READ(0xB20) | 0x08;
            EC_RAM_WRITE(0xB20, (byte)mask);
        }
    }
}