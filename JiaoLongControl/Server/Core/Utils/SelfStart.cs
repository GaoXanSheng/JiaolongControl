using JiaoLongControl.Server.Interop;

namespace JiaoLongControl.Server.Core.Utils;

public class SelfStart
{
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
        if (bridge.Config.App.BootKeyboardGradient) bridge.KeyboardGradient.Start();
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
