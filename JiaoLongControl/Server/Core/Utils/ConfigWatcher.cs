using System.IO;

namespace JiaoLongControl.Server.Core.Utils;

public class ConfigWatcher : IDisposable
{
    private readonly FileSystemWatcher _watcher;
    public event Action? ConfigChanged;

    public ConfigWatcher(string configDir)
    {
        _watcher = new FileSystemWatcher(configDir, "config.yaml")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true
        };

        _watcher.Changed += OnChanged;
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        Thread.Sleep(100);
        ConfigChanged?.Invoke();
    }

    public void Dispose()
    {
        _watcher.EnableRaisingEvents = false;
        _watcher.Dispose();
    }
}
