namespace RyzenSmuControllerTest.Cli;

/// <summary>--help 文案。手写分组说明；CommandRegistry 是选项名与行为的事实来源，新增选项时两处同步。</summary>
internal static class HelpText
{
    public static void Print()
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
  --smu-cmd <cmd> <arg> <mp1|rsmu>
                           调试: 发送原始 SMU 邮箱命令 (hex, 例: --smu-cmd 6 FFFEC rsmu)
  --smu-cmd-read <cmd> <arg> <mp1|rsmu>
                           调试: 发送命令并回读邮箱参数槽 (例: GetDldoPsmMargin: --smu-cmd-read D5 1000000 rsmu)

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
  --selftest               无硬件自检: 验证 CPU → SMU 平台判定表后退出
  -h, --help               显示帮助

例: RyzenSmuControllerTest --curve-all -20
    RyzenSmuControllerTest --stapm 45 --fast 55 --temp-mp1 85
    RyzenSmuControllerTest --curve-core 0 -25 --curve-core 1 -20
");
    }
}
