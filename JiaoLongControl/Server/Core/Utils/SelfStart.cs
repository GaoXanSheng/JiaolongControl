using JiaoLongControl.Server.Core.Controllers;
using JiaoLongControl.Server.Core.Models;
using JiaoLongControl.Server.Interop;

namespace JiaoLongControl.Server.Core.Utils;

public class SelfStart
{
    private readonly bool _startInFan;
    private readonly bool _startInCPU;
    private readonly bool _startInGPU;
    private Config _config = ConfigController.Config;

    public SelfStart()
    {
        _startInFan = _config.BootAdvancedFanControlSystem;
        _startInCPU = _config.BootAdvancedCPUSystem;
        _startInGPU = _config.BootAdvancedGPUSystem;

        if (_startInFan) Fan();
        if (_startInCPU) CPU();
        if (_startInGPU) GPU();
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