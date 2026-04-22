using JiaoLongControl.Server.Core.Controllers;
using JiaoLongControl.Server.Core.Models;
using JiaoLongControl.Server.Interop;

namespace JiaoLongControl.Server.Core.Utils;

public class SelfStart
{
    private Config _config = ConfigController.Config;
    public SelfStart()
    {
        if (_config.BootAdvancedFanControlSystem) Fan();
        if (_config.BootAdvancedCPUSystem) CPU();
        if (_config.BootAdvancedGPUSystem) GPU();
        if (_config.BootSetRyzenSumCurveOptimizerAll)
        {
            Bridge.Instance.RyzenSmu.SetCurveOptimizerAll(_config.RyzenSumConfig.CurveOptimizerAll);
        }
    }

    private void Fan()
    {
        Bridge.Instance.AutoFan.Start();
    }

    private void CPU()
    {
        Bridge.Instance.CPU.SetCpuLongPower(_config.AdvancedCPUSystemConfig.CpuLongPower);
        Bridge.Instance.CPU.SetCpuShortPower(_config.AdvancedCPUSystemConfig.CpuShortPower);
        Bridge.Instance.CPU.SetCPUTempWall(_config.AdvancedCPUSystemConfig.CpuTempWall);
        Bridge.Instance.Power.SetCPUMaxFrequency(_config.AdvancedCPUSystemConfig.CpuMaxFrequency);
        if (_config.AdvancedCPUSystemConfig.CpuTurbo)
            Bridge.Instance.Power.EnableTurbo();
        else
            Bridge.Instance.Power.DisableTurbo();
    }

    private void GPU()
    {
        Bridge.Instance.NvidiaGpu.LockGpuClock(_config.NvidiaGpuConfig.GpuClock);
        Bridge.Instance.NvidiaGpu.LockMemoryClock(_config.NvidiaGpuConfig.MemoryClock);
        Bridge.Instance.NvidiaGpu.SetPowerLimit(_config.NvidiaGpuConfig.PowerLimit);
    }
}