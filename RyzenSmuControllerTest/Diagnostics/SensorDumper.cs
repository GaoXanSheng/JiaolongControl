using RyzenSmuControllerTest.Infrastructure;

namespace RyzenSmuControllerTest.Diagnostics;

/// <summary>枚举 LibreHardwareMonitor 的 CPU 传感器（--dump-sensors）。</summary>
internal static class SensorDumper
{
    public static void DumpAll()
    {
        ConsoleLog.Log(">>> 枚举 LibreHardwareMonitor CPU 传感器 ...");
        try
        {
            using var searcher = new System.Management.ManagementObjectSearcher("SELECT Name FROM Win32_Processor");
            foreach (var obj in searcher.Get())
            {
                ConsoleLog.Log($"    CPU: {obj["Name"]}");
                break;
            }
        }
        catch { }

        var computer = new LibreHardwareMonitor.Hardware.Computer
        {
            IsCpuEnabled = true,
        };
        computer.Open();
        try
        {
            foreach (var hardware in computer.Hardware)
            {
                if (hardware.HardwareType != LibreHardwareMonitor.Hardware.HardwareType.Cpu)
                    continue;

                ConsoleLog.Log($"  [硬件] {hardware.Name} ({hardware.HardwareType})");
                DumpHardwareSensors(hardware);

                foreach (var sub in hardware.SubHardware)
                {
                    ConsoleLog.Log($"    [子硬件] {sub.Name} ({sub.HardwareType})");
                    DumpHardwareSensors(sub, 6);
                }
            }
        }
        finally
        {
            computer.Close();
        }
        ConsoleLog.Log("");
    }

    private static void DumpHardwareSensors(LibreHardwareMonitor.Hardware.IHardware hardware, int indent = 4)
    {
        hardware.Update();
        foreach (var sensor in hardware.Sensors)
        {
            ConsoleLog.Log($"{new string(' ', indent)}{sensor.SensorType,-12} {sensor.Name,-32} = {sensor.Value?.ToString() ?? "N/A"}");
        }
    }
}
