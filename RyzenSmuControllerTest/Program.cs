using JiaoLongControl.Server.Core.Controllers;
using JiaoLongControl.Server.Core.Utils;

if (args.Length == 0 || args.Any(a => a is "-h" or "--help"))
{
    PrintHelp();
    return 0;
}

WriteBanner(args);

var exitCode = 0;
RyzenSmuController? controller = null;

try
{
    Log(">>> 正在初始化 RyzenSmuController ...");
    Log("    (将加载 PawnIO 驱动、SMU 脚本、检测 CPU 型号)");

    controller = new RyzenSmuController();
    if (!controller.IsInitialized)
    {
        LogError("初始化失败: IsInitialized == false");
        return 1;
    }
    Log("    初始化成功!");
    Log($"    检测到 CPU 架构: {controller.CurrentFamily}");
    Log("");

    // 先展示当前遥测
    Log(">>> 读取当前 SMU 遥测 ...");
    LogTelemetry(controller.GetSmuTelemetry());
    Log("");

    // 解析命令行
    int i = 0;
    while (i < args.Length)
    {
        var arg = args[i];
        switch (arg)
        {
            case "--telemetry":
            case "-t":
                // 仅读取，已在上方执行
                i++;
                break;

            case "--curve-all":
                if (!TryParseIntArg(args, ref i, out int curveAllVal, -30, 30)) break;
                exitCode |= RunCommand(controller,
                    () => controller.SetCurveOptimizerAll(curveAllVal),
                    $"SetCurveOptimizerAll({curveAllVal})");
                break;

            case "--curve-core":
                if (!TryParseIntArg(args, ref i, out int coreIdx, 0, 15)) break;
                if (!TryParseIntArg(args, ref i, out int coreVal, -30, 30)) break;
                exitCode |= RunCommand(controller,
                    () => controller.SetCurveOptimizerPerCore((uint)coreIdx, coreVal),
                    $"SetCurveOptimizerPerCore(core={coreIdx}, val={coreVal})");
                break;

            case "--stapm":
                if (!TryParseDoubleArg(args, ref i, out double stapmW, 0, 200)) break;
                exitCode |= RunCommand(controller,
                    () => controller.SetStapmLimit(stapmW),
                    $"SetStapmLimit({stapmW}W)");
                break;

            case "--fast":
                if (!TryParseDoubleArg(args, ref i, out double fastW, 0, 200)) break;
                exitCode |= RunCommand(controller,
                    () => controller.SetFastLimit(fastW),
                    $"SetFastLimit({fastW}W)");
                break;

            case "--slow":
                if (!TryParseDoubleArg(args, ref i, out double slowW, 0, 200)) break;
                exitCode |= RunCommand(controller,
                    () => controller.SetSlowLimit(slowW),
                    $"SetSlowLimit({slowW}W)");
                break;

            case "--ppt-rsmu":
                if (!TryParseDoubleArg(args, ref i, out double pptW, 0, 200)) break;
                exitCode |= RunCommand(controller,
                    () => controller.SetPptLimitRsmu(pptW),
                    $"SetPptLimitRsmu({pptW}W)");
                break;

            case "--temp-mp1":
                if (!TryParseIntArg(args, ref i, out int tempMp1, 40, 115)) break;
                exitCode |= RunCommand(controller,
                    () => controller.SetTempLimitMp1((uint)tempMp1),
                    $"SetTempLimitMp1({tempMp1}°C)");
                break;

            case "--temp-rsmu":
                if (!TryParseIntArg(args, ref i, out int tempRsmu, 40, 115)) break;
                exitCode |= RunCommand(controller,
                    () => controller.SetTempLimitRsmu((uint)tempRsmu),
                    $"SetTempLimitRsmu({tempRsmu}°C)");
                break;

            case "--vrm-mp1":
                if (!TryParseIntArg(args, ref i, out int vrmMa, 0, 200000)) break;
                exitCode |= RunCommand(controller,
                    () => controller.SetVrmCurrentMp1((uint)vrmMa),
                    $"SetVrmCurrentMp1({vrmMa}mA)");
                break;

            case "--edc-mp1":
                if (!TryParseIntArg(args, ref i, out int edcMa, 0, 200000)) break;
                exitCode |= RunCommand(controller,
                    () => controller.SetEdcLimitMp1((uint)edcMa),
                    $"SetEdcLimitMp1({edcMa}mA)");
                break;

            case "--pbo-scalar":
                if (!TryParseIntArg(args, ref i, out int scalar, 0, 10)) break;
                exitCode |= RunCommand(controller,
                    () => controller.SetPboScalar((uint)scalar),
                    $"SetPboScalar({scalar})");
                break;

            case "--oc-clk":
                if (!TryParseIntArg(args, ref i, out int ocMhz, -500, 1000)) break;
                exitCode |= RunCommand(controller,
                    () => controller.SetOcClk(ocMhz),
                    $"SetOcClk({ocMhz}MHz)");
                break;

            case "--oc-volt":
                if (!TryParseIntArg(args, ref i, out int ocMv, 0, 2000)) break;
                exitCode |= RunCommand(controller,
                    () => controller.SetOcVolt((uint)ocMv),
                    $"SetOcVolt({ocMv}mV)");
                break;

            case "--oc-enable":
                i++;
                exitCode |= RunCommand(controller,
                    () => controller.EnableOc(),
                    "EnableOc");
                break;

            case "--oc-disable":
                i++;
                exitCode |= RunCommand(controller,
                    () => controller.DisableOc(),
                    "DisableOc");
                break;

            case "--stapm-time":
                if (!TryParseIntArg(args, ref i, out int timeSec, 1, 3600)) break;
                exitCode |= RunCommand(controller,
                    () => controller.SetStapmTime((uint)timeSec),
                    $"SetStapmTime({timeSec}s)");
                break;

            case "--slow-time":
                if (!TryParseIntArg(args, ref i, out int slowSec, 1, 3600)) break;
                exitCode |= RunCommand(controller,
                    () => controller.SetSlowTime((uint)slowSec),
                    $"SetSlowTime({slowSec}s)");
                break;

            case "--edc-rsmu":
                if (!TryParseIntArg(args, ref i, out int edcRsmuMa, 0, 200000)) break;
                exitCode |= RunCommand(controller,
                    () => controller.SetEdcLimitRsmu((uint)edcRsmuMa),
                    $"SetEdcLimitRsmu({edcRsmuMa}mA)");
                break;

            case "--vrm-rsmu":
                if (!TryParseIntArg(args, ref i, out int vrmRsmuMa, 0, 200000)) break;
                exitCode |= RunCommand(controller,
                    () => controller.SetVrmCurrentRsmu((uint)vrmRsmuMa),
                    $"SetVrmCurrentRsmu({vrmRsmuMa}mA)");
                break;

            default:
                LogError($"未知参数: {arg}");
                Log("使用 --help 查看帮助");
                exitCode = 1;
                i++;
                break;
        }
    }

    // 最后再读一次遥测对比
    Log("");
    Log(">>> 读取当前 SMU 遥测 (操作后) ...");
    LogTelemetry(controller.GetSmuTelemetry());
    Log("");
}
catch (Exception ex)
{
    LogError($"未处理的异常: {ex.GetType().Name}");
    LogError($"  消息: {ex.Message}");
    LogError($"  堆栈: {ex.StackTrace}");
    exitCode = 1;
}
finally
{
    controller?.Dispose();
    Log("");
    Log("PawnIO 驱动已卸载，资源已释放。");
}

