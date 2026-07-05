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
        bridge.CPU.SetCpuLongPower(bridge.Config.Cpu.CpuLongPower);
        bridge.CPU.SetCpuShortPower(bridge.Config.Cpu.CpuShortPower);
        bridge.CPU.SetCPUTempWall(bridge.Config.Cpu.CpuTempWall);
        bridge.Power.SetCPUMaxFrequency(bridge.Config.Cpu.CpuMaxFrequency);
        if (bridge.Config.Cpu.CpuTurbo)
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
