using System.Runtime.InteropServices;
using JiaoLongControl.Server.Core.Utils;
using Microsoft.Win32;
using Microsoft.Win32.TaskScheduler;
namespace JiaoLongControl.Server.Core.Controllers;

[ComVisible(true)]
[ClassInterface(ClassInterfaceType.AutoDual)]
public class AutoStartController
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private const string AppName = "JiaoLongControl";

    public CommandResult Enable()
    {
        using var ts = new TaskService();
        var td = ts.NewTask();
        td.RegistrationInfo.Description = "JiaoLongControl AutoStart";
        td.Triggers.Add(new LogonTrigger
        {
            Delay = TimeSpan.FromSeconds(10)
        });

        string exePath = Environment.ProcessPath!;

        td.Actions.Add(new ExecAction(exePath, "--boot", null));
        td.Principal.RunLevel = TaskRunLevel.Highest;
        td.Settings.MultipleInstances = TaskInstancesPolicy.IgnoreNew;
        // TaskScheduler 库默认电池模式下禁止启动/切电池时停止, 显式关闭以支持笔记本电池场景
        td.Settings.DisallowStartIfOnBatteries = false;
        td.Settings.StopIfGoingOnBatteries = false;
        ts.RootFolder.RegisterTaskDefinition(
            AppName,
            td,
            TaskCreation.CreateOrUpdate,
            null,
            null,
            TaskLogonType.InteractiveToken
        );
        return new CommandResult(true, "已启用开机自启");
    }

    public CommandResult Disable()
    {
        using var ts = new TaskService();
        ts.RootFolder.DeleteTask(AppName, false);
        return new CommandResult(true, "已禁用开机自启");
    }

    public CommandResult IsEnabled()
    {
        using var ts = new TaskService();
        var task = ts.GetTask(AppName);
        return new CommandResult(task != null, task != null ? "开机自启已启用" : "开机自启未启用");
    }
}