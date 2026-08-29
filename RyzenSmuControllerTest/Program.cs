using System.Runtime.InteropServices;
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
    Log($"    检测到 CPU 架构: {controller.CurrentFamily}");
    Log("    (PawnIO 驱动将在首次执行 SMU 写操作时连接)");
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

            case "--dump-sensors":
                i++;
                DumpSensors();
                break;

            case "--dump-svi":
                i++;
                DumpSviTelemetry(controller);
                break;

            case "--smu-read":
                if (!TryParseHexArg(args, ref i, out uint smuAddr)) break;
                DumpSmuRegister(controller, smuAddr);
                break;

            case "--cpu-vid":
                i++;
                DumpCpuVidFromMsr();
                break;

            case "--core-voltage":
                i++;
                DumpCoreVoltage(controller);
                break;

            case "--vid-per-core":
                i++;
                DumpVidPerCore();
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
    Log("客户端资源已释放。");
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

const uint SviBase = 0x0005A000;

static void DumpSviTelemetry(RyzenSmuController ctrl)
{
    Log(">>> 读取 SMN SVI 遥测寄存器 ...");
    try
    {
        uint tfn = (uint)ctrl.Execute("ioctl_read_smu_register", new ulong[] { SviBase + 0x8 }, 1)[0];
        uint plane0 = (uint)ctrl.Execute("ioctl_read_smu_register", new ulong[] { SviBase + 0x10 }, 1)[0];
        uint plane1 = (uint)ctrl.Execute("ioctl_read_smu_register", new ulong[] { SviBase + 0xC }, 1)[0];

        double vcore = 1.550 - 0.00625 * ((plane0 >> 16) & 0xff);
        double vsoc = 1.550 - 0.00625 * ((plane1 >> 16) & 0xff);

        Log($"    SVI0_TFN   : 0x{tfn:X8}");
        Log($"    Plane0     : 0x{plane0:X8}  VCore = {vcore:F3} V");
        Log($"    Plane1     : 0x{plane1:X8}  SoC   = {vsoc:F3} V");
    }
    catch (Exception ex)
    {
        LogError($"    SVI 遥测读取失败: {ex.Message}");
    }
    Log("");
}

static void DumpSmuRegister(RyzenSmuController ctrl, uint addr)
{
    Log($">>> 读取 SMU 寄存器 0x{addr:X6} (3 次采样) ...");
    try
    {
        for (int i = 0; i < 3; i++)
        {
            uint val = (uint)ctrl.Execute("ioctl_read_smu_register", new ulong[] { addr }, 1)[0];
            Log($"    0x{addr:X6} = 0x{val:X8} ({val})");
            Thread.Sleep(200);
        }
    }
    catch (Exception ex)
    {
        LogError($"    读取失败: {ex.Message}");
    }
    Log("");
}

// CPU-Z 读核心电压的机制：通过 MSR 读当前 VID 再换算 (V = 1.550 - VID * 0.00625)。
// 候选寄存器：0xC0010293 (FIDVID_STATUS, VID=[15:6])、0xC0010071 (COFVID_STATUS)。
// 这里直接加载 LHM 的 AMDFamily17.bin 模块（含 ioctl_read_msr）来验证。
static void DumpCpuVidFromMsr()
{
    Log(">>> 通过 MSR 读取当前 VID (CPU-Z 机制) ...");

    const string dllName = "PawnIOLib.dll";
    string dllPath = Path.Combine(AppContext.BaseDirectory, dllName);
    if (!File.Exists(dllPath))
        dllPath = Path.Combine(@"C:\Program Files\PawnIO", dllName);
    if (!File.Exists(dllPath))
    {
        LogError($"    未找到 {dllName}，请确认 PawnIO 已安装");
        return;
    }

    var hModule = NativeMethods.LoadLibrary(dllPath);
    if (hModule == IntPtr.Zero)
    {
        LogError($"    加载 {dllName} 失败 (0x{Marshal.GetLastWin32Error():X})");
        return;
    }

    try
    {
        if (NativeMethods.pawnio_open(out IntPtr handle) != 0)
        {
            LogError($"    pawnio_open 失败，请以管理员身份运行");
            return;
        }

        try
        {
            string binPath = Path.Combine(AppContext.BaseDirectory, "Drivers", "PawnIO", "AMDFamily17.bin");
            byte[] blob = File.ReadAllBytes(binPath);
            if (NativeMethods.pawnio_load(handle, blob, (UIntPtr)blob.Length) != 0)
            {
                LogError("    加载 AMDFamily17.bin 失败");
                return;
            }

            uint[] msrs = { 0xC0010293, 0xC0010071 };
            foreach (uint msr in msrs)
            {
                Log($"    MSR 0x{msr:X8}:");
                for (int i = 0; i < 5; i++)
                {
                    ulong[] input = { msr };
                    var output = new ulong[1];
                    NativeMethods.pawnio_execute(handle, "ioctl_read_msr", input, (UIntPtr)1, output, (UIntPtr)1, out _);
                    ulong raw = output[0];
                    uint vid = (uint)((raw >> 6) & 0xFF);
                    double volts = 1.550 - vid * 0.00625;
                    Log($"      0x{raw:X16}  VID={vid,4} ({vid * 0.00625:F3}V) -> Vcore = {volts:F3} V");
                    Thread.Sleep(200);
                }
            }
        }
        finally
        {
            NativeMethods.pawnio_close(handle);
        }
    }
    finally
    {
        NativeMethods.FreeLibrary(hModule);
    }
    Log("");
}

