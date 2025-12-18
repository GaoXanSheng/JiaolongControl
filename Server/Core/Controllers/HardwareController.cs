using JiaoLongControl.Server.Core.Models;


namespace JiaoLongControl.Server.Core.Controllers
{

    [System.Runtime.InteropServices.ComVisible(true)]
    public class HardwareController
    {
        public static readonly ComputerInformation ComputerInfo = new ComputerInformation();
        public string GetHardwareMonitorInfo()
        {
            return ComputerInfo.GetHardwareMonitorInfo();
        }
    }
}