// Summary
Log("========================================");
if (exitCode == 0)
    Log("  ✓ 测试完成 — 全部成功");
else
    Log("  ✗ 测试完成 — 有错误，详见上方日志");
Log("========================================");

return exitCode;

// ================================================================
//  Command runner
// ================================================================

static int RunCommand(RyzenSmuController ctrl, Func<CommandResult> action, string label)
{
    Log($">>> 执行 {label} ...");
    var result = action();
    Log($"      Success : {result.Success}");
    Log($"      Message : {result.Message}");

    if (!result.Success)
    {
        LogError($"    {label} 失败: {result.Message}");
        return 1;
    }
    return 0;
}

// ================================================================
//  Arg parsers
// ================================================================

static bool TryParseIntArg(string[] args, ref int i, out int value, int min, int max)
{
    i++;
    value = 0;
    if (i >= args.Length)
    {
        LogError($"参数 {args[i - 1]} 缺少值");
        return false;
    }
    if (!int.TryParse(args[i], out value))
    {
        LogError($"参数 {args[i - 1]} 值 '{args[i]}' 不是有效整数");
        i++;
        return false;
    }
    i++;
    if (value < min || value > max)
        Log($"    (值 {value} 超出范围 [{min}, {max}], 仍继续执行)");
    return true;
}

