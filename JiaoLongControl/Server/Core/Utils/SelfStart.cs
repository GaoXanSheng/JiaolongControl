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
        bridge.NvidiaGpu.LockGpuClock(gpu.GpuClock);
        bridge.NvidiaGpu.LockMemoryClock(gpu.MemoryClock);
        // bridge.NvidiaGpu.SetPowerLimit(gpu.PowerLimit);
        // if (gpu.CoreClockOffset != 0 || gpu.MemoryClockOffset != 0)
            // bridge.NvidiaGpu.ApplyClockOffsets(gpu.CoreClockOffset, gpu.MemoryClockOffset);
        // if (gpu.VoltageBoostPercent > 0)
            // bridge.NvidiaGpu.SetVoltageBoostPercent(gpu.VoltageBoostPercent);
    }
}
