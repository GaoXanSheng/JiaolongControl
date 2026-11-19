using System.Diagnostics;
using JiaoLongControl.Server.Core.Utils;
using LibreHardwareMonitor.Hardware;
using Microsoft.Win32;

namespace JiaoLongControl.Server.Core.Services
{
    public sealed class CpuHardwareInfo
    {
        public string Name { get; set; } = "";
        public int Cores { get; set; }
        public int Threads { get; set; }
        public int BaseFrequencyMhz { get; set; }
    }

    public sealed class CpuSensors
    {
        public int Usage { get; set; }
        public int FrequencyMhz { get; set; }
        public double Voltage { get; set; }
        public int Temperature { get; set; }
        public double PackagePower { get; set; }
    }

    public static class HardwareMonitorService
    {
        private const int CacheMilliseconds = 300;

        private static readonly Log Logger = Log.For(nameof(HardwareMonitorService));
        private static readonly object Gate = new();
        private static readonly int BaseFrequencyMhz = ReadBaseFrequency();
        private static PerformanceCounter? _perfCounter;
        private static bool _perfCounterUnavailable;
        private static Computer? _computer;
        private static CpuSensors? _cached;
        private static long _cachedAt;
        private static CpuHardwareInfo? _info;

        public static CpuHardwareInfo ReadCpuInfo()
        {
            if (!Monitor.TryEnter(Gate))
                return _info ?? new CpuHardwareInfo
                {
                    Threads = Environment.ProcessorCount,
                    BaseFrequencyMhz = BaseFrequencyMhz,
                };

            try
            {
                if (_info != null)
                    return _info;

                var info = ReadCpuInfoCore();
                if (!string.IsNullOrEmpty(info.Name))
                    _info = info;
                return info;
            }
            finally
            {
                Monitor.Exit(Gate);
            }
        }

        private static CpuHardwareInfo ReadCpuInfoCore()
        {
            var info = new CpuHardwareInfo
            {
                Threads = Environment.ProcessorCount,
                BaseFrequencyMhz = ReadBaseFrequency(),
            };

            try
            {
                _computer ??= Open();
                foreach (var hardware in _computer.Hardware)
                {
                    if (hardware.HardwareType != HardwareType.Cpu)
                        continue;

                    info.Name = hardware.Name;
                    info.Cores = hardware.Sensors.Count(s =>
                        s.SensorType == SensorType.Clock &&
                        !s.Name.Contains("Effective") &&
                        (s.Name.StartsWith("Core #") || s.Name.StartsWith("CPU Core #")));
                    break;
                }
            }
            catch
            {
            }

            return info;
        }

        private static int ReadBaseFrequency()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(
                    @"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
                return Convert.ToInt32(key?.GetValue("~MHz") ?? 0);
            }
            catch
            {
                return 0;
            }
        }

        public static CpuSensors ReadCpu()
        {
            if (!Monitor.TryEnter(Gate))
                return _cached ?? new CpuSensors();

            try
            {
                long now = Environment.TickCount64;
                if (_cached != null && now - _cachedAt < CacheMilliseconds)
                    return _cached;

                var totalSw = Stopwatch.StartNew();
                long openMs = 0, updateMs = 0, collectMs = 0;
                var sensors = new CpuSensors();
                try
                {
                    var openSw = Stopwatch.StartNew();
                    _computer ??= Open();
                    openMs = openSw.ElapsedMilliseconds;

                    foreach (var hardware in _computer.Hardware)
                    {
                        if (hardware.HardwareType != HardwareType.Cpu)
                            continue;

                        var updateSw = Stopwatch.StartNew();
                        hardware.Update();
                        updateMs = updateSw.ElapsedMilliseconds;

                        var collectSw = Stopwatch.StartNew();
                        Collect(hardware, sensors);
                        collectMs = collectSw.ElapsedMilliseconds;
                        break;
                    }
                }
                catch
                {
                }

                if (totalSw.ElapsedMilliseconds > 500)
                    Logger.Info($"LHM ReadCpu: open={openMs} update={updateMs} collect={collectMs} total={totalSw.ElapsedMilliseconds}ms");

                _cached = sensors;
                _cachedAt = now;
                return sensors;
            }
            finally
            {
                Monitor.Exit(Gate);
            }
        }

        private static Computer Open()
        {
            var computer = new Computer { IsCpuEnabled = true };
            var sw = Stopwatch.StartNew();
            computer.Open();
            Logger.Info($"LHM Open: {sw.ElapsedMilliseconds}ms");
            return computer;
        }

        private static void Collect(IHardware hardware, CpuSensors sensors)
        {
            double averageClock = 0;
            double coreClockSum = 0;
            int coreClockCount = 0;
            var coreVid = new List<double>();
            double? vcore = null;
            double? tctlDie = null;
            double? tdie = null;
            double maxTemperature = 0;

            foreach (var sensor in hardware.Sensors)
            {
                if (sensor.Value is not { } value)
                    continue;

                switch (sensor.SensorType)
                {
                    case SensorType.Load:
                        if (sensor.Name == "CPU Total")
                            sensors.Usage = (int)Math.Round(value);
                        break;

                    case SensorType.Clock:
                        if (sensor.Name == "Cores (Average)")
                            averageClock = value;
                        else if (!sensor.Name.Contains("Effective") && sensor.Name.StartsWith("Core #"))
                        {
                            coreClockSum += value;
                            coreClockCount++;
                        }
                        break;

                    case SensorType.Voltage:
                        if (sensor.Name.Contains("Vcore"))
                            vcore ??= value;
                        else if (sensor.Name.Contains("VID"))
                            coreVid.Add(value);
                        break;

                    case SensorType.Temperature:
                        if (value <= 0)
                            break;
                        if (sensor.Name.Contains("Tctl/Tdie"))
                            tctlDie ??= value;
                        else if (sensor.Name.Contains("Tdie"))
                            tdie ??= value;
                        if (value > maxTemperature)
                            maxTemperature = value;
                        break;

                    case SensorType.Power:
                        if (sensor.Name == "Package")
                            sensors.PackagePower = value;
                        break;
                }
            }

            double lhmClock = averageClock > 0
                ? averageClock
                : coreClockCount > 0 ? coreClockSum / coreClockCount : 0;
            int perfClock = ReadFrequencyMhz();
            sensors.FrequencyMhz = perfClock > 0 ? perfClock : (int)Math.Round(lhmClock);
            sensors.Voltage = vcore ?? (coreVid.Count > 0 ? coreVid.Average() : 0);
            sensors.Temperature = (int)Math.Round(tctlDie ?? tdie ?? maxTemperature);
        }

        private static int ReadFrequencyMhz()
        {
            if (_perfCounter == null && !_perfCounterUnavailable)
            {
                try
                {
                    var counter = new PerformanceCounter(
                        "Processor Information", "% Processor Performance", "_Total");
                    counter.NextValue();
                    _perfCounter = counter;
                }
                catch
                {
                    _perfCounterUnavailable = true;
                }
            }

            if (_perfCounter == null || BaseFrequencyMhz <= 0)
                return 0;

            try
            {
                return (int)Math.Round(_perfCounter.NextValue() / 100.0 * BaseFrequencyMhz);
            }
            catch
            {
                return 0;
            }
        }
    }
}
