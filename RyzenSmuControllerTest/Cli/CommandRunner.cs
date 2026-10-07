using JiaoLongControl.Server.Core.Controllers;
using JiaoLongControl.Server.Core.Utils;
using RyzenSmuControllerTest.Infrastructure;

namespace RyzenSmuControllerTest.Cli;

/// <summary>执行一条控制器写操作并打印结果，返回是否成功。</summary>
internal static class CommandRunner
{
    public static bool Run(RyzenSmuController controller, Func<CommandResult> action, string label)
    {
        ConsoleLog.Log($">>> 执行 {label} ...");
        var result = action();
        ConsoleLog.Log($"      Success : {result.Success}");
        ConsoleLog.Log($"      Message : {result.Message}");

        if (!result.Success)
        {
            ConsoleLog.Error($"    {label} 失败: {result.Message}");
            return false;
        }
        return true;
    }
}
