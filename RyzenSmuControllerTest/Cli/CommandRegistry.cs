using JiaoLongControl.Server.Core.Controllers;
using JiaoLongControl.Server.Core.Utils;
using RyzenSmuControllerTest.Diagnostics;
using RyzenSmuControllerTest.Infrastructure;

namespace RyzenSmuControllerTest.Cli;

/// <summary>
/// 全部命令行选项的注册表——选项名的唯一事实来源（--help 文案为手写分组说明，改动选项时两处同步）。
/// 解析失败的命令返回 BadArguments，沿用旧版语义：打印错误但不计入退出码。
/// </summary>
internal static class CommandRegistry
{
    public static IReadOnlyDictionary<string, CliCommand> CreateDefault() =>
        Commands()
            .SelectMany(cmd => cmd.Names.Select(name => (Name: name, Cmd: cmd)))
            .ToDictionary(entry => entry.Name, entry => entry.Cmd, StringComparer.Ordinal);

    private static IEnumerable<CliCommand> Commands()
    {
        // === 遥测 ===
        yield return new CliCommand(["--telemetry", "-t"], _ => CommandOutcome.Succeeded); // 仅读取，主流程已执行
        yield return new CliCommand(["--dump-sensors"], _ =>
        {
            SensorDumper.DumpAll();
            return CommandOutcome.Succeeded;
        });
        yield return new CliCommand(["--dump-svi"], ctx =>
        {
            SviTelemetryDumper.Dump(ctx.Controller);
            return CommandOutcome.Succeeded;
        });
        yield return new CliCommand(["--smu-read"], ctx =>
        {
            if (!ctx.Args.TryReadHex(out uint addr)) return CommandOutcome.BadArguments;
            SmuRegisterDumper.Dump(ctx.Controller, addr);
            return CommandOutcome.Succeeded;
        });
        yield return new CliCommand(["--cpu-vid"], _ =>
        {
            CpuVidDumper.DumpCurrentVidFromMsr();
            return CommandOutcome.Succeeded;
        });
        yield return new CliCommand(["--core-voltage"], ctx =>
        {
            CoreVoltageDumper.Dump(ctx.Controller);
            return CommandOutcome.Succeeded;
        });
        yield return new CliCommand(["--vid-per-core"], _ =>
        {
            CpuVidDumper.DumpVidPerCore();
            return CommandOutcome.Succeeded;
        });

        // === Curve Optimizer ===
        yield return new CliCommand(["--curve-all"], ctx =>
        {
            if (!ctx.Args.TryReadInt(-30, 30, out int value)) return CommandOutcome.BadArguments;
            return Execute(ctx, () => ctx.Controller.SetCurveOptimizerAll(value),
                $"SetCurveOptimizerAll({value})");
        });
        yield return new CliCommand(["--curve-core"], ctx =>
        {
            if (!ctx.Args.TryReadInt(0, 15, out int coreIdx)) return CommandOutcome.BadArguments;
            if (!ctx.Args.TryReadInt(-30, 30, out int value)) return CommandOutcome.BadArguments;
            return Execute(ctx, () => ctx.Controller.SetCurveOptimizerPerCore((uint)coreIdx, value),
                $"SetCurveOptimizerPerCore(core={coreIdx}, val={value})");
        });
        yield return new CliCommand(["--smu-cmd"], ctx => SendRaw(ctx, "--smu-cmd", readBack: false));
        yield return new CliCommand(["--smu-cmd-read"], ctx => SendRaw(ctx, "--smu-cmd-read", readBack: true));

        // === 功耗限制 ===
        yield return new CliCommand(["--stapm"], ctx =>
        {
            if (!ctx.Args.TryReadDouble(0, 200, out double watts)) return CommandOutcome.BadArguments;
            return Execute(ctx, () => ctx.Controller.SetStapmLimit(watts), $"SetStapmLimit({watts}W)");
        });
        yield return new CliCommand(["--fast"], ctx =>
        {
            if (!ctx.Args.TryReadDouble(0, 200, out double watts)) return CommandOutcome.BadArguments;
            return Execute(ctx, () => ctx.Controller.SetFastLimit(watts), $"SetFastLimit({watts}W)");
        });
        yield return new CliCommand(["--slow"], ctx =>
        {
            if (!ctx.Args.TryReadDouble(0, 200, out double watts)) return CommandOutcome.BadArguments;
            return Execute(ctx, () => ctx.Controller.SetSlowLimit(watts), $"SetSlowLimit({watts}W)");
        });
        yield return new CliCommand(["--ppt-rsmu"], ctx =>
        {
            if (!ctx.Args.TryReadDouble(0, 200, out double watts)) return CommandOutcome.BadArguments;
            return Execute(ctx, () => ctx.Controller.SetPptLimitRsmu(watts), $"SetPptLimitRsmu({watts}W)");
        });
        yield return new CliCommand(["--stapm-time"], ctx =>
        {
            if (!ctx.Args.TryReadInt(1, 3600, out int seconds)) return CommandOutcome.BadArguments;
            return Execute(ctx, () => ctx.Controller.SetStapmTime((uint)seconds), $"SetStapmTime({seconds}s)");
        });
        yield return new CliCommand(["--slow-time"], ctx =>
        {
            if (!ctx.Args.TryReadInt(1, 3600, out int seconds)) return CommandOutcome.BadArguments;
            return Execute(ctx, () => ctx.Controller.SetSlowTime((uint)seconds), $"SetSlowTime({seconds}s)");
        });

        // === 电流限制 ===
        yield return new CliCommand(["--vrm-mp1"], ctx =>
        {
            if (!ctx.Args.TryReadInt(0, 200000, out int ma)) return CommandOutcome.BadArguments;
            return Execute(ctx, () => ctx.Controller.SetVrmCurrentMp1((uint)ma), $"SetVrmCurrentMp1({ma}mA)");
        });
        yield return new CliCommand(["--vrm-rsmu"], ctx =>
        {
            if (!ctx.Args.TryReadInt(0, 200000, out int ma)) return CommandOutcome.BadArguments;
            return Execute(ctx, () => ctx.Controller.SetVrmCurrentRsmu((uint)ma), $"SetVrmCurrentRsmu({ma}mA)");
        });
        yield return new CliCommand(["--edc-mp1"], ctx =>
        {
            if (!ctx.Args.TryReadInt(0, 200000, out int ma)) return CommandOutcome.BadArguments;
            return Execute(ctx, () => ctx.Controller.SetEdcLimitMp1((uint)ma), $"SetEdcLimitMp1({ma}mA)");
        });
        yield return new CliCommand(["--edc-rsmu"], ctx =>
        {
            if (!ctx.Args.TryReadInt(0, 200000, out int ma)) return CommandOutcome.BadArguments;
            return Execute(ctx, () => ctx.Controller.SetEdcLimitRsmu((uint)ma), $"SetEdcLimitRsmu({ma}mA)");
        });

        // === 温度限制 ===
        yield return new CliCommand(["--temp-mp1"], ctx =>
        {
            if (!ctx.Args.TryReadInt(40, 115, out int celsius)) return CommandOutcome.BadArguments;
            return Execute(ctx, () => ctx.Controller.SetTempLimitMp1((uint)celsius), $"SetTempLimitMp1({celsius}°C)");
        });
        yield return new CliCommand(["--temp-rsmu"], ctx =>
        {
            if (!ctx.Args.TryReadInt(40, 115, out int celsius)) return CommandOutcome.BadArguments;
            return Execute(ctx, () => ctx.Controller.SetTempLimitRsmu((uint)celsius), $"SetTempLimitRsmu({celsius}°C)");
        });

        // === PBO & 超频 ===
        yield return new CliCommand(["--pbo-scalar"], ctx =>
        {
            if (!ctx.Args.TryReadInt(0, 10, out int scalar)) return CommandOutcome.BadArguments;
            return Execute(ctx, () => ctx.Controller.SetPboScalar((uint)scalar), $"SetPboScalar({scalar})");
        });
        yield return new CliCommand(["--oc-enable"], ctx =>
            Execute(ctx, () => ctx.Controller.EnableOc(), "EnableOc"));
        yield return new CliCommand(["--oc-disable"], ctx =>
            Execute(ctx, () => ctx.Controller.DisableOc(), "DisableOc"));
        yield return new CliCommand(["--oc-clk"], ctx =>
        {
            if (!ctx.Args.TryReadInt(-500, 1000, out int mhz)) return CommandOutcome.BadArguments;
            return Execute(ctx, () => ctx.Controller.SetOcClk(mhz), $"SetOcClk({mhz}MHz)");
        });
        yield return new CliCommand(["--oc-volt"], ctx =>
        {
            if (!ctx.Args.TryReadInt(0, 2000, out int mv)) return CommandOutcome.BadArguments;
            return Execute(ctx, () => ctx.Controller.SetOcVolt((uint)mv), $"SetOcVolt({mv}mV)");
        });
    }

