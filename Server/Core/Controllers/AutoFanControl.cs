using System.Management;
using JiaoLongControl.Server.Core.Utils;

namespace JiaoLongControl.Server.Core.Controllers
{
    [System.Runtime.InteropServices.ComVisible(true)]
    public class AutoFanControl : IDisposable
    {
        private volatile bool _isRunning = false;

        private CancellationTokenSource? _cts;
        private Task? _controlTask;

        private const int IntervalMs = 3000;

        private const int RPM_UNIT_DIVISOR = 100;
        private const int MAX_FAN_BYTE = 58;
        private const int MIN_FAN_BYTE = 0;

        private readonly FanController _fanController = new();

        public bool IsRunning()
        {
            return _isRunning;
        }

        public void Start()
        {
            if (_isRunning)
                return;

            if (!ConfigController.Config.EnableAdvancedFanControlSystem)
            {
                Logger.Info("Auto Fan Control is disabled by config.");
                return;
            }

            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            _controlTask = Task.Factory.StartNew(
                () => ControlLoop(token),
                token,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default
            );
        }

        public void Stop()
        {
            if (!_isRunning)
                return;

            Logger.Info("Auto Fan Control stopping...");

            _cts?.Cancel();

            try
            {
                _controlTask?.Wait(2000);
            }
            catch (AggregateException)
            {
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message);
            }
            finally
            {
                _isRunning = false;
            }
        }

        private float _lastLoggedTemp = -1000f;

        private void ControlLoop(CancellationToken token)
        {
            _isRunning = true;
            Logger.Info("Auto Fan Control started.");

            Queue<float> tempQueue = new();
            const int smoothSampleCount = 3;

            try
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        float rawTemp = GetCurrentCpuTempByWMI();

                        tempQueue.Enqueue(rawTemp);
                        if (tempQueue.Count > smoothSampleCount)
                            tempQueue.Dequeue();

                        float smoothTemp = tempQueue.Average();

                        if (Math.Abs(smoothTemp - _lastLoggedTemp) >= 1.0f)
                        {
                            Logger.Info("CPU Temp: {0:F1}", smoothTemp);
                            _lastLoggedTemp = smoothTemp;
                        }

                        int targetByte = CalculateFanSpeed(smoothTemp);

                        ApplyFanSpeed(targetByte);

                        Task.Delay(IntervalMs, token).Wait(token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex);
                        Thread.Sleep(2000);
                    }
                }
            }
            finally
            {
                _isRunning = false;
                Logger.Info("Auto Fan Control stopped.");
            }
        }

        private float GetCurrentCpuTempByWMI()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    @"root\WMI",
                    "SELECT * FROM MSAcpi_ThermalZoneTemperature");

                foreach (ManagementObject obj in searcher.Get())
                {
                    var tempObj = obj["CurrentTemperature"];
                    if (tempObj == null) continue;

                    double tempKelvin = Convert.ToDouble(tempObj);
                    float tempCelsius = (float)((tempKelvin - 2732) / 10.0);

                    if (tempCelsius > 0 && tempCelsius < 130)
                        return tempCelsius;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }

            return 55.0f;
        }

        private int CalculateFanSpeed(float currentTemp)
        {
            var configPoints = ConfigController.Config.AdvancedFanControlSystemConfig;

            if (configPoints == null || configPoints.Count == 0)
                return 25;

            var sortedPoints = configPoints.OrderBy(p => p.temp).ToList();
            double targetRpm;

            if (currentTemp <= sortedPoints.First().temp)
            {
                targetRpm = sortedPoints.First().speed;
            }
            else if (currentTemp >= sortedPoints.Last().temp)
            {
                targetRpm = sortedPoints.Last().speed;
            }
            else
            {
                targetRpm = sortedPoints.First().speed;

                for (int i = 0; i < sortedPoints.Count - 1; i++)
                {
                    var p1 = sortedPoints[i];
                    var p2 = sortedPoints[i + 1];

                    if (currentTemp >= p1.temp && currentTemp <= p2.temp)
                    {
                        double ratio =
                            (currentTemp - p1.temp) /
                            (double)(p2.temp - p1.temp);

                        targetRpm =
                            p1.speed +
                            (p2.speed - p1.speed) * ratio;
                        break;
                    }
                }
            }

            int targetByte = (int)Math.Round(targetRpm / RPM_UNIT_DIVISOR);
            return Math.Clamp(targetByte, MIN_FAN_BYTE, MAX_FAN_BYTE);
        }

        private int _lastAppliedByte = -1;

        private void ApplyFanSpeed(int speedByte)
        {
            if (speedByte == _lastAppliedByte)
                return;

            int rpm = speedByte * 100;

            if (_fanController.SetFanSpeed((byte)speedByte))
            {
                Logger.Info(
                    "Fan Speed Applied: {1} RPM",
                    rpm
                );

                _lastAppliedByte = speedByte;
            }
            else
            {
                Logger.Error($"Fan Speed Apply Failed: EC={speedByte}");
            }
        }

        public void Dispose()
        {
            Stop();
            _cts?.Dispose();
        }
    }
}