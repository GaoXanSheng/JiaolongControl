using System.Runtime.InteropServices;
using System.Text.Json;
using JiaoLongControl.Server.Core.Models;
using JiaoLongControl.Server.Core.Utils;
using JiaoLongControl.Server.Interop;

namespace JiaoLongControl.Server.Core.Controllers;

[ComVisible(true)]
[ClassInterface(ClassInterfaceType.AutoDual)]
public class ConfigController
{
    public CommandResult GetConfig()
    {
        try
        {
            return new CommandResult(true, "成功", Bridge.Instance.Config);
        }
        catch (Exception ex)
        {
            return new CommandResult(false, ex.Message);
        }
    }

    public CommandResult SetConfig(string configJson)
    {
        try
        {
            var config = JsonSerializer.Deserialize<JiaoLongConfig>(configJson);
            if (config == null)
                return new CommandResult(false, "配置数据无效");
            ConfigSerializer.Save(config);
            Bridge.Instance.Config = config;
            return new CommandResult(true, "保存成功");
        }
        catch (Exception ex)
        {
            return new CommandResult(false, ex.Message);
        }
    }
}
