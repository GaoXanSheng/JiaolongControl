using System.Diagnostics;
using Microsoft.Win32;

namespace JiaoLongControl.Server.Core.Controllers;

[System.Runtime.InteropServices.ComVisible(true)]
public class AutoStartController
{
    private const string RunKeyPath =
        @"Software\Microsoft\Windows\CurrentVersion\Run";

    private const string AppName = "JiaoLongControl";

    public void Enable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
        key?.SetValue(AppName, GetExePath());
    }

    public void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
        key?.DeleteValue(AppName, false);
    }

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
        return key?.GetValue(AppName) != null;
    }

    private string GetExePath()
    {
        return Process.GetCurrentProcess().MainModule!.FileName!;
    }
}