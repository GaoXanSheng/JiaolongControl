using JiaoLongControl.Server.Core.Controllers;
using RyzenSmuControllerTest.Diagnostics;
using RyzenSmuControllerTest.Infrastructure;

namespace RyzenSmuControllerTest.Cli;

/// <summary>
/// 主运行管线：初始化控制器 → 前置遥测 → 流式分发命令（边解析边执行，保持旧版语义）→ 后置遥测对比 → 汇总退出码。
/// </summary>
internal static class CliRunner
{
    public static int Run(string[] args)
    {
        var exitCode = 0;
        RyzenSmuController? controller = null;

        try
        {
            ConsoleLog.Log(">>> 正在初始化 RyzenSmuController ...");
            ConsoleLog.Log("    (将加载 PawnIO 驱动、SMU 脚本、检测 CPU 型号)");

            controller = new RyzenSmuController();
            ConsoleLog.Log($"    检测到 CPU 架构: {controller.CurrentFamily}");
            ConsoleLog.Log("    (PawnIO 驱动将在首次执行 SMU 写操作时连接)");
            ConsoleLog.Log("");

            // 先展示当前遥测
            ConsoleLog.Log(">>> 读取当前 SMU 遥测 ...");
            TelemetryPrinter.Print(controller.GetSmuTelemetry());
            ConsoleLog.Log("");

            exitCode = DispatchLoop(controller, args);

            // 最后再读一次遥测对比
            ConsoleLog.Log("");
            ConsoleLog.Log(">>> 读取当前 SMU 遥测 (操作后) ...");
            TelemetryPrinter.Print(controller.GetSmuTelemetry());
            ConsoleLog.Log("");
        }
        catch (Exception ex)
        {
            ConsoleLog.Error($"未处理的异常: {ex.GetType().Name}");
            ConsoleLog.Error($"  消息: {ex.Message}");
            ConsoleLog.Error($"  堆栈: {ex.StackTrace}");
            exitCode = 1;
        }
        finally
        {
            controller?.Dispose();
            ConsoleLog.Log("");
            ConsoleLog.Log("客户端资源已释放。");
        }

        ConsoleLog.Log("========================================");
        ConsoleLog.Log(exitCode == 0
            ? "  ✓ 测试完成 — 全部成功"
            : "  ✗ 测试完成 — 有错误，详见上方日志");
        ConsoleLog.Log("========================================");

        return exitCode;
    }

    private static int DispatchLoop(RyzenSmuController controller, string[] args)
    {
        var registry = CommandRegistry.CreateDefault();
        var cursor = new ArgCursor(args);
        var exitCode = 0;

        while (!cursor.AtEnd)
        {
            string token = cursor.Current;
            cursor.Advance();

            if (!registry.TryGetValue(token, out CliCommand? command))
            {
                ConsoleLog.Error($"未知参数: {token}");
                ConsoleLog.Log("使用 --help 查看帮助");
                exitCode = 1;
                continue;
            }

            CommandOutcome outcome = command.Handler(new CliContext(controller, cursor));
            if (outcome == CommandOutcome.Failed)
                exitCode |= 1;
            // BadArguments 与 Succeeded 均不影响退出码（旧版行为：解析失败只打印错误）
        }

        return exitCode;
    }
}
