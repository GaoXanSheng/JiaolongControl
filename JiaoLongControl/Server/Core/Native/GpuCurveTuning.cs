namespace JiaoLongControl.Server.Core.Native;

/// <summary>
/// GPU 频率偏移调校算法 (Afterburner 主滑条模型): 核心偏移 = 整条 V/F 曲线统一平移
/// (所有锚点写相同 delta, 正值超频/负值降压), 显存偏移 = P0 显存频率增量。
/// 写入含清零与读回验证, 未落地自动回滚。
///
/// Portions Copyright (c) 2026 vuplea, simple-nvidia-undervolt, MIT License
/// https://github.com/vuplea/simple-nvidia-undervolt
/// </summary>
internal static class GpuCurveTuning
{
    /// <summary>睿频算法的电压粒度 (mV): 负载下睿频停在平顶第一锚点下方一步处 (实测行为)。</summary>
    public const int BoostStepMv = 5;

    /// <summary>干净读允许的相邻锚点频率回退: 驱动会重塑上报的曲线 (bin 吸附/温漂),
    /// 相邻锚点可抖动几个 bin; 过渡态塌陷是几百 MHz, 仍会被拒。</summary>
    private const int MaxBenignDipMhz = 25;

    /// <summary>任何真实核心时钟的上界, 超过即判定为垃圾读。</summary>
    private const int MaxPlausibleCoreClockMhz = 4000;

    // ===== 清零 =====

    /// <summary>重置全部调校到 stock。曲线不合法的卡拒绝执行 —
    /// 清零是往硬件特定的控制表偏移里写零, 需要与偏移写入同等的曲线识别守卫。</summary>
    public static IReadOnlyList<string> Clear(IntPtr gpu)
    {
        IReadOnlyList<(int Mv, int Mhz)> curve = NvGpuCurveInterop.GetVfCurve(gpu);
        if (!CurveVoltsPlausible(curve))
        {
            throw new NvCurveException(UnrecognizedCurveMessage("重置调校"));
        }

        // 控制表能装下的 delta 全部清零 (等价驱动自身的重置) — 清整表而不只是当前读到的
        // 锚点, 截断读后面的外来 delta 也能清除; 然后清 P0 时钟偏移和电压提升。
        int[] deltas = NvGpuCurveInterop.GetCurveFreqDeltasKhz(gpu, NvGpuCurveInterop.MaxCurveAnchors);
        NvGpuCurveInterop.SetCurveFreqDeltasKhz(gpu, new int[NvGpuCurveInterop.MaxCurveAnchors]);
        int cleared = deltas.Count(d => d != 0);
        NvGpuCurveInterop.SetPstate0Offsets(gpu, graphicsDeltaKhz: 0, memoryDeltaKhz: 0, coreVoltageDeltaUv: 0);
        NvGpuCurveInterop.SetCoreVoltageBoostPercent(gpu, 0);

        return new[]
        {
            cleared > 0 ? $"核心 V/F 曲线: 已清除 {cleared} 个偏移点" : "核心 V/F 曲线: 本就是 stock",
            "显存/核心时钟偏移: 已归零",
            "核心电压提升: 已归零",
        };
    }

