using System.IO;
using System.Runtime.CompilerServices;
using Serilog.Events;

namespace JiaoLongControl.Server.Core.Utils;

public sealed class Log
{
    private readonly string _source;

    private Log(string source) => _source = source;

    public static Log For<T>() => new(typeof(T).Name);

    public static Log For(string source) => new(source);

    public void Debug(string message,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0,
        [CallerMemberName] string member = "")
        => Write(LogEventLevel.Debug, message, null, file, line, member);

    public void Info(string message,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0,
        [CallerMemberName] string member = "")
        => Write(LogEventLevel.Information, message, null, file, line, member);

    public void Warn(string message,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0,
        [CallerMemberName] string member = "")
        => Write(LogEventLevel.Warning, message, null, file, line, member);

    public void Error(string message, Exception? exception = null,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0,
        [CallerMemberName] string member = "")
        => Write(LogEventLevel.Error, message, exception, file, line, member);

    public void Fatal(string message, Exception? exception = null,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0,
        [CallerMemberName] string member = "")
        => Write(LogEventLevel.Fatal, message, exception, file, line, member);

    private void Write(LogEventLevel level, string message, Exception? exception, string file, int line, string member)
    {
        if (!Serilog.Log.IsEnabled(level))
            return;
        Serilog.Log.Write(level, exception, "{Source} {Caller} {Text}",
            _source, $"{Path.GetFileName(file)}:{line} {member}", message);
    }
}
