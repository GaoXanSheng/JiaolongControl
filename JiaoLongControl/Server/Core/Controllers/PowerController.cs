using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using JiaoLongControl.Server.Core.Utils;

namespace JiaoLongControl.Server.Core.Controllers;

[ComVisible(true)]
[ClassInterface(ClassInterfaceType.AutoDual)]
public class PowerController
{
    public CommandResult SetCPUMaxFrequency(uint mhz)
    {
        var commands = new[]
        {
            "/overlaysetactive overlay_scheme_none",
            "/attributes SUB_PROCESSOR 75b0ae3f-bce0-45a7-8c89-c9611c25e100 -ATTRIB_HIDE",
            $"/setacvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCFREQMAX {mhz}",
            $"/setdcvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCFREQMAX {mhz}",
            "/setactive SCHEME_CURRENT"
        };
        foreach (var cmd in commands)
        {
            var result = RunPowerCfg(cmd);
            if (!result.Success)
            {
                return  result;
            }
        }
        App.Logger.Info($"[PowerController] CPU 最大频率已限制为 {mhz} MHz");
        return new CommandResult(true, $"CPU 最大频率已限制为 {mhz} MHz");
    }
    public CommandResult ResetCPUMaxFrequency()
    {
        var commands = new[]
        {
            "/setacvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCFREQMAX 0",
            "/setdcvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCFREQMAX 0",
            "/setactive SCHEME_CURRENT"
        };
        foreach (var cmd in commands)
        {
            var result = RunPowerCfg(cmd);
            if (!result.Success)
            {
                return result;
            }
        }
        App.Logger.Info("[PowerController] CPU 频率限制已取消");
        return new CommandResult(true, "CPU 频率限制已取消");
    }
    public CommandResult SetCPUMaxState(uint percent)
    {
        if (percent > 100) percent = 100;
        var commands = new[]
        {
            $"/setacvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMAX {percent}",
            $"/setdcvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMAX {percent}",
            "/setactive SCHEME_CURRENT"
        };
        foreach (var cmd in commands)
        {
            var result = RunPowerCfg(cmd);
            if (!result.Success)
            {
                return result;
            }
        }
        App.Logger.Info($"[PowerController] CPU 最大状态限制为 {percent}%");
        return new CommandResult(true, $"CPU 最大状态限制为 {percent}%");
    }
    public CommandResult DisableTurbo()
    {
        var commands = new[]
        {
            "/attributes SUB_PROCESSOR be337238-0d82-4146-a960-4f3749d470c7 -ATTRIB_HIDE",
            "/setacvalueindex SCHEME_CURRENT SUB_PROCESSOR PERFBOOSTMODE 0",
            "/setdcvalueindex SCHEME_CURRENT SUB_PROCESSOR PERFBOOSTMODE 0",
            "/setactive SCHEME_CURRENT"
        };
        foreach (var cmd in commands)
        {
            var result = RunPowerCfg(cmd);
            if (!result.Success)
            {
                return result;
            }
        }
        App.Logger.Info("[PowerController] 睿频已禁用");
        return new CommandResult(true, "睿频已禁用");
    }
    public CommandResult EnableTurbo()
    {
        var commands = new[]
        {
            "/setacvalueindex SCHEME_CURRENT SUB_PROCESSOR PERFBOOSTMODE 2",
            "/setdcvalueindex SCHEME_CURRENT SUB_PROCESSOR PERFBOOSTMODE 2",
            "/setactive SCHEME_CURRENT"
        };
        foreach (var cmd in commands)
        {
            var result = RunPowerCfg(cmd);
            if (!result.Success)
            {
                return result;
            }
        }
        App.Logger.Info("[PowerController] 睿频已开启");
        return new CommandResult(true, "睿频已开启");
    }

    public CommandResult GetCPUMaxFrequency()
    {
        var result = RunPowerCfgWithOutput("/query SCHEME_CURRENT SUB_PROCESSOR PROCFREQMAX");
        if (!result.Success)
        {
            return new CommandResult(false, result.Message);
        }
        if (!TryParseLastTwoHex((string)result.Data!, out var ac, out var dc))
            return new CommandResult(false, "解析失败");

        return new CommandResult(true, "ok", (ac, dc));
    }
    public CommandResult GetCPUMaxState()
    {
        var result = RunPowerCfgWithOutput("/query SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMAX");
        if (!result.Success)
            return new CommandResult(false, result.Message);

        if (!TryParseLastTwoHex((string)result.Data, out var ac, out var dc))
            return new CommandResult(false, "解析失败");

        return new CommandResult(true, "ok", (ac, dc));
    }
    public CommandResult GetTurboEnabled()
    {
        var result = RunPowerCfgWithOutput("/query SCHEME_CURRENT SUB_PROCESSOR PERFBOOSTMODE");
        if (!result.Success)
            return new CommandResult(false, result.Message);

        if (!TryParseLastTwoHex((string)result.Data, out var acVal, out var dcVal))
            return new CommandResult(false, "解析失败");

        return new CommandResult(
            true,
            "ok",
            (acVal != 0, dcVal != 0)
        );
    }
    private CommandResult RunPowerCfg(string arguments)
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
            {
                App.Logger.Error($"[PowerController] powercfg {arguments} 失败: {error}");
                return new CommandResult(false, "执行 powercfg 失败 ");
            }
        }
        catch (Exception ex)
        {
            App.Logger.Error($"[PowerController] 执行 powercfg 异常: {ex.Message}");
            return new CommandResult(false,"执行 powercfg 异常");
        }
        return new CommandResult(true, "执行成功");
    }
    private CommandResult RunPowerCfgWithOutput(string arguments)
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
            {
                App.Logger.Error($"powercfg {arguments} 失败: {error}");
                return new CommandResult(false, error);
            }

            return new CommandResult(true, "ok", output);
        }
        catch (Exception ex)
        {
            App.Logger.Error($"执行 powercfg 异常: {ex.Message}");
            return new CommandResult(false, ex.Message);
        }
    }
    private bool TryParseLastTwoHex(string output, out uint ac, out uint dc)
    {
        ac = 0;
        dc = 0;
        var matches = Regex.Matches(output, @"0x[0-9a-fA-F]+");
        if (matches.Count < 2)
            return false;
        ac = Convert.ToUInt32(matches[matches.Count - 2].Value, 16);
        dc = Convert.ToUInt32(matches[matches.Count - 1].Value, 16);
        return true;
    }
}