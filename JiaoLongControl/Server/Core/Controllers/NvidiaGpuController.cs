using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using JiaoLongControl.Server.Core.Drivers;
using JiaoLongControl.Server.Core.Services;
using JiaoLongControl.Server.Core.Utils;

namespace JiaoLongControl.Server.Core.Controllers
{
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.AutoDual)]
    public class NvidiaGpuController : IDisposable
    {
        private readonly NvidiaApiService _apiService;
        private bool _isDisposed;

        public NvidiaGpuController()
        {
            _apiService = new NvidiaApiService();
        }

        private int SanitizeGpuIndex(int gpuIndex) => gpuIndex < 0 ? 0 : gpuIndex;

        public CommandResult GetGpuTemperature(int gpuIndex = -1)
            => _apiService.GetGpuTemperature(SanitizeGpuIndex(gpuIndex));
        
        public CommandResult LockGpuClock(int freq, int gpuIndex = -1)
            => _apiService.LockGpuClock(freq, freq, SanitizeGpuIndex(gpuIndex));

        public CommandResult LockGpuClock(int minFreq, int maxFreq, int gpuIndex = -1)
            => _apiService.LockGpuClock(minFreq, maxFreq, SanitizeGpuIndex(gpuIndex));

        public CommandResult ResetGpuClock(int gpuIndex = -1)
            => _apiService.ResetGpuClock(SanitizeGpuIndex(gpuIndex));

        public CommandResult LockMemoryClock(int freq, int gpuIndex = -1)
            => _apiService.LockMemoryClock(freq, SanitizeGpuIndex(gpuIndex));

        public CommandResult ResetMemoryClock(int gpuIndex = -1)
            => _apiService.ResetMemoryClock(SanitizeGpuIndex(gpuIndex));

        public CommandResult SetPowerLimit(int watts, int gpuIndex = -1)
            => _apiService.SetPowerLimit(watts, SanitizeGpuIndex(gpuIndex));
            
        public CommandResult UnlockDB()
        {
            var driver = new NVPCF();
            var installResult = driver.Install();
            if (!installResult.Success)
            {
                return new CommandResult(false, $"驱动阶段失败: {installResult.Message}");
            }

            const string deviceId = @"ACPI\NVDA0820\NPCF";
            var enableRes = ExecuteSystemCommand("pnputil", string.Format("/enable-device \"{0}\"", deviceId));
            if (!enableRes.Success)
            {
                return new CommandResult(false, $"UnlockDB 失败 (启用设备阶段): {enableRes.Message}");
            }

            Thread.Sleep(3000);
            var disableRes = ExecuteSystemCommand("pnputil", string.Format("/disable-device \"{0}\"", deviceId));
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

        ~NvidiaGpuController() => Dispose(false);

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed) return;
            if (disposing)
            {
                _apiService?.Dispose();
            }
            _isDisposed = true;
        }
    }
}
