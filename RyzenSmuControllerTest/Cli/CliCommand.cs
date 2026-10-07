using JiaoLongControl.Server.Core.Controllers;

namespace RyzenSmuControllerTest.Cli;

/// <summary>单条选项的处理结果。BadArguments 沿用旧版行为：不计入退出码（仅打印错误）。</summary>
internal enum CommandOutcome
{
    Succeeded,
    Failed,
    BadArguments,
}

/// <summary>一条命令行选项：若干别名 + 处理器。处理器从游标自取参数。</summary>
internal sealed record CliCommand(string[] Names, Func<CliContext, CommandOutcome> Handler);

/// <summary>命令执行上下文：已初始化的控制器 + 参数游标。</summary>
internal sealed class CliContext(RyzenSmuController controller, ArgCursor args)
{
    public RyzenSmuController Controller { get; } = controller;

    public ArgCursor Args { get; } = args;
}
