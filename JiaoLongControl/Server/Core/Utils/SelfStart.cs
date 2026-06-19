using JiaoLongControl.Server.Core.Models;
using JiaoLongControl.Server.Interop;

namespace JiaoLongControl.Server.Core.Utils;

public class SelfStart
{
    public SelfStart()
    {
        var bridge = Bridge.Instance;
        if (bridge.AppConfig.BootAdvancedFanControlSystem) Fan();
        if (bridge.AppConfig.BootAdvancedCPUSystem) CPU();
        if (bridge.AppConfig.BootAdvancedGPUSystem) GPU();
        if (bridge.AppConfig.BootSetRyzenSumCurveOptimizerAll)
            bridge.RyzenSmu.SetCurveOptimizerAll(bridge.SmuConfig.CurveOptimizerAll);
    }

    private void Fan() { Bridge.Instance.AutoFan.Start(); }

    private void CPU()
    {
        var bridge = Bridge.Instance;
        bridge.CPU.SetCpuLongPower(bridge.CpuConfig.CpuLongPower);
        bridge.CPU.SetCpuShortPower(bridge.CpuConfig.CpuShortPower);
        bridge.CPU.SetCPUTempWall(bridge.CpuConfig.CpuTempWall);
        bridge.Power.SetCPUMaxFrequency(bridge.CpuConfig.CpuMaxFrequency);
        if (bridge.CpuConfig.CpuTurbo)
            bridge.Power.EnableTurbo();
        else
            bridge.Power.DisableTurbo();
    }

    private void GPU()
    {
        var bridge = Bridge.Instance;
        bridge.NvidiaGpu.LockGpuClock(bridge.GpuConfig.GpuClock);
        bridge.NvidiaGpu.LockMemoryClock(bridge.GpuConfig.MemoryClock);
        bridge.NvidiaGpu.SetPowerLimit(bridge.GpuConfig.PowerLimit);
    }
}
