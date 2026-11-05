using System.IO;
using Serilog;

namespace JiaoLongControl.Server.Core.Utils;

public static class LogSetup
{
    public static void Initialize()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "logs");
        Directory.CreateDirectory(directory);

        Serilog.Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                Path.Combine(directory, "log-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 31,
                outputTemplate:
                "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Source} {Caller} {Text:lj}{NewLine}{Exception}")
            .CreateLogger();
    }

    public static void Shutdown() => Serilog.Log.CloseAndFlush();
}
