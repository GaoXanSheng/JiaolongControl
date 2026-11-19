using System.Diagnostics;
using System.Management;
using JiaoLongControl.Server.Core.Utils;

namespace JiaoLongControl.Server.Core.Controllers
{
    public class SystemInfoController
    {
        private static readonly Log Logger = Log.For<SystemInfoController>();

        // 系统概览整会话不变: 只查一次, 失败时清空缓存允许下次轮询重试
        private static Task<CommandResult>? _overviewTask;

        /// <summary>使用系统默认浏览器打开外部链接（仅允许 http/https）</summary>
        public CommandResult OpenUrl(string url)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(url))
                {
                    return new CommandResult(false, "无效的链接地址");
                }

                url = url.Trim();
                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                    (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                {
                    return new CommandResult(false, "无效的链接地址");
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = uri.AbsoluteUri,
                    UseShellExecute = true
                });
                return new CommandResult(true, "已打开浏览器");
            }
            catch (Exception ex)
            {
                return new CommandResult(false, $"打开浏览器失败: {ex.Message}");
            }
        }

        public Task<CommandResult> GetSystemOverview()
        {
            var task = _overviewTask;
            if (task != null)
                return task;

            task = Task.Run(QuerySystemOverview);
            _overviewTask = task;
            _ = task.ContinueWith(t =>
            {
                if (!t.Result.Success)
                    _overviewTask = null;
            });
            return task;
        }

        private static CommandResult QuerySystemOverview()
        {
            var overview = new SystemOverview();
            var sw = Stopwatch.StartNew();

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

                Logger.Info($"overview CPU: {sw.ElapsedMilliseconds}ms");
                sw.Restart();

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

                Logger.Info($"overview GPU: {sw.ElapsedMilliseconds}ms");
                sw.Restart();

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

                Logger.Info($"overview OS: {sw.ElapsedMilliseconds}ms");
                sw.Restart();

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
                
                Logger.Info($"overview RAM: {sw.ElapsedMilliseconds}ms");
                sw.Stop();

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

        public class SystemOverview
        {
            public string CpuName { get; set; } = "Unknown CPU";
            public string GpuName { get; set; } = "Unknown GPU";
            public string OsVersion { get; set; } = "Unknown OS";
            public string MemoryInfo { get; set; } = "Unknown Memory";
        }
    }
}
