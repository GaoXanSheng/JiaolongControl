using LibreHardwareMonitor.Hardware;

namespace JiaoLongControl.Server.Core.Services
{
    public sealed class CpuSensors
    {
        public int Usage { get; set; }
        public int FrequencyMhz { get; set; }
        public double Voltage { get; set; }
        public int Temperature { get; set; }
        public double PackagePower { get; set; }
        public double? Tdc { get; set; }
        public double? Edc { get; set; }
    }

    public static class HardwareMonitorService
    {
        private const int CacheMilliseconds = 300;

        private static readonly object Gate = new();
        private static Computer? _computer;
        private static CpuSensors? _cached;
        private static long _cachedAt;

        public static CpuSensors ReadCpu()
        {
            lock (Gate)
            {
                long now = Environment.TickCount64;
                if (_cached != null && now - _cachedAt < CacheMilliseconds)
                    return _cached;

                var sensors = new CpuSensors();
                try
                {
                    _computer ??= Open();
                    foreach (var hardware in _computer.Hardware)
                    {
                        if (hardware.HardwareType != HardwareType.Cpu)
                            continue;

                        hardware.Update();
                        Collect(hardware, sensors);
                        break;
                    }
                }
                catch
                {
                }

                _cached = sensors;
                _cachedAt = now;
                return sensors;
            }
        }

        private static Computer Open()
        {
            var computer = new Computer { IsCpuEnabled = true };
            computer.Open();
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

                    case SensorType.Current:
                        if (sensor.Name.Contains("TDC"))
                            sensors.Tdc ??= Math.Round(value, 1);
                        else if (sensor.Name.Contains("EDC"))
                            sensors.Edc ??= Math.Round(value, 1);
                        break;
                }
            }

            sensors.FrequencyMhz = (int)Math.Round(
                averageClock > 0 ? averageClock : coreClockCount > 0 ? coreClockSum / coreClockCount : 0);
            sensors.Voltage = vcore ?? (coreVid.Count > 0 ? coreVid.Average() : 0);
            sensors.Temperature = (int)Math.Round(tctlDie ?? tdie ?? maxTemperature);
        }
    }
}