// 打开 AMDFamily17.bin executor（含 ioctl_read_msr），返回 executor 句柄；失败返回 false
static bool TryOpenAmd17Executor(out IntPtr executor)
{
    executor = IntPtr.Zero;

    string dllPath = Path.Combine(AppContext.BaseDirectory, "PawnIOLib.dll");
    if (!File.Exists(dllPath))
        dllPath = Path.Combine(@"C:\Program Files\PawnIO", "PawnIOLib.dll");
    if (!File.Exists(dllPath))
    {
        LogError($"    未找到 PawnIOLib.dll，请确认 PawnIO 已安装");
        return false;
    }

    // 仅用于验证实验；句柄由进程退出时释放，测试工具无需严格清理
    NativeMethods.LoadLibrary(dllPath);

    if (NativeMethods.pawnio_open(out executor) != 0)
    {
        LogError($"    pawnio_open 失败，请以管理员身份运行");
        return false;
    }

    string binPath = Path.Combine(AppContext.BaseDirectory, "Drivers", "PawnIO", "AMDFamily17.bin");
    byte[] blob = File.ReadAllBytes(binPath);
    if (NativeMethods.pawnio_load(executor, blob, (UIntPtr)blob.Length) != 0)
    {
        LogError("    加载 AMDFamily17.bin 失败");
        NativeMethods.pawnio_close(executor);
        executor = IntPtr.Zero;
        return false;
    }

    return true;
}

static ulong ReadMsrViaAmd17(IntPtr executor, uint msrIndex)
{
    ulong[] input = { msrIndex };
    var output = new ulong[1];
    NativeMethods.pawnio_execute(executor, "ioctl_read_msr", input, (UIntPtr)1, output, (UIntPtr)1, out _);
    return output[0];
}

static void DumpVidPerCore()
{
    Log(">>> 各逻辑核心 FIDVID_STATUS 电压 (设置线程亲和性后读取) ...");
    if (!TryOpenAmd17Executor(out IntPtr executor))
        return;

    try
    {
        int coreCount = Environment.ProcessorCount;
        for (int core = 0; core < coreCount; core++)
        {
            IntPtr mask = new IntPtr(1L << core);
            IntPtr prev = NativeMethods.SetThreadAffinityMask(NativeMethods.GetCurrentThread(), mask);
            if (prev == IntPtr.Zero)
                continue;

            ulong raw = ReadMsrViaAmd17(executor, 0xC0010293);
            NativeMethods.SetThreadAffinityMask(NativeMethods.GetCurrentThread(), prev);

            uint vid = (uint)((raw >> 6) & 0xFF);
            double volts = 1.550 - vid * 0.00625;
            Log($"    Core {core,2}: VID={vid,3} -> {volts:F3} V   (0x{raw:X12})");
            Thread.Sleep(50);
        }
    }
    finally
    {
        NativeMethods.pawnio_close(executor);
    }
    Log("");
}

