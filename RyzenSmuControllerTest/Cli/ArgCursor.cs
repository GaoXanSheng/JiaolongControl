using RyzenSmuControllerTest.Infrastructure;

namespace RyzenSmuControllerTest.Cli;

/// <summary>
/// 位置参数游标。分发器在调用处理器前已消耗选项名，游标停在第一个值 token 上。
/// TryRead* 的推进语义与旧版 TryParse* 行为逐字一致：
/// 缺值时报"参数 X 缺少值"且游标停在末尾（外层循环随之结束）；
/// 值非法时报错并跳过该值继续；值合法但超范围仅告警不失败（工具定位是实验性，允许越界写入）。
/// </summary>
internal sealed class ArgCursor
{
    private readonly string[] _args;
    private int _i;

    public ArgCursor(string[] args) => _args = args;

    public bool AtEnd => _i >= _args.Length;

    public string Current => _args[_i];

    public void Advance() => _i++;

    public bool TryReadHex(out uint value)
    {
        value = 0;
        if (!EnsureValue()) return false;
        if (!uint.TryParse(_args[_i], System.Globalization.NumberStyles.HexNumber, null, out value))
        {
            RejectValue("不是有效十六进制数");
            return false;
        }
        _i++;
        return true;
    }

    public bool TryReadInt(int min, int max, out int value)
    {
        value = 0;
        if (!EnsureValue()) return false;
        if (!int.TryParse(_args[_i], out value))
        {
            RejectValue("不是有效整数");
            return false;
        }
        _i++;
        WarnOutOfRange(value, min, max);
        return true;
    }

    public bool TryReadDouble(double min, double max, out double value)
    {
        value = 0;
        if (!EnsureValue()) return false;
        if (!double.TryParse(_args[_i], out value))
        {
            RejectValue("不是有效数值");
            return false;
        }
        _i++;
        WarnOutOfRange(value, min, max);
        return true;
    }

    /// <summary>读取邮箱类型 token（mp1/rsmu）。类型无效时不推进游标——与旧版一致，
    /// 该 token 会落回外层循环再报一次"未知参数"并计入退出码。</summary>
    public bool TryReadMailbox(string optionName, out bool isMp1)
    {
        isMp1 = false;
        if (_i >= _args.Length)
        {
            ConsoleLog.Error($"参数 {optionName} 缺少邮箱类型 (mp1/rsmu)");
            return false;
        }
        if (_args[_i].Equals("mp1", StringComparison.OrdinalIgnoreCase)) isMp1 = true;
        else if (!_args[_i].Equals("rsmu", StringComparison.OrdinalIgnoreCase))
        {
            ConsoleLog.Error($"参数 {optionName} 邮箱类型无效: '{_args[_i]}' (应为 mp1 或 rsmu)");
            return false;
        }
        _i++;
        return true;
    }

    private bool EnsureValue()
    {
        if (_i < _args.Length) return true;
        ConsoleLog.Error($"参数 {_args[_i - 1]} 缺少值");
        return false;
    }

    private void RejectValue(string reason)
    {
        ConsoleLog.Error($"参数 {_args[_i - 1]} 值 '{_args[_i]}' {reason}");
        _i++;
    }

    private void WarnOutOfRange(int value, int min, int max)
    {
        if (value < min || value > max)
            ConsoleLog.Log($"    (值 {value} 超出范围 [{min}, {max}], 仍继续执行)");
    }

    private void WarnOutOfRange(double value, double min, double max)
    {
        if (value < min || value > max)
            ConsoleLog.Log($"    (值 {value} 超出范围 [{min}, {max}], 仍继续执行)");
    }
}
