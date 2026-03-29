using System.Diagnostics;
using JiaoLongControl.Server.Core.Utils;

namespace JiaoLongControl.Server.Core.Controllers;

[System.Runtime.InteropServices.ComVisible(true)]
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
                    return new CommandResult(false, $"执行失败: {errMsg}");
                }

                return new CommandResult(true, output);
            }
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return new CommandResult(false, "找不到 nvidia-smi，请确保安装了 NVIDIA 显卡驱动，或尝试提供绝对路径。");
        }
        catch (Exception ex)
        {
            return new CommandResult(false, $"发生异常: {ex.Message}");
        }
    }
}