    private static CommandOutcome Execute(CliContext ctx, Func<CommandResult> action, string label) =>
        CommandRunner.Run(ctx.Controller, action, label) ? CommandOutcome.Succeeded : CommandOutcome.Failed;

    /// <summary>--smu-cmd 与 --smu-cmd-read 的共同主体：cmd、arg 两个 hex 值 + 邮箱类型；
    /// readBack 为 true 时追加回读邮箱参数槽。</summary>
    private static CommandOutcome SendRaw(CliContext ctx, string optionName, bool readBack)
    {
        if (!ctx.Args.TryReadHex(out uint cmd)) return CommandOutcome.BadArguments;
        if (!ctx.Args.TryReadHex(out uint arg)) return CommandOutcome.BadArguments;
        if (!ctx.Args.TryReadMailbox(optionName, out bool isMp1)) return CommandOutcome.BadArguments;

        string rawName = $"SMU Raw 0x{cmd:X2} {(isMp1 ? "MP1" : "RSMU")}";
        CommandOutcome outcome = Execute(ctx,
            () => ctx.Controller.SendRaw(cmd, arg, isMp1, rawName),
            $"SendRaw(cmd=0x{cmd:X}, arg=0x{arg:X}, {(isMp1 ? "MP1" : "RSMU")})");

        if (readBack)
        {
            // 旧版行为：无论命令成败都回读一次参数槽
            ulong[] retArgs = ctx.Controller.ReadMailboxArgs(isMp1);
            ConsoleLog.Log("      返回参数槽: " + string.Join(" ", retArgs.Select(a => $"0x{a:X}")));
        }
        return outcome;
    }
}