    /// <summary>不抛版本的清零, 返回 null 表示成功, 否则为错误信息。</summary>
    public static string? TryClear(IntPtr gpu)
    {
        try
        {
            Clear(gpu);
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    private static string UnrecognizedCurveMessage(string action) =>
        $"拒绝{action}: 本 GPU 的 V/F 曲线读出来不像已识别的 NVIDIA 表, 调校缓冲的字节偏移"
        + "可能与本卡不匹配, 写入会落在未知字段";

    // ===== 曲线读取: 可读性与合法性检查 =====

    /// <summary>曲线频率列是否为干净可用的读。电源状态切换前后实时状态可能短暂塌陷
    /// (整列变小或陡峭区凹陷); 稳态下 (含深度待机) 读是干净的。合格读 = 完整、单调、
    /// 合理并达到睿频频率的曲线 — 上界同时保证后续以 kHz 计的 delta 运算不溢出 int。</summary>
    public static bool CurveFreqsReadable(IReadOnlyList<(int Mv, int Mhz)> curve)
    {
        if (curve.Count < 16)
        {
            return false;
        }

        int max = 0;
        for (int i = 0; i < curve.Count; i++)
        {
            if (curve[i].Mhz < 100)
            {
                return false; // 塌陷/垃圾点
            }

            if (curve[i].Mhz > MaxPlausibleCoreClockMhz)
            {
                return false; // 超过任何真实核心时钟
            }

            if (i > 0 && curve[i].Mhz < curve[i - 1].Mhz - MaxBenignDipMhz)
            {
                return false; // 非单调 — 脏读
            }

            max = Math.Max(max, curve[i].Mhz);
        }

        return max >= NvGpuCurveInterop.MinBoostClockKhz / 1000; // 达到真实睿频
    }

    /// <summary>曲线电压轴是否像真实的 NVIDIA V/F 表: 一列足够长、递增、电压合理的锚点。
    /// 电压列与电源状态无关 (频率列不是), 所以受支持的卡在待机时也成立; 状态缓冲偏移与
    /// 硬件不匹配、字节解码成垃圾时会读出 false (过短或过窄)。它把守"写入":
    /// 调校缓冲偏移是硬件特定的, 目标曲线不被识别就不应写入任何调校。</summary>
    public static bool CurveVoltsPlausible(IReadOnlyList<(int Mv, int Mhz)> curve)
    {
        // 真实表有 80~130 个左右锚点; 不匹配的布局走几个点就会断掉
        if (curve.Count < 16)
        {
            return false;
        }

        // 电压递增。原始表按微伏递增, 相邻锚点可截断成同一毫伏 — 相等是真实表,
        // 递减才是布局不匹配的垃圾读。
        for (int i = 1; i < curve.Count; i++)
        {
            if (curve[i].Mv < curve[i - 1].Mv)
            {
                return false;
            }
        }

        // …且跨度要进入真实睿频电压区间 (对低压移动端表宽容)
        int span = curve[^1].Mv - curve[0].Mv;
        return span >= 200 && curve[^1].Mv >= 850;
    }

    /// <summary>两点间直线段在某电压处的频率, 区间外钳位 — 驱动更细粒度的曲线就是这样
    /// 填充锚点间隙的。</summary>
    public static int SegmentClockAt((int Mv, int Mhz) a, (int Mv, int Mhz) b, int mv)
    {
        if (mv <= a.Mv)
        {
            return a.Mhz;
        }

        if (mv >= b.Mv)
        {
            return b.Mhz;
        }

        return a.Mhz + (int)Math.Round((double)(b.Mhz - a.Mhz) * (mv - a.Mv) / (b.Mv - a.Mv));
    }

    /// <summary>曲线的工作点 — 负载下睿频停的位置 — 仅由生效曲线形状推断: 平顶第一锚点下方
    /// 一个 <see cref="BoostStepMv"/> 处 (下限为其下锚点), 频率取线段在该处的值。平顶是曲线
    /// 顶部一段等频的最长连续段; 单锚点的顶不是压平 (stock 峰值或读回噪声), 不报告工作点。
    /// 频率列不干净时返回 null。</summary>
    public static (int Mv, int Mhz)? EffectiveOperatingPoint(IReadOnlyList<(int Mv, int Mhz)> effective)
    {
        if (!CurveFreqsReadable(effective))
        {
            return null;
        }

        int flatStart = FlatStartIndex(effective);
        if (flatStart == effective.Count - 1)
        {
            return null; // 单锚点顶不是平台
        }

        if (flatStart == 0)
        {
            return effective[0];
        }

        (int Mv, int Mhz) below = effective[flatStart - 1];
        (int Mv, int Mhz) flat = effective[flatStart];
        int mv = Math.Max(below.Mv, flat.Mv - BoostStepMv);
        return (mv, SegmentClockAt(below, flat, mv));
    }

    /// <summary>曲线平顶的第一个下标 — 与末锚点等频的最长连续段起点。整条曲线等频时为 0。</summary>
    private static int FlatStartIndex(IReadOnlyList<(int Mv, int Mhz)> curve)
    {
        int flatStart = curve.Count - 1;
        while (flatStart > 0 && curve[flatStart - 1].Mhz == curve[^1].Mhz)
        {
            flatStart--;
        }

        return flatStart;
    }

    // ===== 偏移应用 =====

    /// <summary>
    /// 应用核心/显存频率偏移 (Afterburner 主滑条模型): 核心偏移 = 整条 V/F 曲线统一
    /// 平移 (所有锚点写相同 delta, 正值超频/负值降压), 显存偏移走 Pstates 通道。
    /// 顺序约束: 写 Pstates 会重建性能表并抹掉曲线 delta, 必须先写显存再写曲线。
    /// 只调显存时不依赖曲线接口 — 被 OEM 桩化 V/F 接口的卡也可用。核心偏移写入后
    /// 读回控制表验证, 不一致 (偏移落进保留字节) 回滚到 stock 并抛出。
    /// </summary>
    public static IReadOnlyList<string> ApplyOffsets(IntPtr gpu, int coreOffsetKhz, int memoryDeltaKhz)
    {
        // 核心偏移为 0: 不依赖 V/F 曲线接口 (被 OEM 桩化的卡也可用)
        if (coreOffsetKhz == 0)
        {
            NvGpuCurveInterop.SetPstate0Offsets(gpu, graphicsDeltaKhz: 0,
                memoryDeltaKhz: memoryDeltaKhz, coreVoltageDeltaUv: 0);
            return new[]
            {
                memoryDeltaKhz != 0
                    ? $"显存频率偏移 {memoryOffsetText(memoryDeltaKhz)} 已应用"
                    : "偏移均为 0, 显存已恢复 stock",
            };
        }

        Clear(gpu);
        IReadOnlyList<(int Mv, int Mhz)> curve = NvGpuCurveInterop.GetVfCurve(gpu);
        if (!CurveVoltsPlausible(curve))
        {
            throw new NvCurveException(UnrecognizedCurveMessage("应用频率偏移"));
        }

        NvGpuCurveInterop.SetPstate0Offsets(gpu, graphicsDeltaKhz: 0,
            memoryDeltaKhz: memoryDeltaKhz, coreVoltageDeltaUv: 0);

        int n = curve.Count;
        var deltas = new int[n];
        for (int i = 1; i < n; i++)
        {
            deltas[i] = coreOffsetKhz; // 锚点 0 无控制条目, 保持 0
        }

        NvGpuCurveInterop.SetCurveFreqDeltasKhz(gpu, deltas);

        // 读回验证: 核心锚点应全部保持写入的均匀偏移
        int[] readBack = NvGpuCurveInterop.GetCurveFreqDeltasKhz(gpu, n);
        if (readBack.Length > 1 && readBack.Skip(1).Any(d => d != coreOffsetKhz))
        {
            string reverted = TryClear(gpu) is { } clearFailure
                ? $"回滚到 stock 也失败了 ({clearFailure})"
                : "已回滚到 stock";
            throw new NvCurveException(
                $"写入后频率偏移读回不一致, 控制表偏移与本 GPU 不匹配 — {reverted}");
        }

        var lines = new List<string>
        {
            $"核心频率偏移 {coreOffsetKhz / 1000.0:+0;-0} MHz 已应用 (整条 V/F 曲线)",
        };
        if (memoryDeltaKhz != 0)
        {
            lines.Add($"显存频率偏移 {memoryOffsetText(memoryDeltaKhz)} 已应用");
        }

        return lines;
    }

    private static string memoryOffsetText(int deltaKhz) =>
        deltaKhz == 0 ? "0 MHz" : $"{deltaKhz / 1000.0:+0;-0} MHz";

    // ===== Pstates (显存偏移读取) =====

    /// <summary>P0 性能状态 (调校所在的 3D 状态) 的已填充时钟与基础电压条目,
    /// 驱动的计数钳到结构边界; 无 P0 时两列为空。</summary>
    private static (IReadOnlyList<Pstate20ClockEntry> Clocks, IReadOnlyList<Pstate20BaseVoltageEntry> BaseVoltages)
        P0Entries(Pstates20InfoV1 info)
    {
        int numPstates = (int)Math.Min(info.NumPstates, (uint)Pstates20InfoV1.MaxPstates);
        for (int p = 0; p < numPstates; p++)
        {
            if (info.Pstates[p].PstateId == 0)
            {
                return (
                    info.Pstates[p].Clocks
                        .Take((int)Math.Min(info.NumClocks, (uint)Pstate20.MaxClocks)).ToArray(),
                    info.Pstates[p].BaseVoltages
                        .Take((int)Math.Min(info.NumBaseVoltages, (uint)Pstate20.MaxBaseVoltages)).ToArray());
            }
        }

        return (Array.Empty<Pstate20ClockEntry>(), Array.Empty<Pstate20BaseVoltageEntry>());
    }

    /// <summary>P0 中某时钟域的条目, 没有时为 null。</summary>
    private static Pstate20ClockEntry? P0Clock(Pstates20InfoV1 info, uint domainId)
    {
        foreach (Pstate20ClockEntry entry in P0Entries(info).Clocks)
        {
            if (entry.DomainId == domainId)
            {
                return entry;
            }
        }

        return null;
    }

    /// <summary>当前应用的显存频率偏移 (kHz), 未应用时为 0。</summary>
    public static int GetMemoryOffsetKhz(IntPtr gpu)
        => P0Clock(NvGpuCurveInterop.GetPstates20(gpu), NvGpuCurveInterop.ClockDomainMemory)?.FreqDeltaKhz.Value ?? 0;

    /// <summary>当前应用的核心频率偏移 (kHz): 曲线全部锚点的 delta 一致时返回该值
    /// (Afterburner 主滑条模型), 否则 (压平/逐点自定义) 返回 0, 界面不显示偏移值。</summary>
    public static int GetCoreOffsetKhz(IntPtr gpu)
    {
        int[] deltas = NvGpuCurveInterop.GetCurveFreqDeltasKhz(gpu, NvGpuCurveInterop.GetVfCurve(gpu).Count);
        if (deltas.Length <= 1)
        {
            return 0;
        }

        int first = deltas[1];
        for (int i = 2; i < deltas.Length; i++)
        {
            if (deltas[i] != first)
            {
                return 0;
            }
        }

        return first;
    }

    /// <summary>显存偏移的驱动合法范围 (kHz); 读不到时退化为 ±1000 MHz 的普遍上限。</summary>
    public static (int MinKhz, int MaxKhz) GetMemoryOffsetRangeKhz(IntPtr gpu)
    {
        var entry = P0Clock(NvGpuCurveInterop.GetPstates20(gpu), NvGpuCurveInterop.ClockDomainMemory);
        return entry is { } e && e.FreqDeltaKhz.ValueMax > e.FreqDeltaKhz.ValueMin
            ? (e.FreqDeltaKhz.ValueMin, e.FreqDeltaKhz.ValueMax)
            : (-1_000_000, 1_000_000);
    }

    /// <summary>出厂基准显存频率 (MHz) — 显存偏移的参照, 静态出厂值, 任何电源状态下都可读。</summary>
    public static int BaseMemoryClockMhz(IntPtr gpu)
    {
        uint khz = NvGpuCurveInterop.GetClockFrequencyKhz(gpu,
            NvGpuCurveInterop.ClockFreqTypeBase, NvGpuCurveInterop.ClockDomainMemory);
        return (int)(khz / 1000);
    }
}
