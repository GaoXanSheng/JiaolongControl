using RyzenSmuControllerTest.Infrastructure;

namespace RyzenSmuControllerTest.Diagnostics;

/// <summary>枚举 LibreHardwareMonitor 的 CPU 传感器（--dump-sensors）。</summary>
internal static class SensorDumper
{
    public static bool DumpAll()
    {
        ConsoleLog.Log(">>> 枚举 LibreHardwareMonitor CPU 传感器 ...");

        var computer = new LibreHardwareMonitor.Hardware.Computer
        {
            IsCpuEnabled = true,
        };

        try
        {
            computer.Open();
        }
        catch (Exception ex)
        {
            ConsoleLog.Error($"    打开 LibreHardwareMonitor 失败: {ex.Message}");
            return false;
        }

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
        catch (Exception ex)
        {
            ConsoleLog.Error($"    枚举传感器失败: {ex.Message}");
            return false;
        }
        finally
        {
            computer.Close();
            ConsoleLog.Log("");
        }

        return true;
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
