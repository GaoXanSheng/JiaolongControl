using RyzenSmuControllerTest.Cli;
using RyzenSmuControllerTest.Infrastructure;
using RyzenSmuControllerTest.SelfTest;

if (args.Length == 0 || args.Any(a => a is "-h" or "--help"))
{
    HelpText.Print();
    return 0;
}

ConsoleLog.Banner(args);

// --selftest: 无硬件自检, 只验证 SmuFamilyResolver 判定表, 不构造控制器、不加载 PawnIO
if (args.Contains("--selftest"))
{
    return SmuFamilySelfTest.Run();
}

return CliRunner.Run(args);
