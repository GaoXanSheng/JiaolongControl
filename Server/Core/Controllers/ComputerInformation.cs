using System.Text.Json.Nodes;
using System.Timers;
using OpenHardwareMonitor.Hardware;

namespace JiaoLongControl.Server.Core.Controllers
{
    public class ComputerInformation : IDisposable
    {
        private JsonObject _res = new JsonObject();
        private Computer _computer = new Computer { IsCpuEnabled = true,IsGpuEnabled = true,IsRing0Enabled = true};

        private System.Timers.Timer _refreshTimer;
        private readonly object _lock = new object();

        public event EventHandler<JsonObject> OnHardwareInfoRefreshed;

        public ComputerInformation()
        {
            _computer.Open(true);
            // 初始刷新一次数据
            UpdateHardwareMonitorInfo();

            _refreshTimer = new System.Timers.Timer(1000); // 每秒刷新一次
            _refreshTimer.Elapsed += _refreshTimer_Elapsed;
            _refreshTimer.AutoReset = true;
        }

        private void _refreshTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            UpdateHardwareMonitorInfo();
            OnHardwareInfoRefreshed?.Invoke(this, _res);
        }

        private void UpdateHardwareMonitorInfo()
        {
            lock (_lock)
            {
                _res.Clear();
                foreach (var hardwareItem in _computer.Hardware)
                {
                    hardwareItem.Update();
                    var hardwareJson = new JsonObject();

                    foreach (var sensor in hardwareItem.Sensors)
                    {
                        if (sensor.Value.HasValue && sensor.Name != null)
                        {
                            string sensorType = sensor.SensorType.ToString();
                            JsonArray data;
                            if (hardwareJson.ContainsKey(sensorType))
                            {
                                data = hardwareJson[sensorType]?.AsArray()!;
                            }
                            else
                            {
                                data = new JsonArray();
                                hardwareJson[sensorType] = data;
                            }

                            data.Add(new JsonObject
                            {
                                ["Name"] = sensor.Name,
                                ["Value"] = sensor.Value.Value
                            });
                        }
                    }
                    if (hardwareJson.Count > 0)
                    {
                        hardwareJson["Name"] = hardwareItem.Name;
                        _res[hardwareItem.HardwareType.ToString()] = hardwareJson;
                    }
                }
            }
        }

        public string GetHardwareMonitorInfo() 
        {
            lock (_lock)
            {
                return _res.ToJsonString(); 
            }
        }

        public void StartMonitoring()
        {
            _refreshTimer.Start();
        }

        public void StopMonitoring()
        {
            _refreshTimer.Stop();
        }

        public void Dispose()
        {
            _refreshTimer.Stop();
            _refreshTimer.Dispose();
            _computer.Close();
        }
    }
}