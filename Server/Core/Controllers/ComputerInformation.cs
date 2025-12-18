using System.Text.Json.Nodes;
using LibreHardwareMonitor.Hardware;

namespace JiaoLongControl.Server.Core.Controllers
{
    public class ComputerInformation : IDisposable
    {
        private JsonObject _res = new JsonObject();
        private Computer _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true, 
            IsMemoryEnabled = false,
            IsMotherboardEnabled = false,
            IsControllerEnabled = false,
            IsNetworkEnabled = false,
            IsStorageEnabled = false,
            IsBatteryEnabled = false,
        };

        public ComputerInformation()
        {
            _computer.Open();
        }

        public JsonObject GetHardwareMonitorInfo()
        {
            // 每次调用都刷新数据
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
            return _res;
        }

        public void Dispose()
        {
            _computer.Close();
        }
    }
}