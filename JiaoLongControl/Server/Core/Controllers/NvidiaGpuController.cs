using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using JiaoLongControl.Server.Core.Drivers;
using JiaoLongControl.Server.Core.Models;
using JiaoLongControl.Server.Core.Utils;
using JiaoLongControl.Server.Interop;

namespace JiaoLongControl.Server.Core.Controllers
{
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.AutoDual)]
    public class NvidiaGpuController
    {
        private readonly string _smiPath = "nvidia-smi";

        public NvidiaGpuController(string customSmiPath = null)
        {
            if (!string.IsNullOrEmpty(customSmiPath))
            {
                _smiPath = customSmiPath;
            }
        }

        public CommandResult GetGpuAllStats(int gpuIndex = -1)
        {
            const string query =
                "name,driver_version,memory.total,pcie.link.width.current,utilization.gpu,utilization.memory,clocks.current.graphics,clocks.current.memory,temperature.gpu,fan.speed";
            var result = ExecuteCommand($"--query-gpu={query} --format=csv,noheader,nounits", gpuIndex);

            if (!result.Success || result.Data == null)
            {
                return new CommandResult(false, $"Failed to get stats: {result.Message}");
            }

            var dataString = result.Data as string;
            if (string.IsNullOrWhiteSpace(dataString))
            {
                return new CommandResult(false, "Received empty data from nvidia-smi.");
            }

            var values = dataString.Split(new[] { ", " }, StringSplitOptions.None);
            if (values.Length < 10)
            {
                return new CommandResult(false, "Invalid data received from nvidia-smi.");
            }

            var FanSpeed = ((FanSpeedInfo)Bridge.Instance.Fan.GetFanSpeed().Data).GPUFanSpeed;
            try
            {
                var stats = new GpuStats
                {
                    GpuName = values[0],
                    DriverVersion = values[1],
                    MemoryTotal = values[2],
                    BusWidth = values[3],
                    GpuUtilization = values[4],
                    MemoryUtilization = values[5],
                    CoreClock = values[6],
                    MemoryClock = values[7],
                    GpuTemperature = values[8],
                    FanSpeed = FanSpeed.ToString()
                };
                return new CommandResult(true, "Success", stats);
            }
            catch (Exception ex)
            {
                return new CommandResult(false, $"Failed to parse stats: {ex.Message}");
            }
        }

        public CommandResult GetGpuTemperature(int gpuIndex = -1)
            => ExecuteCommand("--query-gpu=temperature.gpu --format=csv,noheader,nounits", gpuIndex);

        public CommandResult LockGpuClock(int freq, int gpuIndex = -1)
            => ExecuteCommand($"-lgc {freq}", gpuIndex);

        public CommandResult LockGpuClock(int minFreq, int maxFreq, int gpuIndex = -1)
            => ExecuteCommand($"-lgc {minFreq},{maxFreq}", gpuIndex);

        public CommandResult ResetGpuClock(int gpuIndex = -1)
            => ExecuteCommand("-rgc", gpuIndex);

        public CommandResult LockMemoryClock(int freq, int gpuIndex = -1)
            => ExecuteCommand($"-lmc {freq}", gpuIndex);

        public CommandResult ResetMemoryClock(int gpuIndex = -1)
            => ExecuteCommand("-rmc", gpuIndex);

        public CommandResult SetPowerLimit(int watts, int gpuIndex = -1)
            => ExecuteCommand($"-pl {watts}", gpuIndex);

        public CommandResult UnlockDB()
        {
            var driver = new NVPCF();
            var installResult = driver.Install();
            if (!installResult.Success)
            {
                return new CommandResult(false, $"驱动阶段失败: {installResult.Message}");
            }

            const string deviceId = @"ACPI\NVDA0820\NPCF";
            string enableArgs = $@"/enable-device ""{deviceId}""";
            var enableRes = ExecuteSystemCommand("pnputil", enableArgs);
            if (!enableRes.Success)
            {
                return new CommandResult(false, $"UnlockDB 失败 (启用设备阶段): {enableRes.Message}");
            }

            Thread.Sleep(3000);
            string disableArgs = $@"/disable-device ""{deviceId}""";
            var disableRes = ExecuteSystemCommand("pnputil", disableArgs);
            if (!disableRes.Success)
            {
                return new CommandResult(false, $"UnlockDB 失败 (禁用设备阶段): {disableRes.Message}");
            }

            return new CommandResult(true, "UnlockDB 成功。");
        }

        private CommandResult ExecuteSystemCommand(string fileName, string arguments)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (Process? process = Process.Start(psi))
                {
                    if (process == null) return new CommandResult(false, $"无法启动 {fileName}");
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();

                    process.WaitForExit();

                    if (process.ExitCode != 0)
                    {
                        return new CommandResult(false, $"命令返回码 {process.ExitCode}: {error} {output}");
                    }

                    return new CommandResult(true, output);
                }
            }
            catch (Exception ex)
            {
                return new CommandResult(false, $"执行 {fileName} 时发生异常: {ex.Message}");
            }
        }

        private CommandResult ExecuteCommand(string arguments, int gpuIndex)
        {
            if (gpuIndex >= 0)
            {
                arguments = $"-i {gpuIndex} {arguments}";
            }

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = _smiPath,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (Process process = Process.Start(psi))
                {
                    if (process == null)
                        return new CommandResult(false, "无法启动 nvidia-smi 进程。");
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    if (process.ExitCode != 0 || !string.IsNullOrWhiteSpace(error))
                    {
                        string errMsg = !string.IsNullOrWhiteSpace(error) ? error : output;
                        return new CommandResult(false, $"执行失败: {errMsg.Trim()}");
                    }

                    return new CommandResult(true, "获取成功", output.Trim());
                }
            }
            catch (System.ComponentModel.Win32Exception)
            {
                return new CommandResult(false, "找不到 nvidia-smi，请确保安装了 NVIDIA 显卡驱动。");
            }
            catch (Exception ex)
            {
                return new CommandResult(false, $"发生异常: {ex.Message}");
            }
        }
    }
}