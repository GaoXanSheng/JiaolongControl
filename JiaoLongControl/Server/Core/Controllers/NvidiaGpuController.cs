using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using JiaoLongControl.Server.Core.Drivers;
using JiaoLongControl.Server.Core.Models;
using JiaoLongControl.Server.Core.Native;
using JiaoLongControl.Server.Core.Utils;
using JiaoLongControl.Server.Interop;
using NvAPIWrapper;
using NvAPIWrapper.GPU;

namespace JiaoLongControl.Server.Core.Controllers
{
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.AutoDual)]
    public class NvidiaGpuController : IDisposable
    {
        public NvidiaGpuController()
        {
            NVIDIA.Initialize();
        }

        private PhysicalGPU GetGPU(int gpuIndex)
        {
            var gpus = PhysicalGPU.GetPhysicalGPUs();
            if (gpus.Length == 0)
                throw new InvalidOperationException("没有找到 NVIDIA GPU");
            int idx = gpuIndex >= 0 && gpuIndex < gpus.Length ? gpuIndex : 0;
            return gpus[idx];
        }

        public class GpuStatsInfo
        {
            public string GpuName { get; set; } = "";
            public string DriverVersion { get; set; } = "";
            public string DriverDate { get; set; } = "Unknown";
            public string MemoryTotal { get; set; } = "";
            public string BusWidth { get; set; } = "";
            public string GpuUtilization { get; set; } = "";
            public string MemoryUtilization { get; set; } = "";
            public string CoreClock { get; set; } = "";
            public string MemoryClock { get; set; } = "";
            public string GpuTemperature { get; set; } = "";
            public string FanSpeed { get; set; } = "";
        }

        public CommandResult GetGpuAllStats(int gpuIndex = -1)
        {
            try
            {
                var gpu = GetGPU(gpuIndex);
                var stats = new GpuStatsInfo
                {
                    GpuName = gpu.FullName,
                    DriverVersion = $"{(NVIDIA.DriverVersion / 100).ToString()}.{(NVIDIA.DriverVersion % 100).ToString()}",
                    MemoryTotal = $"{(gpu.MemoryInformation.DedicatedVideoMemoryInkB / 1024).ToString()} MiB",
                    BusWidth = $"x{gpu.BusInformation.CurrentPCIeLanes}",
                    GpuUtilization = gpu.UsageInformation.GPU.Percentage.ToString(),
                    MemoryUtilization = gpu.UsageInformation.FrameBuffer.Percentage.ToString(),
                    CoreClock = ((int)(gpu.CurrentClockFrequencies.GraphicsClock.Frequency / 1000)).ToString(),
                    MemoryClock = ((int)(gpu.CurrentClockFrequencies.MemoryClock.Frequency / 1000)).ToString(),
                    GpuTemperature = gpu.ThermalInformation.ThermalSensors.First().CurrentTemperature.ToString(),
                    FanSpeed = GetGpuFanSpeed(gpuIndex).Data?.ToString() ?? "0"
                };
                return new CommandResult(true, "获取成功", stats);
            }
            catch (Exception ex)
            {
                return new CommandResult(false, ex.Message);
            }
        }

        public CommandResult GetGpuName(int gpuIndex = -1)
        {
            try { return new CommandResult(true, "获取成功", GetGPU(gpuIndex).FullName); }
            catch (Exception ex) { return new CommandResult(false, ex.Message); }
        }

        public CommandResult GetGpuDriverVersion(int gpuIndex = -1)
        {
            try
            {
                uint ver = NVIDIA.DriverVersion;
                return new CommandResult(true, "获取成功", $"{(ver / 100).ToString()}.{(ver % 100).ToString()}");
            }
            catch (Exception ex) { return new CommandResult(false, ex.Message); }
        }

        public CommandResult GetGpuMemoryTotal(int gpuIndex = -1)
        {
            try { return new CommandResult(true, "获取成功", $"{(GetGPU(gpuIndex).MemoryInformation.DedicatedVideoMemoryInkB / 1024).ToString()} MiB"); }
            catch (Exception ex) { return new CommandResult(false, ex.Message); }
        }

        public CommandResult GetGpuBusWidth(int gpuIndex = -1)
        {
            try { return new CommandResult(true, "获取成功", $"x{GetGPU(gpuIndex).BusInformation.CurrentPCIeLanes}"); }
            catch (Exception ex) { return new CommandResult(false, ex.Message); }
        }

        public CommandResult GetGpuUtilization(int gpuIndex = -1)
        {
            try { return new CommandResult(true, "获取成功", GetGPU(gpuIndex).UsageInformation.GPU.Percentage); }
            catch (Exception ex) { return new CommandResult(false, ex.Message); }
        }

        public CommandResult GetGpuMemoryUtilization(int gpuIndex = -1)
        {
            try { return new CommandResult(true, "获取成功", GetGPU(gpuIndex).UsageInformation.FrameBuffer.Percentage); }
            catch (Exception ex) { return new CommandResult(false, ex.Message); }
        }

        public CommandResult GetGpuCoreClock(int gpuIndex = -1)
        {
            try
            {
                var gpu = GetGPU(gpuIndex);
                int clock = (int)(gpu.BoostClockFrequencies.GraphicsClock.Frequency / 1000);
                if (clock == 0)
                {
                    clock = (int)(gpu.CurrentClockFrequencies.GraphicsClock.Frequency / 1000);
                }
                return new CommandResult(true, "获取成功", clock);
            }
            catch (Exception ex) { return new CommandResult(false, ex.Message); }
        }

        public CommandResult GetGpuMemoryClock(int gpuIndex = -1)
        {
            try
            {
                var gpu = GetGPU(gpuIndex);
                int clock = (int)(gpu.BoostClockFrequencies.MemoryClock.Frequency / 1000);
                if (clock == 0)
                {
                    clock = (int)(gpu.CurrentClockFrequencies.MemoryClock.Frequency / 1000);
                }
                return new CommandResult(true, "获取成功", clock);
            }
            catch (Exception ex) { return new CommandResult(false, ex.Message); }
        }

        public CommandResult GetGpuTemperature(int gpuIndex = -1)
        {
            try { return new CommandResult(true, "获取成功", GetGPU(gpuIndex).ThermalInformation.ThermalSensors.First().CurrentTemperature); }
            catch (Exception ex) { return new CommandResult(false, ex.Message); }
        }

        public CommandResult GetGpuFanSpeed(int gpuIndex = -1)
        {
            try { return new CommandResult(true, "获取成功", (int)GetGPU(gpuIndex).CoolerInformation.Coolers.First().CurrentLevel); }
            catch
            {
                try
                {
                    int ecFan = ((FanSpeedInfo)Bridge.Instance.Fan.GetFanSpeed().Data).GPUFanSpeed;
                    return new CommandResult(true, "获取成功", ecFan);
                }
                catch (Exception ex) { return new CommandResult(false, ex.Message); }
            }
        }

        public CommandResult GetGpuCoreClockRange(int gpuIndex = -1)
        {
            try
            {
                var gpu = GetGPU(gpuIndex);
                int baseMhz = (int)(gpu.BaseClockFrequencies.GraphicsClock.Frequency / 1000);
                int boostMhz = (int)(gpu.BoostClockFrequencies.GraphicsClock.Frequency / 1000);
                if (baseMhz == 0 || boostMhz == 0) throw new Exception("Invalid clocks");
                return new CommandResult(true, "获取成功", new { Min = baseMhz, Max = boostMhz });
            }
            catch { return new CommandResult(true, "获取成功 (Fallback)", new { Min = 0, Max = 3500 }); }
        }

        public CommandResult GetGpuMemoryClockRange(int gpuIndex = -1)
        {
            try
            {
                var gpu = GetGPU(gpuIndex);
                int baseMem = (int)(gpu.BaseClockFrequencies.MemoryClock.Frequency / 1000);
                int boostMem = (int)(gpu.BoostClockFrequencies.MemoryClock.Frequency / 1000);

                if (boostMem == 0) boostMem = (int)(gpu.CurrentClockFrequencies.MemoryClock.Frequency / 1000);
                if (baseMem == 0) baseMem = Math.Max(0, boostMem - 2000);

                if (boostMem == 0) throw new Exception("Invalid memory clock");

                int minMhz = Math.Min(baseMem, boostMem);
                int maxMhz = Math.Max(baseMem, boostMem);

                if (minMhz == maxMhz || minMhz == 0)
                {
                    minMhz = Math.Max(0, maxMhz - 2000);
                }

                return new CommandResult(true, "获取成功", new { Min = minMhz, Max = maxMhz });
            }
            catch { return new CommandResult(true, "获取成功 (Fallback)", new { Min = 0, Max = 10000 }); }
        }

        public CommandResult GetGpuPowerLimitRange(int gpuIndex = -1)
        {
            try
            {
                var gpu = GetGPU(gpuIndex);
                var info = gpu.PerformanceControl.PowerLimitInformation.First();
                int minW = (int)(info.MinimumPowerInPCM / 1000);
                int maxW = (int)(info.MaximumPowerInPCM / 1000);
                if (maxW == 0) throw new Exception("Invalid power limit");
                return new CommandResult(true, "获取成功", new { Min = minW, Max = maxW });
            }
            catch { return new CommandResult(true, "获取成功 (Fallback)", new { Min = 50, Max = 175 }); }
        }

        public CommandResult LockGpuClock(int freq, int gpuIndex = -1)
        {
            if (!NvapiClockPower.IsAvailable)
                return new CommandResult(false, "nvapi64.dll 不可用");
            bool ok = NvapiClockPower.SetGpuClock(freq, gpuIndex);
            return new CommandResult(ok, ok ? $"GPU 频率已锁定 {freq} MHz" : "GPU 频率锁定失败");
        }

        public CommandResult LockGpuClock(int minFreq, int maxFreq, int gpuIndex = -1)
        {
            if (!NvapiClockPower.IsAvailable)
                return new CommandResult(false, "nvapi64.dll 不可用");
            bool ok = NvapiClockPower.SetGpuClock(maxFreq, gpuIndex);
            return new CommandResult(ok, ok ? $"GPU 频率范围已锁定 {minFreq}-{maxFreq} MHz" : "GPU 频率锁定失败");
        }

        public CommandResult ResetGpuClock(int gpuIndex = -1)
        {
            if (!NvapiClockPower.IsAvailable)
                return new CommandResult(false, "nvapi64.dll 不可用");
            bool ok = NvapiClockPower.ResetGpuClock(gpuIndex);
            return new CommandResult(ok, ok ? "GPU 频率已重置" : "GPU 频率重置失败");
        }

        public CommandResult LockMemoryClock(int freq, int gpuIndex = -1)
        {
            if (!NvapiClockPower.IsAvailable)
                return new CommandResult(false, "nvapi64.dll 不可用");
            bool ok = NvapiClockPower.SetMemoryClock(freq, gpuIndex);
            return new CommandResult(ok, ok ? $"显存频率已锁定 {freq} MHz" : "显存频率锁定失败");
        }

        public CommandResult ResetMemoryClock(int gpuIndex = -1)
        {
            if (!NvapiClockPower.IsAvailable)
                return new CommandResult(false, "nvapi64.dll 不可用");
            bool ok = NvapiClockPower.ResetMemoryClock(gpuIndex);
            return new CommandResult(ok, ok ? "显存频率已重置" : "显存频率重置失败");
        }

        public CommandResult SetPowerLimit(int watts, int gpuIndex = -1)
        {
            if (!NvapiClockPower.IsAvailable)
                return new CommandResult(false, "nvapi64.dll 不可用");
            bool ok = NvapiClockPower.SetPowerLimit(watts, gpuIndex);
            return new CommandResult(ok, ok ? $"功耗限制已设置为 {watts} W" : "功耗限制设置失败");
        }

        public CommandResult UnlockDB()
        {
            var driver = new NVPCF();
            var installResult = driver.Install();
            if (!installResult.Success)
                return new CommandResult(false, $"驱动阶段失败: {installResult.Message}");

            const string deviceId = @"ACPI\NVDA0820\NPCF";

            // 1. 尝试启用设备 (Win10 pnputil -> Win11 /deviceid -> Win11 PowerShell PnpDevice)
            var enableRes = ExecuteSystemCommand("pnputil", $@"/enable-device ""{deviceId}""");
            if (!enableRes.Success)
            {
                enableRes = ExecuteSystemCommand("pnputil", $@"/enable-device /deviceid ""{deviceId}""");
            }
            if (!enableRes.Success)
            {
                enableRes = ExecuteSystemCommand("powershell", $@"-NoProfile -Command ""Get-PnpDevice | Where-Object {{ $_.HardwareID -like '*NVDA0820*' }} | Enable-PnpDevice -Confirm:$false""");
            }
            if (!enableRes.Success)
                return new CommandResult(false, $"UnlockDB 失败 (启用设备阶段): {enableRes.Message}");

            Thread.Sleep(3000);

            // 2. 尝试禁用设备 (Win10 pnputil -> Win11 /deviceid -> Win11 PowerShell PnpDevice)
            var disableRes = ExecuteSystemCommand("pnputil", $@"/disable-device ""{deviceId}""");
            if (!disableRes.Success)
            {
                disableRes = ExecuteSystemCommand("pnputil", $@"/disable-device /deviceid ""{deviceId}""");
            }
            if (!disableRes.Success)
            {
                disableRes = ExecuteSystemCommand("powershell", $@"-NoProfile -Command ""Get-PnpDevice | Where-Object {{ $_.HardwareID -like '*NVDA0820*' }} | Disable-PnpDevice -Confirm:$false""");
            }
            if (!disableRes.Success)
                return new CommandResult(false, $"UnlockDB 失败 (禁用设备阶段): {disableRes.Message}");

            return new CommandResult(true, "UnlockDB 成功。");
        }

        private CommandResult ExecuteSystemCommand(string fileName, string arguments)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (Process? process = Process.Start(psi))
                {
                    if (process == null) return new CommandResult(false, $"无法启动 {fileName}");
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    if (process.ExitCode != 0)
                        return new CommandResult(false, $"命令返回码 {process.ExitCode}: {error} {output}");
                    return new CommandResult(true, output);
                }
            }
            catch (Exception ex)
            {
                return new CommandResult(false, $"执行 {fileName} 时发生异常: {ex.Message}");
            }
        }

        public void Dispose()
        {
            try { NVIDIA.Unload(); } catch { }
            GC.SuppressFinalize(this);
        }
    }
}
