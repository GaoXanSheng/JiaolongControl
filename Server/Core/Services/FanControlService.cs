using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using JiaoLongControl.Server.Core.Models;
using JiaoLongControl.Server.Core.Controllers;
using JiaoLongControl.Server.Core.Repositories;
using JiaoLongControl.Server.Core.Utils;

namespace JiaoLongControl.Server.Core.Services
{
    public static class FanControlService
    {
        private static bool _isRunning = false;
        private static List<FanCurvePoint> _fanCurve = new();
        
        // 全局单例的硬件监控对象，供 CliProgramEnumerationType 和本服务共同使用
        // 避免多次实例化导致资源冲突
        public static readonly ComputerInformation ComputerInfo = new ComputerInformation();

        /// <summary>
        /// 启动温控服务
        /// </summary>
        public static void Start()
        {
            if (_isRunning) return;
            _isRunning = true;
            
            // 在后台线程运行，避免阻塞 UI
            Task.Run(Loop);
            Logger.Info("FanControlService 已启动");
        }

        /// <summary>
        /// 停止温控服务
        /// </summary>
        public static void Stop()
        {
            _isRunning = false;
            // 服务停止时，可以选择将风扇恢复自动控制（取决于具体需求）
            // Fan.SetMaxFanSpeedSwitch("0"); 
            Logger.Info("FanControlService 已停止");
        }

        /// <summary>
        /// 重新加载风扇曲线（通常由前端保存配置后调用）
        /// </summary>
        public static void ReloadFanCurve(string json)
        {
            try
            {
                var curve = JsonSerializer.Deserialize<List<FanCurvePoint>>(json);
                _fanCurve = curve ?? new();
                Logger.Info($"FanCurve 重载成功，共 {_fanCurve.Count} 个节点");
            }
            catch (Exception ex)
            {
                Logger.Error($"FanCurve 重载失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 主循环
        /// </summary>
        private static async Task Loop()
        {
            // 1. 尝试加载本地配置文件
            LoadInitialCurve();

            while (_isRunning)
            {
                try
                {
                    // 如果曲线为空，不执行任何操作（或者可以设置为默认转速）
                    if (_fanCurve.Count > 0)
                    {
                        int temp = GetCpuTemperature();
                        int rpm = CalculateRpm(temp);
                        string result = Fan.SetFanSpeed(rpm.ToString());
                        Logger.Info($"Temp: {temp}°C -> Target: {rpm}%, Result: {result}");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"FanControlLoop 异常: {ex.Message}");
                }

                // 等待 5 秒
                await Task.Delay(5000);
            }
        }

        private static void LoadInitialCurve()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "fanCurve.json");
            if (File.Exists(path))
            {
                try
                {
                    string json = File.ReadAllText(path);
                    ReloadFanCurve(json);
                }
                catch (Exception ex)
                {
                    Logger.Error($"读取初始配置文件失败: {ex.Message}");
                }
            }
            else
            {
                Logger.Info("未找到 fanCurve.json，等待前端配置");
            }
        }

        private static int GetCpuTemperature()
        {
            try
            {

                JsonObject root = ComputerInfo.GetHardwareMonitorInfo();

                var cpuNode = root["Cpu"];
                if (cpuNode == null) return 0;

                var tempNode = cpuNode["Temperature"];
                if (tempNode == null) return 0;

                var firstSensor = tempNode[0];
                if (firstSensor == null) return 0;

                var valueNode = firstSensor["Value"];
                if (valueNode == null) return 0;

                float tempFloat = valueNode.GetValue<float>();
                return (int)Math.Round(tempFloat);
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// 根据温度和曲线计算目标转速（线性插值）
        /// </summary>
        private static int CalculateRpm(int temperature)
        {
            if (_fanCurve.Count == 0)
            {
                return 50; // 默认安全值
            }

            // 确保按温度排序
            var sortedCurve = _fanCurve.OrderBy(p => p.Temp).ToList();

            // 1. 低于最低温度，使用最低转速
            if (temperature <= sortedCurve[0].Temp)
            {
                return sortedCurve[0].Speed;
            }

            // 2. 高于最高温度，使用最高转速
            if (temperature >= sortedCurve[^1].Temp)
            {
                return sortedCurve[^1].Speed;
            }

            for (int i = 0; i < sortedCurve.Count - 1; i++)
            {
                var lower = sortedCurve[i];
                var upper = sortedCurve[i + 1];

                if (temperature >= lower.Temp && temperature <= upper.Temp)
                {
                    double ratio = (double)(temperature - lower.Temp) / (upper.Temp - lower.Temp);
                    int interpolatedSpeed = (int)(lower.Speed + ratio * (upper.Speed - lower.Speed));
                    return interpolatedSpeed;
                }
            }

            return 50; 
        }
    }
}