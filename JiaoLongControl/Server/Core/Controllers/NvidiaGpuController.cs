using System.Diagnostics;
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

        public void Dispose()
        {
            try { NVIDIA.Unload(); } catch { }
            GC.SuppressFinalize(this);
        }

        private PhysicalGPU GetGPU(int gpuIndex)
        {
            var gpus = PhysicalGPU.GetPhysicalGPUs();
            if (gpus.Length == 0)
                throw new InvalidOperationException("没有找到 NVIDIA GPU");
            int idx = gpuIndex >= 0 && gpuIndex < gpus.Length ? gpuIndex : 0;
            return gpus[idx];
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
                    DriverDate = GetNvidiaDriverDate(gpuIndex),
                    MemoryTotal = $"{(gpu.MemoryInformation.DedicatedVideoMemoryInkB / 1024).ToString()} MiB",
                    BusWidth = $"x{gpu.BusInformation.CurrentPCIeLanes}",
                    GpuUtilization = gpu.UsageInformation.GPU.Percentage.ToString(),
                    MemoryUtilization = gpu.UsageInformation.FrameBuffer.Percentage.ToString(),
                    CoreClock = ((int)(gpu.CurrentClockFrequencies.GraphicsClock.Frequency / 1000)).ToString(),
                    MemoryClock = ((int)(gpu.CurrentClockFrequencies.MemoryClock.Frequency / 1000)).ToString(),
                    GpuTemperature = gpu.ThermalInformation.ThermalSensors.First().CurrentTemperature.ToString(),
                    FanSpeed = GetGpuFanSpeed(gpuIndex).Data.ToString() ?? "0"
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

        #region GPU 曲线调校 (NVAPI ClkVfPoints — 移植自 vuplea/simple-nvidia-undervolt, MIT)

        private CommandResult? _curveCapsCache;

        /// <summary>曲线调校用的物理 GPU 句柄 (NVAPI 枚举序)。</summary>
        private IntPtr GetCurveGpu(int gpuIndex)
        {
            var gpus = NvGpuCurveInterop.EnumeratePhysicalGpus();
            if (gpus.Length == 0)
                throw new NvCurveException("没有找到 NVIDIA GPU");
            int idx = ResolveGpuIndex(gpuIndex);
            return gpus[idx < gpus.Length ? idx : 0];
        }

        /// <summary>
        /// 探测本机 GPU 的曲线调校能力 (架构支持 / 接口是否被 OEM 驱动桩化 / 曲线可识别)。
        /// 结果按进程缓存, 更换驱动后需重启应用。
        /// </summary>
        public CommandResult GetGpuCurveCapabilities(int gpuIndex = -1)
        {
            if (_curveCapsCache != null)
                return _curveCapsCache;

            CommandResult result;
            try
            {
                var gpu = GetCurveGpu(gpuIndex);
                bool supported = true;
                string reason = "";
                var coreRange = (-1_000_000, 1_000_000);
                try
                {
                    var curve = NvGpuCurveInterop.GetVfCurve(gpu);
                    if (!GpuCurveTuning.CurveVoltsPlausible(curve))
                    {
                        supported = false;
                        reason = "V/F 曲线无法识别, 调校接口可能已被 OEM 驱动桩化";
                    }
                    else
                    {
                        coreRange = NvGpuCurveInterop.GetCoreOffsetRangeKhz(gpu);
                    }
                }
                catch (NvCurveException ex)
                {
                    // 首个 ClkVfPoints 调用失败: 带架构诊断 (Pascal 前 / 接口桩化)
                    supported = false;
                    reason = ex.Message;
                }

                result = new CommandResult(true, "获取成功", new
                {
                    Supported = supported,
                    Reason = reason,
                    GpuName = NvGpuCurveInterop.SafeFullName(gpu),
                    CoreOffsetMinMhz = coreRange.Item1 / 1000,
                    CoreOffsetMaxMhz = coreRange.Item2 / 1000,
                });
            }
            catch (Exception ex)
            {
                result = new CommandResult(false, $"曲线调校能力探测失败: {ex.Message}");
            }

            _curveCapsCache = result;
            return result;
        }

        /// <summary>曲线调校当前状态: 核心/显存偏移当前值与其驱动合法范围、出厂显存频率、
        /// 当前工作点 (负载下睿频落点的形状推断, 读不干净时为 null)。</summary>
        public CommandResult GetGpuCurveStatus(int gpuIndex = -1)
        {
            try
            {
                var gpu = GetCurveGpu(gpuIndex);
                var curve = NvGpuCurveInterop.GetVfCurve(gpu);
                var (coreMinKhz, coreMaxKhz) = NvGpuCurveInterop.GetCoreOffsetRangeKhz(gpu);
                var (memMinKhz, memMaxKhz) = GpuCurveTuning.GetMemoryOffsetRangeKhz(gpu);
                var op = GpuCurveTuning.EffectiveOperatingPoint(curve);

                return new CommandResult(true, "获取成功", new
                {
                    CoreOffsetMhz = GpuCurveTuning.GetCoreOffsetKhz(gpu) / 1000,
                    CoreOffsetMinMhz = coreMinKhz / 1000,
                    CoreOffsetMaxMhz = coreMaxKhz / 1000,
                    MemoryOffsetMhz = GpuCurveTuning.GetMemoryOffsetKhz(gpu) / 1000,
                    MemoryOffsetMinMhz = memMinKhz / 1000,
                    MemoryOffsetMaxMhz = memMaxKhz / 1000,
                    BaseMemoryClockMhz = GpuCurveTuning.BaseMemoryClockMhz(gpu),
                    OperatingPoint = op is { } point ? new { point.Mv, point.Mhz } : null,
                });
            }
            catch (Exception ex)
            {
                return new CommandResult(false, $"获取曲线状态失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 应用核心/显存频率偏移 (Afterburner 主滑条模型): 核心偏移 = 整条 V/F 曲线统一平移
        /// (正值超频/负值降压), 显存偏移 = P0 显存频率增量。写入含清零与读回验证,
        /// 未落地自动回滚, 约需 1~2 秒。
        /// </summary>
        public CommandResult SetGpuOffsets(int coreOffsetMhz, int memoryOffsetMhz, int gpuIndex = -1)
        {
            try
            {
                var gpu = GetCurveGpu(gpuIndex);
                var (coreMin, coreMax) = NvGpuCurveInterop.GetCoreOffsetRangeKhz(gpu);
                var (memMin, memMax) = GpuCurveTuning.GetMemoryOffsetRangeKhz(gpu);
                int coreKhz = Math.Clamp(coreOffsetMhz * 1000, coreMin, coreMax);
                int memKhz = Math.Clamp(memoryOffsetMhz * 1000, memMin, memMax);

                var log = GpuCurveTuning.ApplyOffsets(gpu, coreKhz, memKhz);
                return new CommandResult(true, string.Join("; ", log));
            }
            catch (NvCurveException ex)
            {
                return new CommandResult(false, ex.Message);
            }
            catch (Exception ex)
            {
                return new CommandResult(false, $"应用频率偏移失败: {ex.Message}");
            }
        }

        /// <summary>重置全部曲线调校到 stock (曲线 delta、显存/核心偏移、电压提升)。</summary>
        public CommandResult ResetGpuCurve(int gpuIndex = -1)
        {
            try
            {
                var gpu = GetCurveGpu(gpuIndex);
                var log = GpuCurveTuning.Clear(gpu);
                return new CommandResult(true, string.Join("; ", log));
            }
            catch (NvCurveException ex)
            {
                return new CommandResult(false, ex.Message);
            }
            catch (Exception ex)
            {
                return new CommandResult(false, $"重置曲线调校失败: {ex.Message}");
            }
        }

        #endregion
    }
}
