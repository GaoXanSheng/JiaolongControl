using System.IO;
using System.Runtime.InteropServices;
using JiaoLongControl.Server.Core.Native;
using JiaoLongControl.Server.Core.Utils;

namespace JiaoLongControl.Server.Core.Drivers;

public class NVPCF
{
    private const uint INSTALLFLAG_FORCE = 0x00000001;
    private const uint INSTALLFLAG_NONINTERACTIVE = 0x00000002;
    private const string HwId = @"ACPI\NVDA0820";

    public CommandResult Install()
    {
        try
        {
            string driverFolderPath = Path.Combine(AppContext.BaseDirectory, "Drivers", "NVPCF");
            string infPath = Path.Combine(driverFolderPath, "nvpcf.inf");

            if (!File.Exists(infPath))
                return new CommandResult(false, $"找不到驱动文件: {infPath}");

            uint flags = INSTALLFLAG_FORCE | INSTALLFLAG_NONINTERACTIVE;
            bool rebootRequired = false;

            bool result = Newdev64.UpdateDriverForPlugAndPlayDevices(
                IntPtr.Zero, HwId, infPath, flags, out rebootRequired);

            if (result)
            {
                return new CommandResult(true, rebootRequired ? "驱动安装成功，但系统需要重启。" : "驱动降级安装成功！");
            }
            else
            {
                int errorCode = Marshal.GetLastWin32Error();
                string msg = errorCode switch
                {
                    5 => "拒绝访问 (0x00000005)，请以管理员身份运行。",
                    _ => $"驱动安装失败，Win32错误码: 0x{errorCode:X8}"
                };
                return new CommandResult(false, msg);
            }
        }
        catch (Exception ex)
        {
            return new CommandResult(false, $"安装过程发生异常: {ex.Message}");
        }
    }
}