static void DumpCoreVoltage(RyzenSmuController ctrl)
{
    Log(">>> 通过主项目 GetCoreVoltage() 读取核心电压 (8 次采样) ...");
    for (int i = 0; i < 8; i++)
    {
        var v = ctrl.GetCoreVoltage();
        Log($"    {i + 1}: {(v.HasValue ? $"{v.Value:F3} V" : "null")}");
        Thread.Sleep(250);
    }
    Log("");
}

static void DumpSensors()
{
    Log(">>> 枚举 LibreHardwareMonitor CPU 传感器 ...");
    try
    {
        using var searcher = new System.Management.ManagementObjectSearcher("SELECT Name FROM Win32_Processor");
        foreach (var obj in searcher.Get())
        {
            Log($"    CPU: {obj["Name"]}");
            break;
        }
    }
    catch { }

    var computer = new LibreHardwareMonitor.Hardware.Computer
    {
        IsCpuEnabled = true,
    };
    computer.Open();
    try
    {
        foreach (var hardware in computer.Hardware)
        {
            if (hardware.HardwareType != LibreHardwareMonitor.Hardware.HardwareType.Cpu)
                continue;

            Log($"  [硬件] {hardware.Name} ({hardware.HardwareType})");
            DumpHardwareSensors(hardware);

            foreach (var sub in hardware.SubHardware)
            {
                Log($"    [子硬件] {sub.Name} ({sub.HardwareType})");
                DumpHardwareSensors(sub, 6);
            }
        }
    }
    finally
    {
        computer.Close();
    }
    Log("");
}

static void DumpHardwareSensors(LibreHardwareMonitor.Hardware.IHardware hardware, int indent = 4)
{
    hardware.Update();
    foreach (var sensor in hardware.Sensors)
    {
        Log($"{new string(' ', indent)}{sensor.SensorType,-12} {sensor.Name,-32} = {sensor.Value?.ToString() ?? "N/A"}");
    }
}

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

static bool TryParseHexArg(string[] args, ref int i, out uint value)
{
    i++;
    value = 0;
    if (i >= args.Length)
    {
        LogError($"参数 {args[i - 1]} 缺少值");
        return false;
    }
    if (!uint.TryParse(args[i], System.Globalization.NumberStyles.HexNumber, null, out value))
    {
        LogError($"参数 {args[i - 1]} 值 '{args[i]}' 不是有效十六进制数");
        i++;
        return false;
    }
    i++;
    return true;
}

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
  --dump-sensors           枚举 LibreHardwareMonitor 的 CPU 传感器
  --dump-svi               读取 SMN SVI 遥测寄存器并解码电压
  --smu-read <hex>         读取任意 SMU/SMN 寄存器 (例: --smu-read 3B10570)
  --cpu-vid                通过 MSR 读取当前 VID 电压 (CPU-Z 机制)
  --core-voltage           通过主项目 GetCoreVoltage() 读取核心电压
  --vid-per-core           遍历各逻辑核心读取 VID 电压 (验证核心差异)

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

internal static class NativeMethods
{
    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr LoadLibrary(string lpFileName);

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    public static extern bool FreeLibrary(IntPtr hModule);

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    public static extern IntPtr GetCurrentThread();

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr SetThreadAffinityMask(IntPtr hThread, IntPtr dwThreadAffinityMask);

    [System.Runtime.InteropServices.DllImport("PawnIOLib.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern int pawnio_open(out IntPtr handle);

    [System.Runtime.InteropServices.DllImport("PawnIOLib.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern int pawnio_load(IntPtr handle, byte[] blob, UIntPtr size);

    [System.Runtime.InteropServices.DllImport("PawnIOLib.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern int pawnio_execute(
        IntPtr handle,
        [MarshalAs(UnmanagedType.LPStr)] string name,
        ulong[] input,
        UIntPtr inSize,
        ulong[] output,
        UIntPtr outSize,
        out UIntPtr returnSize
    );

    [System.Runtime.InteropServices.DllImport("PawnIOLib.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern int pawnio_close(IntPtr handle);
}
