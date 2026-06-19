using System.IO;
using System.Runtime.InteropServices;
using JiaoLongControl.Server.Core.Models;
using JiaoLongControl.Server.Core.Utils;
using JiaoLongControl.Server.Interop;

namespace JiaoLongControl.Server.Core.Controllers;

[ComVisible(true)]
[ClassInterface(ClassInterfaceType.AutoDual)]
public class ConfigController
{
    static ConfigController()
    {
        PageConfigBase.ConfigDir = Path.Combine(AppContext.BaseDirectory, "config");
    }

    public CommandResult GetAppConfig()
    {
        try { return new CommandResult(true, "成功", Bridge.Instance.AppConfig); }
        catch (Exception ex) { return new CommandResult(false, ex.Message); }
    }

    public CommandResult SetAppConfig(AppPageConfig config)
    {
        try 
        { 
            config.Save("app.json"); 
            Bridge.Instance.AppConfig = config;
            return new CommandResult(true, "保存成功"); 
        }
        catch (Exception ex) { return new CommandResult(false, ex.Message); }
    }

    public CommandResult GetCpuConfig()
    {
        try { return new CommandResult(true, "成功", Bridge.Instance.CpuConfig); }
        catch (Exception ex) { return new CommandResult(false, ex.Message); }
    }

    public CommandResult SetCpuConfig(CpuPageConfig config)
    {
        try 
        { 
            config.Save("cpu.json"); 
            Bridge.Instance.CpuConfig = config;
            return new CommandResult(true, "保存成功"); 
        }
        catch (Exception ex) { return new CommandResult(false, ex.Message); }
    }

    public CommandResult GetGpuConfig()
    {
        try { return new CommandResult(true, "成功", Bridge.Instance.GpuConfig); }
        catch (Exception ex) { return new CommandResult(false, ex.Message); }
    }

    public CommandResult SetGpuConfig(GpuPageConfig config)
    {
        try 
        { 
            config.Save("gpu.json"); 
            Bridge.Instance.GpuConfig = config;
            return new CommandResult(true, "保存成功"); 
        }
        catch (Exception ex) { return new CommandResult(false, ex.Message); }
    }

    public CommandResult GetFanConfig()
    {
        try { return new CommandResult(true, "成功", Bridge.Instance.FanConfig); }
        catch (Exception ex) { return new CommandResult(false, ex.Message); }
    }

    public CommandResult SetFanConfig(FanPageConfig config)
    {
        try 
        { 
            config.Save("fan.json"); 
            Bridge.Instance.FanConfig = config;
            return new CommandResult(true, "保存成功"); 
        }
        catch (Exception ex) { return new CommandResult(false, ex.Message); }
    }

    public CommandResult GetSmuConfig()
    {
        try { return new CommandResult(true, "成功", Bridge.Instance.SmuConfig); }
        catch (Exception ex) { return new CommandResult(false, ex.Message); }
    }

    public CommandResult SetSmuConfig(SmuPageConfig config)
    {
        try 
        { 
            config.Save("smu.json"); 
            Bridge.Instance.SmuConfig = config;
            return new CommandResult(true, "保存成功"); 
        }
        catch (Exception ex) { return new CommandResult(false, ex.Message); }
    }
}