static bool TryParseDoubleArg(string[] args, ref int i, out double value, double min, double max)
{
    i++;
    value = 0;
    if (i >= args.Length)
    {
        LogError($"参数 {args[i - 1]} 缺少值");
        return false;
    }
    if (!double.TryParse(args[i], out value))
    {
        LogError($"参数 {args[i - 1]} 值 '{args[i]}' 不是有效数值");
        i++;
        return false;
    }
    i++;
    if (value < min || value > max)
        Log($"    (值 {value} 超出范围 [{min}, {max}], 仍继续执行)");
    return true;
}

// ================================================================
//  Output helpers
// ================================================================

static void WriteBanner(string[] args)
{
    Log("╔══════════════════════════════════════════╗");
    Log("║   SMU 命令行测试工具                     ║");
    Log($"║   {string.Join(" ", args).Truncate(34)}");
    Log("╚══════════════════════════════════════════╝");
    Log("");
}

static void PrintHelp()
{
    Console.WriteLine(@"
SMU 命令行测试工具 — 支持所有 RyzenSmuController 操作

用法: RyzenSmuControllerTest [选项...]

=== 遥测 ===
  -t, --telemetry          仅读取遥测 (默认行为)

=== Curve Optimizer ===
  --curve-all <val>        全核 Curve Optimizer (-30 ~ 30)
  --curve-core <idx> <val> 单核 Curve Optimizer (例: --curve-core 0 -15)

=== 功耗限制 ===
  --stapm <watts>          STAPM Limit (W)
  --stapm-time <sec>       STAPM Time (秒)
  --fast <watts>           Fast Limit (W)
  --slow <watts>           Slow Limit (W)
  --slow-time <sec>        Slow Time (秒)
  --ppt-rsmu <watts>       PPT Limit RSMU (W)

=== 电流限制 ===
  --vrm-mp1 <mA>           VRM Current MP1
  --vrm-rsmu <mA>          VRM Current RSMU
  --edc-mp1 <mA>           EDC Limit MP1
  --edc-rsmu <mA>          EDC Limit RSMU

=== 温度限制 ===
  --temp-mp1 <°C>          Temp Limit MP1
  --temp-rsmu <°C>         Temp Limit RSMU

=== PBO & 超频 ===
  --pbo-scalar <val>       PBO Scalar (0~10)
  --oc-enable              启用 OC Mode
  --oc-disable             禁用 OC Mode
  --oc-clk <MHz>           OC Clock 偏移
  --oc-volt <mV>           OC Voltage

=== 其他 ===
  -h, --help               显示帮助

例: RyzenSmuControllerTest --curve-all -20
    RyzenSmuControllerTest --stapm 45 --fast 55 --temp-mp1 85
    RyzenSmuControllerTest --curve-core 0 -25 --curve-core 1 -20
");
}

static void Log(string msg)
{
    var ts = DateTime.Now.ToString("HH:mm:ss.fff");
    Console.WriteLine($"[{ts}] {msg}");
}

static void LogError(string msg)
{
    var original = Console.ForegroundColor;
    Console.ForegroundColor = ConsoleColor.Red;
    var ts = DateTime.Now.ToString("HH:mm:ss.fff");
    Console.WriteLine($"[{ts}] [ERR] {msg}");
    Console.ForegroundColor = original;
}

static void LogTelemetry(CommandResult telemetryResult)
{
    if (!telemetryResult.Success)
    {
        LogError($"  遥测读取失败: {telemetryResult.Message}");
        return;
    }

    var data = telemetryResult.Data;
    if (data == null)
    {
        Log("  遥测数据: null");
        return;
    }

    var type = data.GetType();
    foreach (var prop in type.GetProperties())
    {
        var value = prop.GetValue(data);
        var unit = prop.Name switch
        {
            "Ppt" or "Tdc" or "Edc" => "W",
            "Temp" => "°C",
            "FreqMhz" => "MHz",
            "Usage" => "%",
            _ => ""
        };
        Log($"    {prop.Name,-10}: {value,8} {unit}");
    }
}

// ================================================================
//  String helper
// ================================================================

public static class StringExtensions
{
    public static string Truncate(this string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value.Length <= maxLength ? value : value[..(maxLength - 3)] + "...";
    }
}
