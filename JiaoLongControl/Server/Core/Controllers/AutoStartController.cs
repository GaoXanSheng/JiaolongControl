using Microsoft.Win32;
using Microsoft.Win32.TaskScheduler;
namespace JiaoLongControl.Server.Core.Controllers;

[System.Runtime.InteropServices.ComVisible(true)]
public class AutoStartController
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private const string AppName = "JiaoLongControl";

    public void Enable()
    {
        RemoveLegacyRegistry();
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
        ts.RootFolder.RegisterTaskDefinition(
            AppName,
            td,
            TaskCreation.CreateOrUpdate,
            null,
            null,
            TaskLogonType.InteractiveToken
        );
    }

    public void Disable()
    {
        using var ts = new TaskService();
        ts.RootFolder.DeleteTask(AppName, false);
        RemoveLegacyRegistry();
    }

    public bool IsEnabled()
    {
        using var ts = new TaskService();
        return ts.GetTask(AppName) != null;
    }

    [Obsolete]
    private void RemoveLegacyRegistry()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
        key?.DeleteValue(AppName, false);
    }
}