namespace RyzenSmuControllerTest.Infrastructure;

/// <summary>带时间戳的控制台输出原语，全工具唯一的日志出口。</summary>
internal static class ConsoleLog
{
    public static void Log(string msg)
    {
        var ts = DateTime.Now.ToString("HH:mm:ss.fff");
        Console.WriteLine($"[{ts}] {msg}");
    }

    public static void Error(string msg)
    {
        var original = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Red;
        var ts = DateTime.Now.ToString("HH:mm:ss.fff");
        Console.WriteLine($"[{ts}] [ERR] {msg}");
        Console.ForegroundColor = original;
    }

    public static void Banner(string[] args)
    {
        Log("╔══════════════════════════════════════════╗");
        Log("║   SMU 命令行测试工具                     ║");
        Log($"║   {string.Join(" ", args).Truncate(34)}");
        Log("╚══════════════════════════════════════════╝");
        Log("");
    }
}
