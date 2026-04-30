using System.Runtime.InteropServices;

namespace JiaoLongControl.Server.Core.Native;

public class Newdev64
{

    [DllImport("newdev.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "UpdateDriverForPlugAndPlayDevicesW")]
    public static extern bool UpdateDriverForPlugAndPlayDevices(
        IntPtr hwndParent,
        string hardwareId,
        string fullInfPath,
        uint installFlags,
        out bool bRebootRequired);
}