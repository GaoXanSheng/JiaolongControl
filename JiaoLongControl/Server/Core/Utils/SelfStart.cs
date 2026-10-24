using JiaoLongControl.Server.Interop;
using log4net;

namespace JiaoLongControl.Server.Core.Utils;

public class SelfStart
{
    private static readonly ILog Logger = LogManager.GetLogger(typeof(SelfStart));

    public SelfStart()
    {
        var bridge = Bridge.Instance;
        if (bridge.Config.App.BootAdvancedFanControlSystem) Fan();
        if (bridge.Config.App.BootAdvancedCPUSystem) CPU();
        if (bridge.Config.App.BootAdvancedGPUSystem) GPU();
        if (bridge.Config.App.BootSetRyzenSumCurveOptimizerAll)
        {
            if (bridge.Config.App.BootSetRyzenSmuCurveOptimizerPerCore)
            {
                // 分核模式：逐核写入已保存的偏移（含0值，用于重置之前应用的偏移）；列表为空时不写入
                var perCore = bridge.Config.Smu.CurveOptimizerPerCore;
                for (int i = 0; i < perCore.Count; i++)
                    bridge.RyzenSmu.SetCurveOptimizerPerCore((uint)i, perCore[i]);
            }
            else
            {
                bridge.RyzenSmu.SetCurveOptimizerAll(bridge.Config.Smu.CurveOptimizerAll);
            }
        }
        Smu();
        if (bridge.Config.App.BootKeyboardGradient) bridge.KeyboardGradient.Start();
    }

    private void Smu()
    {
        var smu = Bridge.Instance.Config.Smu;
        var controller = Bridge.Instance.RyzenSmu;
        foreach (var key in smu.BootApplyItems)
        {
            CommandResult? res = key switch
            {
                "StapmLimit" when smu.StapmLimit > 0 => controller.SetStapmLimit(smu.StapmLimit),
                "StapmTime" when smu.StapmTime > 0 => controller.SetStapmTime((uint)smu.StapmTime),
                "FastLimit" when smu.FastLimit > 0 => controller.SetFastLimit(smu.FastLimit),
                "SlowLimit" when smu.SlowLimit > 0 => controller.SetSlowLimit(smu.SlowLimit),
                "SlowTime" when smu.SlowTime > 0 => controller.SetSlowTime((uint)smu.SlowTime),
                "PptLimitRsmu" when smu.PptLimitRsmu > 0 => controller.SetPptLimitRsmu(smu.PptLimitRsmu),
                "VrmCurrentMp1" when smu.VrmCurrentMp1 > 0 => controller.SetVrmCurrentMp1((uint)smu.VrmCurrentMp1),
                "VrmCurrentRsmu" when smu.VrmCurrentRsmu > 0 => controller.SetVrmCurrentRsmu((uint)smu.VrmCurrentRsmu),
                "EdcLimitMp1" when smu.EdcLimitMp1 > 0 => controller.SetEdcLimitMp1((uint)smu.EdcLimitMp1),
                "EdcLimitRsmu" when smu.EdcLimitRsmu > 0 => controller.SetEdcLimitRsmu((uint)smu.EdcLimitRsmu),
                "TempLimitMp1" when smu.TempLimitMp1 > 0 => controller.SetTempLimitMp1((uint)smu.TempLimitMp1),
                "TempLimitRsmu" when smu.TempLimitRsmu > 0 => controller.SetTempLimitRsmu((uint)smu.TempLimitRsmu),
                "PboScalar" when smu.PboScalar > 0 => controller.SetPboScalar((uint)smu.PboScalar),
                "OcClk" when smu.OcClk != 0 => controller.SetOcClk(smu.OcClk),
                "OcVolt" when smu.OcVolt > 0 => controller.SetOcVolt((uint)smu.OcVolt),
                _ => null,
            };
            if (res == null)
                Logger.Info($"开机应用 SMU {key}: 跳过 (值为 0 或字段不存在)");
            else if (!res.Success)
                Logger.Warn($"开机应用 SMU {key} 失败: {res.Message}");
        }
    }

    private void Fan() { Bridge.Instance.AutoFan.Start(); }

    private void CPU()
    {
        var bridge = Bridge.Instance;
        var cpu = bridge.Config.Cpu.Active;
        bridge.CPU.SetCpuLongPower(cpu.CpuLongPower);
        bridge.CPU.SetCpuShortPower(cpu.CpuShortPower);
        bridge.CPU.SetCPUTempWall(cpu.CpuTempWall);
        bridge.Power.SetCPUMaxFrequency(cpu.CpuMaxFrequency);
        if (cpu.CpuTurbo)
            bridge.Power.EnableTurbo();
        else
            bridge.Power.DisableTurbo();
    }

    private void GPU()
    {
        var bridge = Bridge.Instance;
        var gpu = bridge.Config.Gpu;
        if (bridge.Config.App.BootGpuUseAdvancedOffsets)
        {
            // 高级超频: 应用已保存的核心/显存频率偏移 (内部自含清零+读回验证+失败回滚,
            // 电源状态过渡期曲线读取失败等场景由其返回失败结果, 不抛出不阻塞其它自启项); 均为 0 时无需写入
            if (gpu.CoreClockOffsetMhz != 0 || gpu.MemoryClockOffsetMhz != 0)
                bridge.NvidiaGpu.SetGpuOffsets(gpu.CoreClockOffsetMhz, gpu.MemoryClockOffsetMhz);
        }
        else
        {
            bridge.NvidiaGpu.LockGpuClock(gpu.GpuClock);
            bridge.NvidiaGpu.LockMemoryClock(gpu.MemoryClock);
            // bridge.NvidiaGpu.SetPowerLimit(gpu.PowerLimit);
        }
    }
}
