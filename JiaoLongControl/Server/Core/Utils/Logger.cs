using System;
using System.IO;

namespace JiaoLongControl.Server.Core.Utils;

/// <summary>
/// 简单日志记录器
/// </summary>
public static class Logger
{
    private static readonly object _lock = new();

    private static string GetLogDirectory()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string logDir = Path.Combine(baseDir, "log");
        Directory.CreateDirectory(logDir);
        return logDir;
    }

    private static string GetLogFilePath()
    {
        return Path.Combine(GetLogDirectory(), "service.log");
    }

    public static void Info(string format, params object[] args)
    {
        try
        {
            string message = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] INFO  {string.Format(format, args)}";
            WriteLine(message);
        }
        catch
        {
        }
    }

    public static void Error(string message)
    {
        WriteLine(
            $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR {message}"
        );
    }

    private static void WriteLine(string message)
    {
        lock (_lock)
        {
            File.AppendAllText(
                GetLogFilePath(),
                message + Environment.NewLine
            );
#if DEBUG
        Console.WriteLine(message);
#endif
        }
    }
}