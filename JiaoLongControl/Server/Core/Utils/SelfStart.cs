using JiaoLongControl.Server.Core.Models;
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
            bridge.RyzenSmu.SetCurveOptimizerAll(bridge.Config.Smu.CurveOptimizerAll);
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
        bridge.NvidiaGpu.LockGpuClock(bridge.Config.Gpu.GpuClock);
        bridge.NvidiaGpu.LockMemoryClock(bridge.Config.Gpu.MemoryClock);
        bridge.NvidiaGpu.SetPowerLimit(bridge.Config.Gpu.PowerLimit);
    }
}
