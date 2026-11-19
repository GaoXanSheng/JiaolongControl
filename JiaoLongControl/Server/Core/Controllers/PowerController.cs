using System.Diagnostics;
using System.Runtime.InteropServices;
using JiaoLongControl.Server.Core.Utils;

namespace JiaoLongControl.Server.Core.Controllers;

[ComVisible(true)]
[ClassInterface(ClassInterfaceType.AutoDual)]
public class PowerController
{
    public CommandResult SetCPUMaxFrequency(uint mhz)
        => RunAll(new[]
        {
            "/overlaysetactive overlay_scheme_none",
            "/attributes SUB_PROCESSOR 75b0ae3f-bce0-45a7-8c89-c9611c25e100 -ATTRIB_HIDE",
            $"/setacvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCFREQMAX {mhz}",
            $"/setdcvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCFREQMAX {mhz}",
            "/setactive SCHEME_CURRENT"
        }, $"CPU 最大频率已限制为 {mhz} MHz");

    public CommandResult DisableTurbo()
        => RunAll(new[]
        {
            "/attributes SUB_PROCESSOR be337238-0d82-4146-a960-4f3749d470c7 -ATTRIB_HIDE",
            "/setacvalueindex SCHEME_CURRENT SUB_PROCESSOR PERFBOOSTMODE 0",
            "/setdcvalueindex SCHEME_CURRENT SUB_PROCESSOR PERFBOOSTMODE 0",
            "/setactive SCHEME_CURRENT"
        }, "睿频已禁用");

    public CommandResult EnableTurbo()
        => RunAll(new[]
        {
            "/setacvalueindex SCHEME_CURRENT SUB_PROCESSOR PERFBOOSTMODE 2",
            "/setdcvalueindex SCHEME_CURRENT SUB_PROCESSOR PERFBOOSTMODE 2",
            "/setactive SCHEME_CURRENT"
        }, "睿频已开启");

    /// <summary>依序执行 powercfg 命令，任一步失败立即返回该失败结果。</summary>
    private CommandResult RunAll(string[] commands, string successMessage)
    {
        foreach (var cmd in commands)
        {
            var result = RunPowerCfgCore(cmd, "[PowerController] ");
            if (!result.Success)
                return result;
        }
        return new CommandResult(true, successMessage);
    }

    /// <summary>执行 powercfg 并统一容错；错误文案保持原有 [PowerController] 前缀。</summary>
    private CommandResult RunPowerCfgCore(string arguments, string errorPrefix)
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
            process!.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit(5000);

            if (process.ExitCode != 0)
            {
                return new CommandResult(false, $"{errorPrefix}powercfg {arguments} 失败: {error}");
            }

            return new CommandResult(true, "执行成功");
        }
        catch (Exception ex)
        {
            return new CommandResult(false, $"{errorPrefix}执行 powercfg 异常: {ex.Message}");
        }
    }
}
