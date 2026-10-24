namespace RyzenSmuControllerTest.Infrastructure;

/// <summary>带时间戳的控制台输出原语，运行日志统一出口（帮助文本除外）。</summary>
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
        Log($"║   {string.Join(" ", args).Truncate(38).PadRight(38)}║");
        Log("╚══════════════════════════════════════════╝");
        Log("");
    }
}
