namespace JiaoLongControl.Server.Core.Models
{
    public class GpuStats
    {
        public string? GpuName { get; set; }
        public string? DriverVersion { get; set; }
        public string? MemoryTotal { get; set; }
        public string? BusWidth { get; set; }
        public string? GpuUtilization { get; set; }
        public string? MemoryUtilization { get; set; }
        public string? CoreClock { get; set; }
        public string? MemoryClock { get; set; }
        public string? GpuTemperature { get; set; }
        public string? FanSpeed { get; set; }
        public string DriverDate { get; set; } = "N/A";
    }
}
