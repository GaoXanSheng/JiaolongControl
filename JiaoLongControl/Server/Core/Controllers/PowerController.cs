using System.Diagnostics;
using JiaoLongControl.Server.Core.Utils;

namespace JiaoLongControl.Server.Core.Controllers;

[System.Runtime.InteropServices.ComVisible(true)]
public class PowerController
{
    public void SetCPUMaxFrequency(uint mhz)
    {
        RunPowerCfg("/overlaysetactive overlay_scheme_none");
        RunPowerCfg("/attributes SUB_PROCESSOR 75b0ae3f-bce0-45a7-8c89-c9611c25e100 -ATTRIB_HIDE");
        RunPowerCfg($"/setacvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCFREQMAX {mhz}");
        RunPowerCfg($"/setdcvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCFREQMAX {mhz}");
        RunPowerCfg("/setactive SCHEME_CURRENT");
        Logger.Info($"[PowerController] CPU 最大频率已限制为 {mhz} MHz");
    }
    public void ResetCPUMaxFrequency()
    {
        RunPowerCfg($"/setacvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCFREQMAX 0");
        RunPowerCfg($"/setdcvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCFREQMAX 0");
        RunPowerCfg("/setactive SCHEME_CURRENT");
        Logger.Info("[PowerController] CPU 频率限制已取消");
    }
    public void SetCPUMaxState(uint percent)
    {
        if (percent > 100) percent = 100;

        RunPowerCfg($"/setacvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMAX {percent}");
        RunPowerCfg($"/setdcvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMAX {percent}");
        RunPowerCfg("/setactive SCHEME_CURRENT");

        Logger.Info($"[PowerController] CPU 最大状态限制为 {percent}%");
    }
    public void DisableTurbo()
    {
        RunPowerCfg("/attributes SUB_PROCESSOR be337238-0d82-4146-a960-4f3749d470c7 -ATTRIB_HIDE");
        RunPowerCfg($"/setacvalueindex SCHEME_CURRENT SUB_PROCESSOR PERFBOOSTMODE 0");
        RunPowerCfg($"/setdcvalueindex SCHEME_CURRENT SUB_PROCESSOR PERFBOOSTMODE 0");
        RunPowerCfg("/setactive SCHEME_CURRENT");

        Logger.Info("[PowerController] 睿频已禁用");
    }
    public void EnableTurbo()
    {
        RunPowerCfg($"/setacvalueindex SCHEME_CURRENT SUB_PROCESSOR PERFBOOSTMODE 2");
        RunPowerCfg($"/setdcvalueindex SCHEME_CURRENT SUB_PROCESSOR PERFBOOSTMODE 2");
        RunPowerCfg("/setactive SCHEME_CURRENT");

        Logger.Info("[PowerController] 睿频已开启");
    }

    private void RunPowerCfg(string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powercfg",
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var process = Process.Start(psi);
            string output = process!.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit(5000);

            if (process.ExitCode != 0)
                Logger.Error($"[PowerController] powercfg {arguments} 失败: {error}");
        }
        catch (Exception ex)
        {
            Logger.Error($"[PowerController] 执行 powercfg 异常: {ex.Message}");
        }
    }
}