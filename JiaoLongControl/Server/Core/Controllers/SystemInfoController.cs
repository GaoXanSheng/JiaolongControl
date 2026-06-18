using System;
using System.Management;
using JiaoLongControl.Server.Core.Utils;

namespace JiaoLongControl.Server.Core.Controllers
{
    public class SystemInfoController
    {
        public class SystemOverview
        {
            public string CpuName { get; set; } = "Unknown CPU";
            public string GpuName { get; set; } = "Unknown GPU";
            public string OsVersion { get; set; } = "Unknown OS";
            public string MemoryInfo { get; set; } = "Unknown Memory";
        }

        public CommandResult GetSystemOverview()
        {
            var overview = new SystemOverview();

            try
            {
                // 获取 CPU 信息
                using (var searcher = new ManagementObjectSearcher("select Name from Win32_Processor"))
                {
                    foreach (var item in searcher.Get())
                    {
                        overview.CpuName = item["Name"]?.ToString()?.Trim() ?? "Unknown CPU";
                        break;
                    }
                }

                // 获取 独立显卡 信息 (排除集显)
                using (var searcher = new ManagementObjectSearcher("select Name from Win32_VideoController"))
                {
                    foreach (var item in searcher.Get())
                    {
                        var name = item["Name"]?.ToString() ?? "";
                        if (name.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) || 
                            name.Contains("AMD Radeon RX", StringComparison.OrdinalIgnoreCase))
                        {
                            overview.GpuName = name;
                            break;
                        }
                        // 如果没有独显，记录第一个找到的显卡
                        if (string.IsNullOrEmpty(overview.GpuName) || overview.GpuName == "Unknown GPU")
                        {
                            overview.GpuName = name;
                        }
                    }
                }

                // 获取 操作系统 信息
                using (var searcher = new ManagementObjectSearcher("select Caption, Version from Win32_OperatingSystem"))
                {
                    foreach (var item in searcher.Get())
                    {
                        var caption = item["Caption"]?.ToString()?.Replace("Microsoft", "").Trim();
                        overview.OsVersion = $"{caption}";
                        break;
                    }
                }

                // 获取 内存 信息
                long totalCapacity = 0;
                string speed = "";
                using (var searcher = new ManagementObjectSearcher("select Capacity, Speed from Win32_PhysicalMemory"))
                {
                    foreach (var item in searcher.Get())
                    {
                        if (long.TryParse(item["Capacity"]?.ToString(), out long capacity))
                        {
                            totalCapacity += capacity;
                        }
                        if (string.IsNullOrEmpty(speed) && item["Speed"] != null)
                        {
                            speed = item["Speed"].ToString();
                        }
                    }
                }
                
                if (totalCapacity > 0)
                {
                    var gb = Math.Round((double)totalCapacity / (1024 * 1024 * 1024));
                    overview.MemoryInfo = $"{gb}GB {(string.IsNullOrEmpty(speed) ? "" : speed + "MHz")}".Trim();
                }

                return new CommandResult(true, "Success", overview);
            }
            catch (Exception ex)
            {
                return new CommandResult(false, $"Failed to get system info: {ex.Message}");
            }
        }
    }
}
