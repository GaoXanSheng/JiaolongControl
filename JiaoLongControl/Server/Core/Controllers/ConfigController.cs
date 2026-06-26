using System.IO;
using System.Runtime.InteropServices;
using JiaoLongControl.Server.Core.Models;

namespace JiaoLongControl.Server.Core.Controllers;

[ComVisible(true)]
[ClassInterface(ClassInterfaceType.AutoDual)]
public partial class ConfigController
{
    static ConfigController()
    {
        PageConfigBase.ConfigDir = Path.Combine(AppContext.BaseDirectory, "config");
    }
}
