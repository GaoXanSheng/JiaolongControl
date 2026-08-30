using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
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
                    DriverDate = GetNvidiaDriverDate(gpuIndex) ?? "Unknown",
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

        public CommandResult GetGpuDriverDate(int gpuIndex = -1)
        {
            try
            {
                string date = GetNvidiaDriverDate(gpuIndex);
                if (string.IsNullOrEmpty(date))
                    return new CommandResult(false, "未获取到 NVIDIA 驱动日期");
                return new CommandResult(true, "获取成功", date);
            }
            catch (Exception ex) { return new CommandResult(false, ex.Message); }
        }

        /// <summary>
        /// 通过 WMI 查询 Win32_VideoController 获取 NVIDIA 显卡驱动安装日期（格式 yyyy-MM-dd）。
        /// </summary>
        private string GetNvidiaDriverDate(int gpuIndex)
        {
            string fullName = GetGPU(gpuIndex).FullName;
            string fallback = "";
            using var searcher = new ManagementObjectSearcher("SELECT Name, DriverDate FROM Win32_VideoController");
            foreach (var obj in searcher.Get())
            {
                string name = obj["Name"]?.ToString() ?? "";
                if (!name.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase))
                    continue;
                string dateStr = obj["DriverDate"]?.ToString();
                if (string.IsNullOrEmpty(dateStr))
                    continue;
                var date = ManagementDateTimeConverter.ToDateTime(dateStr);
                if (name.Equals(fullName, StringComparison.OrdinalIgnoreCase))
                    return date.ToString("yyyy-MM-dd");
                if (string.IsNullOrEmpty(fallback))
                    fallback = date.ToString("yyyy-MM-dd");
            }
            return fallback;
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
                int clock = (int)(gpu.CurrentClockFrequencies.GraphicsClock.Frequency / 1000);
                if (clock == 0)
                {
                    clock = (int)(gpu.BoostClockFrequencies.GraphicsClock.Frequency / 1000);
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
                // BoostClockFrequencies 是标称睿频规格值(恒非零)，只能作读不到当前频率时的兜底
                int clock = (int)(gpu.CurrentClockFrequencies.MemoryClock.Frequency / 1000);
                if (clock == 0)
                {
                    clock = (int)(gpu.BoostClockFrequencies.MemoryClock.Frequency / 1000);
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
            return LockGpuClock(freq, freq, gpuIndex);
        }

        public CommandResult LockGpuClock(int minFreq, int maxFreq, int gpuIndex = -1)
        {
            var result = RunNvidiaSmi("-i", ResolveGpuIndex(gpuIndex).ToString(), "-lgc", $"{minFreq},{maxFreq}");
            if (!result.Success)
                return result;
            string message = minFreq == maxFreq
                ? $"GPU 频率已锁定 {minFreq} MHz"
                : $"GPU 频率范围已锁定 {minFreq}-{maxFreq} MHz";
            return new CommandResult(true, message);
        }

        public CommandResult ResetGpuClock(int gpuIndex = -1)
        {
            var result = RunNvidiaSmi("-i", ResolveGpuIndex(gpuIndex).ToString(), "-rgc");
            return result.Success ? new CommandResult(true, "GPU 频率已重置") : result;
        }

        public CommandResult LockMemoryClock(int freq, int gpuIndex = -1)
        {
            var result = RunNvidiaSmi("-i", ResolveGpuIndex(gpuIndex).ToString(), "-lmc", $"{freq},{freq}");
            return result.Success ? new CommandResult(true, $"显存频率已锁定 {freq} MHz") : result;
        }

        public CommandResult ResetMemoryClock(int gpuIndex = -1)
        {
            var result = RunNvidiaSmi("-i", ResolveGpuIndex(gpuIndex).ToString(), "-rmc");
            return result.Success ? new CommandResult(true, "显存频率已重置") : result;
        }

        public CommandResult SetPowerLimit(int watts, int gpuIndex = -1)
        {
            var result = RunNvidiaSmi("-i", ResolveGpuIndex(gpuIndex).ToString(), "-pl", watts.ToString());
            return result.Success ? new CommandResult(true, $"功耗限制已设置为 {watts} W") : result;
        }

        #region 超频 (NVAPI 私有接口, Afterburner 同款路径)

        public CommandResult GetClockOffsetRange(int gpuIndex = -1)
        {
            try
            {
                var range = NvApiOverclock.GetClockOffsetRange(NvApiOverclock.GetGpuHandle(ResolveGpuIndex(gpuIndex)));
                return new CommandResult(true, "获取成功", new
                {
                    Core = new { Min = range.CoreMinMhz, Max = range.CoreMaxMhz },
                    Memory = new { Min = range.MemoryMinMhz, Max = range.MemoryMaxMhz }
                });
            }
            catch (Exception ex)
            {
                return new CommandResult(false, $"获取频率偏移范围失败: {ex.Message}");
            }
        }

        public CommandResult GetClockOffsets(int gpuIndex = -1)
        {
            try
            {
                var offsets = NvApiOverclock.GetClockOffsets(NvApiOverclock.GetGpuHandle(ResolveGpuIndex(gpuIndex)));
                return new CommandResult(true, "获取成功", new { CoreMhz = offsets.CoreMhz, MemoryMhz = offsets.MemoryMhz });
            }
            catch (Exception ex)
            {
                return new CommandResult(false, $"获取频率偏移失败: {ex.Message}");
            }
        }

        public CommandResult SetCoreClockOffset(int mhz, int gpuIndex = -1)
        {
            return ApplyClockOffsetsInternal(mhz, null, ResolveGpuIndex(gpuIndex));
        }

        public CommandResult SetMemoryClockOffset(int mhz, int gpuIndex = -1)
        {
            return ApplyClockOffsetsInternal(null, mhz, ResolveGpuIndex(gpuIndex));
        }

        public CommandResult ApplyClockOffsets(int coreMhz, int memoryMhz, int gpuIndex = -1)
        {
            return ApplyClockOffsetsInternal(coreMhz, memoryMhz, ResolveGpuIndex(gpuIndex));
        }

        public CommandResult ResetClockOffsets(int gpuIndex = -1)
        {
            return ApplyClockOffsetsInternal(0, 0, ResolveGpuIndex(gpuIndex));
        }

        private CommandResult ApplyClockOffsetsInternal(int? coreMhz, int? memoryMhz, int gpuIndex)
        {
            try
            {
                var gpu = NvApiOverclock.GetGpuHandle(gpuIndex);
                var current = NvApiOverclock.GetClockOffsets(gpu);
                int core = coreMhz ?? current.CoreMhz;
                int memory = memoryMhz ?? current.MemoryMhz;

                // 范围读取成功时做夹取, 读取失败(全 0)则交给驱动校验
                var range = NvApiOverclock.GetClockOffsetRange(gpu);
                if (range.CoreMaxMhz > range.CoreMinMhz)
                    core = Math.Clamp(core, range.CoreMinMhz, range.CoreMaxMhz);
                if (range.MemoryMaxMhz > range.MemoryMinMhz)
                    memory = Math.Clamp(memory, range.MemoryMinMhz, range.MemoryMaxMhz);

                NvApiOverclock.SetClockOffsets(gpu, core, memory);

                // 写入后读回验证: 本机驱动可能静默忽略偏移 (OEM 锁定), 不做假成功
                var verify = NvApiOverclock.GetClockOffsets(gpu);
                if (core != 0 && verify.CoreMhz != core)
                    return new CommandResult(false,
                        $"驱动未应用核心偏移 (写入 {core} MHz, 读回 {verify.CoreMhz} MHz)——本机驱动可能已锁定超频");
                return new CommandResult(true, $"核心偏移 {core:+0;-0} MHz 已应用");
            }
            catch (Exception ex)
            {
                return new CommandResult(false, $"应用频率偏移失败: {ex.Message}");
            }
        }

        public CommandResult GetVoltageBoostPercent(int gpuIndex = -1)
        {
            try
            {
                int percent = NvApiOverclock.GetVoltageBoostPercent(NvApiOverclock.GetGpuHandle(ResolveGpuIndex(gpuIndex)));
                return new CommandResult(true, "获取成功", percent);
            }
            catch (Exception ex)
            {
                return new CommandResult(false, $"获取电压提升失败: {ex.Message}");
            }
        }

        public CommandResult SetVoltageBoostPercent(int percent, int gpuIndex = -1)
        {
            try
            {
                var gpu = NvApiOverclock.GetGpuHandle(ResolveGpuIndex(gpuIndex));
                NvApiOverclock.SetVoltageBoostPercent(gpu, percent);
                return new CommandResult(true, $"核心电压提升已设置为 {Math.Clamp(percent, 0, 100)}%");
            }
            catch (Exception ex)
            {
                return new CommandResult(false, $"设置电压提升失败: {ex.Message}");
            }
        }

        public CommandResult GetGpuPowerPolicy(int gpuIndex = -1)
        {
            try
            {
                var policy = NvApiOverclock.GetPowerPolicy(NvApiOverclock.GetGpuHandle(ResolveGpuIndex(gpuIndex)));
                return new CommandResult(true, "获取成功", new
                {
                    policy.CurrentWatts, policy.MinWatts, policy.DefaultWatts, policy.MaxWatts
                });
            }
            catch (Exception ex)
            {
                return new CommandResult(false, $"获取功耗策略失败: {ex.Message}");
            }
        }

        public CommandResult SetGpuPowerPolicy(int watts, int gpuIndex = -1)
        {
            try
            {
                var gpu = NvApiOverclock.GetGpuHandle(ResolveGpuIndex(gpuIndex));
                NvApiOverclock.SetPowerPolicy(gpu, watts);
                var policy = NvApiOverclock.GetPowerPolicy(gpu);
                return new CommandResult(true, $"功耗墙已设置为 {policy.CurrentWatts} W");
            }
            catch (Exception ex)
            {
                return new CommandResult(false, $"设置功耗墙失败: {ex.Message}");
            }
        }

        public CommandResult GetGpuThermalPolicy(int gpuIndex = -1)
        {
            try
            {
                var policy = NvApiOverclock.GetThermalPolicy(NvApiOverclock.GetGpuHandle(ResolveGpuIndex(gpuIndex)));
                return new CommandResult(true, "获取成功", new
                {
                    policy.CurrentTemp, policy.MinTemp, policy.DefaultTemp, policy.MaxTemp
                });
            }
            catch (Exception ex)
            {
                return new CommandResult(false, $"获取温度策略失败: {ex.Message}");
            }
        }

        public CommandResult SetGpuThermalPolicy(int tempCelsius, int gpuIndex = -1)
        {
            try
            {
                var gpu = NvApiOverclock.GetGpuHandle(ResolveGpuIndex(gpuIndex));
                NvApiOverclock.SetThermalPolicy(gpu, tempCelsius);
                var policy = NvApiOverclock.GetThermalPolicy(gpu);
                return new CommandResult(true, $"温度墙已设置为 {policy.CurrentTemp} ℃");
            }
            catch (Exception ex)
            {
                return new CommandResult(false, $"设置温度墙失败: {ex.Message}");
            }
        }

        public CommandResult GetGpuFanControl(int gpuIndex = -1)
        {
            try
            {
                var fan = NvApiOverclock.GetFanControl(NvApiOverclock.GetGpuHandle(ResolveGpuIndex(gpuIndex)));
                return new CommandResult(true, "获取成功", new
                {
                    fan.CoolerCount, fan.CoolerId, fan.ControlMode, fan.Level, fan.Rpm, fan.MaxRpm
                });
            }
            catch (Exception ex)
            {
                return new CommandResult(false, $"获取 GPU 风扇控制失败: {ex.Message}");
            }
        }

        public CommandResult SetGpuFanLevel(int percent, int gpuIndex = -1)
        {
            try
            {
                NvApiOverclock.SetFanControl(NvApiOverclock.GetGpuHandle(ResolveGpuIndex(gpuIndex)), percent);
                return new CommandResult(true, $"GPU 风扇已设置为手动 {Math.Clamp(percent, 0, 100)}%");
            }
            catch (Exception ex)
            {
                return new CommandResult(false, $"设置 GPU 风扇转速失败: {ex.Message}");
            }
        }

        public CommandResult SetGpuFanAuto(int gpuIndex = -1)
        {
            try
            {
                NvApiOverclock.SetFanControl(NvApiOverclock.GetGpuHandle(ResolveGpuIndex(gpuIndex)), -1);
                return new CommandResult(true, "GPU 风扇已恢复自动调速");
            }
            catch (Exception ex)
            {
                return new CommandResult(false, $"恢复 GPU 风扇自动调速失败: {ex.Message}");
            }
        }

        private CommandResult? _capabilitiesCache;

        /// <summary>
        /// 探测本机驱动实际支持哪些超频能力 (部分 OEM 驱动会静默忽略偏移写入, 只能实测定论)。
        /// 结果按进程缓存, 更换驱动后需重启应用。
        /// </summary>
        public CommandResult GetOverclockCapabilities(int gpuIndex = -1)
        {
            if (_capabilitiesCache != null)
                return _capabilitiesCache;

            CommandResult result;
            try
            {
                result = new CommandResult(true, "获取成功", new
                {
                    CoreOffset = ProbeCoreOffsetSupported(),
                    // 现驱动 V/F 偏移表不提供显存通道, 锁频走 nvidia-smi -lmc
                    MemoryOffset = false,
                    VoltageBoost = ProbeVoltageBoostSupported(),
                    ThermalPolicy = ProbeThermalPolicySupported(),
                    PowerPolicy = ProbePowerPolicySupported(),
                });
            }
            catch (Exception ex)
            {
                result = new CommandResult(false, $"能力探测失败: {ex.Message}");
            }
            _capabilitiesCache = result;
            return result;
        }

        private bool ProbeCoreOffsetSupported()
        {
            try
            {
                var gpu = NvApiOverclock.GetGpuHandle(ResolveGpuIndex(-1));
                var points = NvApiOverclock.GetActiveCurvePoints(gpu);
                if (points.Length == 0)
                    return false;

                // 用 +100MHz 的单点探测写入区分"驱动忽略"与温度步进噪声 (±30MHz)
                int point = points[points.Length / 2];
                int before = NvApiOverclock.GetCurvePointFrequencyMhz(gpu, point);
                NvApiOverclock.SetClockPointOffset(gpu, point, 100000);
                System.Threading.Thread.Sleep(80);
                int after = NvApiOverclock.GetCurvePointFrequencyMhz(gpu, point);
                NvApiOverclock.SetClockPointOffset(gpu, point, 0);
                return after - before > 60;
            }
            catch
            {
                return false;
            }
        }

        private bool ProbeVoltageBoostSupported()
        {
            try
            {
                var gpu = NvApiOverclock.GetGpuHandle(ResolveGpuIndex(-1));
                int current = NvApiOverclock.GetVoltageBoostPercent(gpu);
                NvApiOverclock.SetVoltageBoostPercent(gpu, current); // 写回原值, 仅探测接口可用性
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool ProbeThermalPolicySupported()
        {
            try
            {
                var gpu = NvApiOverclock.GetGpuHandle(ResolveGpuIndex(-1));
                var policy = NvApiOverclock.GetThermalPolicy(gpu);
                NvApiOverclock.SetThermalPolicy(gpu, policy.CurrentTemp); // 写回当前值
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool ProbePowerPolicySupported()
        {
            try
            {
                NvApiOverclock.GetPowerPolicy(NvApiOverclock.GetGpuHandle(ResolveGpuIndex(-1)));
                return true;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        private int ResolveGpuIndex(int gpuIndex)
        {
            return gpuIndex >= 0 ? gpuIndex : 0;
        }

        private CommandResult RunNvidiaSmi(params string[] arguments)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "nvidia-smi",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                // ArgumentList 逐项传递并自动转义, 不构造命令行字符串, 避免参数注入
                foreach (var arg in arguments)
                    psi.ArgumentList.Add(arg);

                using var process = Process.Start(psi);
                string output = process!.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit(5000);

                if (process.ExitCode != 0)
                {
                    string message = string.IsNullOrWhiteSpace(error) ? output : error;
                    return new CommandResult(false, $"[NvidiaGpuController] nvidia-smi {string.Join(" ", arguments)} 失败: {message.Trim()}");
                }
            }
            catch (Exception ex)
            {
                return new CommandResult(false, $"[NvidiaGpuController] 执行 nvidia-smi 异常: {ex.Message}");
            }

            return new CommandResult(true, "执行成功");
        }

        public void Dispose()
        {
            try { NVIDIA.Unload(); } catch { }
            GC.SuppressFinalize(this);
        }
    }
}
