using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;

namespace JiaoLongControl.Server.Core.Native;

/// <summary>NVAPI 调用失败 (状态码非 0) 或调校前置条件不满足。</summary>
public class NvCurveException : Exception
{
    public NvCurveException(string message) : base(message) { }
}

/// <summary>
/// NVIDIA NVAPI 的最小互操作层: 读取/写入超频调校工具 (Afterburner 等) 编程进驱动的
/// 时钟/电压曲线调校。
///
/// NVAPI 只导出 <c>nvapi_QueryInterface</c>, 以函数 ID 映射到真实入口。这里的 ID 与
/// pack 结构布局不属于公开 SDK, 是超频工具社区长期稳定使用的逆向定义。每个调校结构
/// 以 <c>version</c> 字开头, 编码 <c>sizeof(struct) | (版本号 &lt;&lt; 16)</c>, 驱动会校验。
///
/// Portions Copyright (c) 2026 vuplea, simple-nvidia-undervolt, MIT License
/// https://github.com/vuplea/simple-nvidia-undervolt
/// </summary>
internal static class NvGpuCurveInterop
{
    // --- nvapi_QueryInterface 函数 ID ---
    private const uint IdInitialize = 0x0150E828;
    private const uint IdUnload = 0xD22BDD7E;
    private const uint IdGetErrorMessage = 0x6C2D048C;
    private const uint IdEnumPhysicalGPUs = 0xE5AC921F;
    private const uint IdGpuGetFullName = 0xCEEE8E9F;

    private const uint IdGpuGetPstates20 = 0x6FF81213;
    private const uint IdGpuSetPstates20 = 0x0F4DAE6B;

    /// <summary>ClkDomainsGetInfo — 时钟域信息, 只读见证控制表 delta 单位 (Pascal 半 kHz)。</summary>
    private const uint IdGpuGetClkDomainsInfo = 0x64B43A6A;

    /// <summary>ClkVfPointsGetInfo — 卡自身的 VF 点位掩码。</summary>
    private const uint IdGpuGetClockBoostMask = 0x507B4B59;

    /// <summary>ClkVfPointsGetControl — 可编辑的逐点频率 delta 表。</summary>
    private const uint IdGpuGetClockBoostTable = 0x23F1B133;

    /// <summary>ClkVfPointsSetControl — 写逐点频率 delta 表。</summary>
    private const uint IdGpuSetClockBoostTable = 0x0733E009;

    private const uint IdGpuGetCoreVoltageBoostPercent = 0x9DF23CA1;
    private const uint IdGpuSetCoreVoltageBoostPercent = 0xB9306D9B;

    /// <summary>ClockClientClkVfPointsGetStatus — 实时生效的完整 V/F 曲线。</summary>
    private const uint IdGpuGetVfCurveStatus = 0x21537AD4;

    /// <summary>NvAPI_GPU_GetAllClockFrequencies — 当前/基准/睿频公共时钟。</summary>
    private const uint IdGpuGetAllClockFrequencies = 0xDCB616C3;

    /// <summary>NvAPI_GPU_GetArchInfo — 架构 ID (公开文档 API)。</summary>
    private const uint IdGpuGetArchInfo = 0xD8265D24;

    public const uint ClockFreqTypeCurrent = 0;
    public const uint ClockFreqTypeBase = 1;
    public const uint ClockFreqTypeBoost = 2;

    private const int NvapiMaxPhysicalGpus = 64;
    private const int NvapiShortStringMax = 64;

    public const uint ClockDomainGraphics = 0;
    public const uint ClockDomainMemory = 4;
    public const uint VoltageDomainCore = 0;

    // --- V/F 曲线: 状态表 (生效曲线) 与控制表 (逐点频率 delta) ---
    //
    // 两个缓冲都是逐点条目的扁平数组, 一个电压锚点一条; 控制表相对状态表错位一个锚点
    // (见下方 off-by-one 说明)。字节偏移是对着已知 Afterburner 曲线实测的 — SDK 头文件
    // 隐含的类型化布局与驱动不符, 所以按原始字节处理。状态表报告"生效"曲线 (反映已应用
    // 偏移); 控制表只保存可编辑频率 delta (stock 时为 0)。
    //
    // 两类请求都带请求掩码 (版本字之后), 指名要填充的点位槽位; 掩码点名列不存在的槽位时
    // 驱动整体拒绝并返回 NVAPI_ERROR — 所以掩码必须从驱动读 (GetVfPointsMask), 不能假定。
    private const int ControlTableSize = 9248; // ClkVfPointsGetControl / SetControl, 版本 1
    public const int CtrlEntryBase = 0x64;
    public const int CtrlEntryStride = 36;
    public const int CtrlDeltaOffset = 0x18; // 条目内的符号 kHz 频率 delta

    private const int StatusCurveSize = 7208; // ClkVfPointsGetStatus, 版本 1
    public const int StatusEntryBase = 0x40;
    public const int StatusEntryStride = 28;
    public const int StatusTypeOffset = 0x04; // 点位域: 0 = 核心, 1 = 显存
    public const int StatusFreqOffset = 0x08; // kHz
    public const int StatusVoltOffset = 0x0C; // µV

    // 真实 V/F 曲线的判读基准 — 曲线解码、可读性检查共用, 避免口径漂移。
    // 电压窗口包住一切真实核心电压轨; 睿频下限低到限功耗的移动端也能通过,
    // 而整体塌陷的过渡读 (每个频率都小几百 MHz) 会被拒。
    public const int MinCoreVoltUv = 300_000;
    public const int MaxCoreVoltUv = 1_300_000;
    public const int MinBoostClockKhz = 1_200_000;

    private const int ClockMasksSize = 6188; // ClkVfPointsGetInfo, 版本 1

    /// <summary>ClkVfPoints 结构里请求掩码字段的宽度: 版本字后 32 字节, 每点位槽位一比特
    /// (5090 使用 132 位)。</summary>
    private const int VfPointsMaskBytes = 32;

    /// <summary>NV_GPU_ARCHITECTURE_GP100 — Pascal, 第一个有逐点 V/F 曲线 (GPU Boost 3.0) 的代际。
    /// 架构 ID 按代际递增, 更小的都没有曲线。</summary>
    internal const uint ArchitecturePascal = 0x130;

    // --- 控制表 delta 单位 ---
    //
    // 状态曲线的频率在所有代际上都是普通 kHz, 但控制表的 delta 字段不是: Pascal 是半 kHz
    // 单位 (字段值 200000 移动锚点 100 MHz), Turing 起为普通 kHz。单位通过
    // ClkDomainsGetInfo 只读见证: 图形域 delta 量程 Pascal 读 ±2,000,000 原始值,
    // 普通单位卡读 ±1,000,000 — 都对应普遍的 ±1000 MHz 偏移上限。下列辅助在字段处换算,
    // 其余代码只接触真实 kHz。

    private const int ClkDomainsSize = 2344; // ClkDomainsGetInfo, 版本 1
    private const int DomainsEntryBase = 0x28;
    private const int DomainsEntryStride = 72;
    private const int DomainsTypeOffset = 0x04;     // 公共时钟 ID: 0 = 图形, 4 = 显存
    private const int DomainsRangeMaxOffset = 0x28; // 符号数, 控制表 delta 单位
    private const int DomainsRangeMinOffset = 0x2C;

    /// <summary>NV_GPU_ARCHITECTURE_GV100 — Pascal 区间之后的第一个架构 ID。</summary>
    internal const uint ArchitectureVolta = 0x140;

    /// <summary>控制表能寻址的曲线锚点数 — <see cref="SetCurveFreqDeltasKhz"/> 接受的最大数组长度
    /// (锚点 0 计入但无条目)。清零时清到这里而不是仅当前读到的锚点, 连截断读后面的外来 delta
    /// 也一并清除。</summary>
    public const int MaxCurveAnchors =
        (ControlTableSize - CtrlEntryBase - CtrlDeltaOffset - 4) / CtrlEntryStride + 2;

    // -----------------------------------------------------------------------------------------
    // 原始缓冲读取
    // -----------------------------------------------------------------------------------------

    /// <summary>版本字可声称的最大结构尺寸: MakeVersion 把尺寸装进低 16 位,
    /// 更大的声称会环绕成垃圾字, 被驱动以误导性的"版本不兼容"拒绝。</summary>
    public const int MaxClaimedSize = 0xFFFF;

    private static readonly object InitLock = new();
    private static bool _initialized;

    [DllImport("nvapi64", EntryPoint = "nvapi_QueryInterface", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr NvAPI_QueryInterface(uint id);

    private static T GetDelegate<T>(uint id) where T : Delegate
    {
        IntPtr address = NvAPI_QueryInterface(id);
        if (address == IntPtr.Zero)
        {
            throw new NvCurveException($"NVAPI 函数 0x{id:X8} 在本机驱动中不可用");
        }

        return Marshal.GetDelegateForFunctionPointer<T>(address);
    }

    private static int MakeVersion(int structSize, int versionNumber)
        => structSize | (versionNumber << 16);

    private static void Check(int status, string action)
    {
        if (status != 0)
        {
            throw new NvCurveException($"{action} 失败: {DescribeStatus(status)}");
        }
    }

    private static string DescribeStatus(int status)
    {
        try
        {
            var message = new StringBuilder(NvapiShortStringMax);
            if (GetDelegate<GetErrorMessageDelegate>(IdGetErrorMessage)(status, message) == 0)
            {
                return $"{message} ({status})";
            }
        }
        catch
        {
            // 取不到驱动描述就回退到原始状态码
        }

        return $"NVAPI 状态码 {status}";
    }

    public static void Initialize()
        => Check(GetDelegate<NoArgDelegate>(IdInitialize)(), "NvAPI_Initialize");

    /// <summary>NVAPI 契约要求先初始化再调用其它函数; 首次使用时自动完成, 进程内仅一次。</summary>
    private static void EnsureInitialized()
    {
        if (_initialized) return;
        lock (InitLock)
        {
            if (_initialized) return;
            Check(GetDelegate<NoArgDelegate>(IdInitialize)(), "NvAPI_Initialize");
            _initialized = true;
        }
    }

    public static void Unload()
    {
        try
        {
            GetDelegate<NoArgDelegate>(IdUnload)();
        }
        catch
        {
            // 卸载属尽力而为的清理
        }
    }

    public static IntPtr[] EnumeratePhysicalGpus()
    {
        EnsureInitialized();
        var handles = new IntPtr[NvapiMaxPhysicalGpus];
        Check(GetDelegate<EnumPhysicalGpusDelegate>(IdEnumPhysicalGPUs)(handles, out int count),
            "NvAPI_EnumPhysicalGPUs");
        return handles.Take(count).ToArray();
    }

    /// <summary>GPU 全名, 读取失败返回占位文本 — 展示类信息不应让命令整体失败。</summary>
    public static string SafeFullName(IntPtr gpu)
    {
        try
        {
            var name = new StringBuilder(NvapiShortStringMax);
            Check(GetDelegate<GetFullNameDelegate>(IdGpuGetFullName)(gpu, name), "NvAPI_GPU_GetFullName");
            return name.ToString();
        }
        catch (Exception)
        {
            return "<unknown>";
        }
    }

    // --- 通用带版本结构的读/写 ---

    private static T GetStruct<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
        IntPtr gpu, uint functionId, int versionNumber, string action)
        where T : struct
    {
        int size = Marshal.SizeOf<T>();
        IntPtr buffer = AllocZeroed(size);
        try
        {
            Marshal.WriteInt32(buffer, MakeVersion(size, versionNumber));
            Check(GetDelegate<GpuStructDelegate>(functionId)(gpu, buffer), action);
            return Marshal.PtrToStructure<T>(buffer);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    /// <summary>带版本结构的写入: 与 GetStructInOut 同样的往返, 驱动留在缓冲里的输出被丢弃。</summary>
    private static void SetStruct<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
        IntPtr gpu, uint functionId, T value, int versionNumber, string action)
        where T : struct
        => GetStructInOut(gpu, functionId, value, versionNumber, action);

    /// <summary>同 GetStruct, 但先把 <paramref name="input"/> 封送进缓冲, 使调用方输入字段
    /// (如时钟类型选择器) 能进入调用。版本字总是重新盖章覆盖。</summary>
    private static T GetStructInOut<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
        IntPtr gpu, uint functionId, T input, int versionNumber, string action)
        where T : struct
    {
        int size = Marshal.SizeOf<T>();
        IntPtr buffer = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(input, buffer, false);
            Marshal.WriteInt32(buffer, MakeVersion(size, versionNumber));
            Check(GetDelegate<GpuStructDelegate>(functionId)(gpu, buffer), action);
            return Marshal.PtrToStructure<T>(buffer);
        }
        finally
        {
            Marshal.DestroyStructure<T>(buffer);
            Marshal.FreeHGlobal(buffer);
        }
    }

    /// <summary>分配零填充的非托管缓冲: 驱动看到的保留字段与填充字节是确定的, 调用方负责释放。</summary>
    private static IntPtr AllocZeroed(int size)
    {
        IntPtr buffer = Marshal.AllocHGlobal(size);
        Marshal.Copy(new byte[size], 0, buffer, size);
        return buffer;
    }

    /// <summary>把请求掩码盖在版本字之后的字段上 — 声明调用者要驱动填充 (或应用) 哪些条目。</summary>
    private static void WriteRequestMask(IntPtr buffer, byte[] mask)
        => Marshal.Copy(mask, 0, buffer + 4, mask.Length);

    private static byte[] AllOnesMask(int words)
        => Enumerable.Repeat((byte)0xFF, words * 4).ToArray();

    // --- 性能状态 (显存/核心时钟偏移、基础电压) ---

    public static Pstates20InfoV1 GetPstates20(IntPtr gpu)
    {
        EnsureInitialized();
        return GetStruct<Pstates20InfoV1>(gpu, IdGpuGetPstates20, 1, "NvAPI_GPU_GetPstates20");
    }

    /// <summary>写入 P0 图形/显存时钟与核心电压偏移。显存偏移在这里应用 (曲线控制表中没有
    /// 显存通道), 清零显存偏移也走这里。注意读/写不对称: GetPstates20 报告的显存频率是
    /// 绝对值 (已折算 Afterburner 式偏移), 而驱动内部仍按 P0 delta 记录 — 在此写 0 即回到
    /// 出厂基准 (如 +440 MHz 偏移读作 14441 MHz, 写 0 后回到 14001 MHz)。</summary>
    public static void SetPstate0Offsets(IntPtr gpu, int graphicsDeltaKhz, int memoryDeltaKhz, int coreVoltageDeltaUv)
    {
        EnsureInitialized();
        var info = NewPstates20();
        info.NumPstates = 1;
        info.NumClocks = 2;
        info.NumBaseVoltages = 1;

        ref Pstate20 p0 = ref info.Pstates[0];
        p0.PstateId = 0; // P0 — Afterburner 编辑的 3D 性能状态
        p0.Clocks[0].DomainId = ClockDomainGraphics;
        p0.Clocks[0].FreqDeltaKhz.Value = graphicsDeltaKhz;
        p0.Clocks[1].DomainId = ClockDomainMemory;
        p0.Clocks[1].FreqDeltaKhz.Value = memoryDeltaKhz;
        p0.BaseVoltages[0].DomainId = VoltageDomainCore;
        p0.BaseVoltages[0].ValueDeltaUv.Value = coreVoltageDeltaUv;

        SetStruct(gpu, IdGpuSetPstates20, info, 1, "NvAPI_GPU_SetPstates20");
    }

    // --- 公共时钟频率 (当前 / 基准 / 睿频) ---

    /// <summary>读公共时钟域频率 (kHz)。基准类型报告与偏移无关的出厂频率; 无数据返回 0。</summary>
    public static uint GetClockFrequencyKhz(IntPtr gpu, uint clockType, uint domain)
    {
        EnsureInitialized();
        var input = new ClockFrequenciesV2
        {
            ClockType = clockType,
            Domains = new ClockFrequencyDomain[ClockFrequenciesV2.MaxDomains],
        };
        var result = GetStructInOut(gpu, IdGpuGetAllClockFrequencies, input, 2, "NvAPI_GPU_GetAllClockFrequencies");

        ClockFrequencyDomain entry = result.Domains[domain];
        return (entry.IsPresent & 1) != 0 ? entry.FrequencyKhz : 0;
    }

    /// <summary>卡自身的 VF 点位掩码 — 每个已填充曲线点位槽位一比特, 读自
    /// ClkVfPointsGetInfo (它本身不带输入掩码)。状态/控制调用拒绝点名列不存在槽位的掩码,
    /// 而槽位数随代际不同 (GTX 1080 约 103, 5090 为 132), 所以每次状态/控制请求都携带此掩码。
    /// 这也是每条路径上第一个 ClkVfPoints 调用: 完全没有曲线接口的卡在此失败,
    /// 并按架构给出诊断而不是裸的驱动错误。</summary>
    private static byte[] GetVfPointsMask(IntPtr gpu)
    {
        try
        {
            byte[] bytes = ReadRaw(gpu, IdGpuGetClockBoostMask, 1, ClockMasksSize, ClockMasksSize,
                requestMaskWords: 0);
            return bytes[4..(4 + VfPointsMaskBytes)];
        }
        catch (NvCurveException error)
        {
            throw CurveUnavailableDiagnosis(ArchitectureId(gpu)) is { } diagnosis
                ? new NvCurveException($"{diagnosis} ({error.Message})")
                : error;
        }
    }

    /// <summary>GPU 架构 ID (0x110 Maxwell, 0x130 Pascal, 0x160 Turing, 0x1B0 Blackwell…),
    /// 驱动不报告时返回 null — 它只喂给错误诊断, 自身失败不得挤掉原始错误。</summary>
    private static uint? ArchitectureId(IntPtr gpu)
    {
        try
        {
            // NV_GPU_ARCH_INFO_V2: version + architecture + implementation + revision
            return BitConverter.ToUInt32(ReadRaw(gpu, IdGpuGetArchInfo, 2, 16, 16, requestMaskWords: 0), 4);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>VF 点读取失败意味着什么; 架构未知时返回 null, 由驱动错误自己说话。
    /// Pascal 之前的代际没有 ClkVfPoints 接口, 完全无法调校; Pascal 起接口存在,
    /// 但部分 SKU (见过笔记本型号) 的接口被驱动桩化。</summary>
    internal static string? CurveUnavailableDiagnosis(uint? architectureId)
    {
        if (architectureId is not { } id)
        {
            return null;
        }

        if (id < ArchitecturePascal)
        {
            string family = id switch
            {
                >= 0x110 => "Maxwell",
                >= 0xE0 => "Kepler",
                >= 0xC0 => "Fermi",
                _ => $"架构 0x{id:X}",
            };
            return $"这是 {family} 世代的 GPU: 本功能调校的逐点 V/F 曲线自 Pascal (GTX 10xx, GPU Boost 3.0) "
                + "才引入, 更早的显卡无法用此方式调校";
        }

        return "本机驱动未暴露此 GPU 的 V/F 曲线接口 (部分笔记本型号可见)";
    }

    /// <summary>读取生效 V/F 曲线为有序的 (毫伏, 兆赫兹) 点列。电压列稳定; 频率列反映实时
    /// 曲线: 最低锚点在待机时钉在底频, 电源状态切换前后整列可能短暂塌陷。点序与控制表
    /// delta 按下标对齐。只统计核心域点位: 跟在核心锚点后的显存域槽位 (5090 为槽位 127)
    /// 直接终止遍历, 防止电压恰好递增的显存点混进曲线。</summary>
    public static IReadOnlyList<(int Mv, int Mhz)> GetVfCurve(IntPtr gpu)
    {
        EnsureInitialized();
        byte[] bytes = ReadRaw(gpu, IdGpuGetVfCurveStatus, 1, StatusCurveSize, StatusCurveSize,
            GetVfPointsMask(gpu));

        var points = new List<(int Mv, int Mhz)>();
        int lastVoltUv = 0;
        for (int e = StatusEntryBase; e + StatusEntryStride <= bytes.Length; e += StatusEntryStride)
        {
            if (BitConverter.ToInt32(bytes, e + StatusTypeOffset) != 0)
            {
                break; // 显存域点位 — 这些槽位跟在核心锚点后面
            }

            int freq = BitConverter.ToInt32(bytes, e + StatusFreqOffset);
            int volt = BitConverter.ToInt32(bytes, e + StatusVoltOffset);
            if (volt is < MinCoreVoltUv or > MaxCoreVoltUv)
            {
                break; // 越过已填充条目 (尾部零 / 环绕哨兵)
            }

            // 比较原始微伏而非截断后的毫伏: 亚毫伏递减同样标记真实数组的结尾。
            if (points.Count > 0 && volt <= lastVoltUv)
            {
                break; // 不再递增 — 真实数组结束
            }

            lastVoltUv = volt;
            points.Add((volt / 1000, Math.Max(0, freq) / 1000));
        }

        return points;
    }

    /// <summary>ClkDomainsGetInfo 缓冲中图形域的频率 delta 量程 (原始单位):
    /// 第一个有量程的图形条目; 布局不匹配返回 null。</summary>
    internal static (int RawMax, int RawMin)? GraphicsDeltaRange(byte[] bytes)
    {
        for (int e = DomainsEntryBase; e + DomainsEntryStride <= bytes.Length; e += DomainsEntryStride)
        {
            int rawMax = BitConverter.ToInt32(bytes, e + DomainsRangeMaxOffset);
            if (BitConverter.ToInt32(bytes, e + DomainsTypeOffset) == 0 && rawMax != 0)
            {
                return (rawMax, BitConverter.ToInt32(bytes, e + DomainsRangeMinOffset));
            }
        }

        return null;
    }

    /// <summary>控制表 delta 字段的每 kHz 原始单位数: 仅当"精确 Pascal 签名 (±2,000,000 原始值
    /// 对应 ±1000 MHz 上限)"且"架构 ID 落在 Pascal 区间"时为 2, 其余为 1。两个条件缺一不可:
    /// 只看签名会误匹配真正把上限拓宽到 ±2000 MHz 的未来普通单位卡, 在那里加倍会过量写入。
    /// 读不到见证的卡按普通单位处理 — 安全的失误, 半深度落地并由读回报告说明。</summary>
    internal static int CurveDeltaUnitScale((int RawMax, int RawMin)? graphicsRange, uint? architectureId)
        => graphicsRange == (2_000_000, -2_000_000)
           && architectureId is >= ArchitecturePascal and < ArchitectureVolta
            ? 2
            : 1;

    /// <summary>卡的控制表 delta 单位 (见 <see cref="CurveDeltaUnitScale"/>); 见证读不到时按普通单位。</summary>
    private static int GetCurveDeltaUnitScale(IntPtr gpu)
    {
        try
        {
            byte[] bytes = ReadRaw(gpu, IdGpuGetClkDomainsInfo, 1, ClkDomainsSize, ClkDomainsSize,
                requestMaskWords: 0);
            return CurveDeltaUnitScale(GraphicsDeltaRange(bytes), ArchitectureId(gpu));
        }
        catch (Exception)
        {
            return 1;
        }
    }

    /// <summary>图形域频率 delta 的驱动合法范围 (kHz), 读自 ClkDomainsGetInfo —
    /// 即"核心频率偏移"滑条的上下限 (通常 ±1000 MHz); 读不到时退化为 ±1000 MHz 普遍上限。</summary>
    public static (int MinKhz, int MaxKhz) GetCoreOffsetRangeKhz(IntPtr gpu)
    {
        try
        {
            byte[] bytes = ReadRaw(gpu, IdGpuGetClkDomainsInfo, 1, ClkDomainsSize, ClkDomainsSize,
                requestMaskWords: 0);
            var range = GraphicsDeltaRange(bytes);
            if (range is { } r)
            {
                int scale = CurveDeltaUnitScale(range, ArchitectureId(gpu));
                return (r.RawMin / scale, r.RawMax / scale);
            }
        }
        catch (Exception)
        {
            // 读不到范围就用普遍上限
        }

        return (-1_000_000, 1_000_000);
    }

    // 控制条目 j 驱动的是状态曲线的下一个锚点 (状态下标 j+1) — off-by-one 已实测验证
    // (戳控制条目 j, 状态锚点 j+1 移动写入量)。所以曲线锚点 i 的 delta 存于控制条目 i-1;
    // 锚点 0 (最低电压) 没有控制条目, 不可移动。下列辅助按曲线下标对齐 delta,
    // 调用方无需感知错位。

    /// <summary>曲线锚点 <paramref name="anchor"/> 的 delta 字段在控制表中的字节位置 (控制条目 i-1)。</summary>
    private static int CtrlDeltaPos(int anchor) => CtrlEntryBase + (anchor - 1) * CtrlEntryStride + CtrlDeltaOffset;

    /// <summary>读取逐点频率 delta (kHz), 与 <see cref="GetVfCurve"/> 下标对齐 (锚点 i 的 delta
    /// 读自控制条目 i-1)。</summary>
    public static int[] GetCurveFreqDeltasKhz(IntPtr gpu, int count)
    {
        EnsureInitialized();
        int scale = GetCurveDeltaUnitScale(gpu);
        byte[] bytes = ReadRaw(gpu, IdGpuGetClockBoostTable, 1, ControlTableSize, ControlTableSize,
            GetVfPointsMask(gpu));

        var deltas = new int[count];
        for (int i = 1; i < count; i++)
        {
            deltas[i] = BitConverter.ToInt32(bytes, CtrlDeltaPos(i)) / scale;
        }

        return deltas;
    }

    /// <summary>以读-改-写方式写入逐点频率 delta (kHz), 保留字段原样往返。
    /// <paramref name="deltasKhz"/> 与曲线下标对齐 (锚点 i 的 delta 写入控制条目 i-1;
    /// 锚点 0 无控制条目, 被忽略)。</summary>
    public static void SetCurveFreqDeltasKhz(IntPtr gpu, int[] deltasKhz)
    {
        EnsureInitialized();
        // 下面的循环按计算出的偏移戳非托管内存, 越界是堆破坏而非异常 —
        // 最高条目必须装得进控制表。
        if (deltasKhz.Length > 1 && CtrlDeltaPos(deltasKhz.Length - 1) + 4 > ControlTableSize)
        {
            throw new NvCurveException(
                $"{deltasKhz.Length} 个曲线 delta 装不进 {ControlTableSize} 字节的控制表");
        }

        int scale = GetCurveDeltaUnitScale(gpu);
        byte[] mask = GetVfPointsMask(gpu);
        IntPtr buffer = AllocZeroed(ControlTableSize);
        try
        {
            Marshal.WriteInt32(buffer, MakeVersion(ControlTableSize, 1));
            WriteRequestMask(buffer, mask);
            Check(GetDelegate<GpuStructDelegate>(IdGpuGetClockBoostTable)(gpu, buffer),
                "NvAPI_GPU_GetClockBoostTable");

            for (int i = 1; i < deltasKhz.Length; i++)
            {
                Marshal.WriteInt32(buffer, CtrlDeltaPos(i), deltasKhz[i] * scale);
            }

            Marshal.WriteInt32(buffer, MakeVersion(ControlTableSize, 1));
            WriteRequestMask(buffer, mask);
            Check(GetDelegate<GpuStructDelegate>(IdGpuSetClockBoostTable)(gpu, buffer),
                "NvAPI_GPU_SetClockBoostTable");
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    // --- 核心电压提升百分比 (Afterburner 的 "Core Voltage (%)") ---

    public static uint GetCoreVoltageBoostPercent(IntPtr gpu)
    {
        EnsureInitialized();
        return GetStruct<VoltageBoostPercentV1>(gpu, IdGpuGetCoreVoltageBoostPercent, 1,
            "NvAPI_GPU_GetCoreVoltageBoostPercent").Percent;
    }

    public static void SetCoreVoltageBoostPercent(IntPtr gpu, uint percent)
    {
        EnsureInitialized();
        // 先读后写, 让尾部的未知字段原样往返
        var value = GetStruct<VoltageBoostPercentV1>(gpu, IdGpuGetCoreVoltageBoostPercent, 1,
            "NvAPI_GPU_GetCoreVoltageBoostPercent");
        value.Percent = percent;
        SetStruct(gpu, IdGpuSetCoreVoltageBoostPercent, value, 1, "NvAPI_GPU_SetCoreVoltageBoostPercent");
    }

    private static Pstates20InfoV1 NewPstates20()
    {
        var info = new Pstates20InfoV1 { Pstates = new Pstate20[Pstates20InfoV1.MaxPstates] };
        for (int i = 0; i < info.Pstates.Length; i++)
        {
            info.Pstates[i].Clocks = new Pstate20ClockEntry[Pstate20.MaxClocks];
            info.Pstates[i].BaseVoltages = new Pstate20BaseVoltageEntry[Pstate20.MaxBaseVoltages];
        }

        return info;
    }

    /// <summary>按掩码字数构造全 1 掩码的原始读 (用于不带请求掩码语义的调用, 传 0 即可)。</summary>
    private static byte[] ReadRaw(IntPtr gpu, uint functionId, int versionNumber, int claimedSize,
        int allocSize, int requestMaskWords)
        => requestMaskWords < 0 || 4 + (long)requestMaskWords * 4 > allocSize
            ? throw new NvCurveException($"原始读 0x{functionId:X8}: {requestMaskWords} 个请求掩码字 "
                + $"装不进 {allocSize} 字节缓冲")
            : ReadRaw(gpu, functionId, versionNumber, claimedSize, allocSize, AllOnesMask(requestMaskWords));

    /// <summary>读取某函数的原始缓冲。claimedSize 可小于 allocSize: 驱动按版本字校验声称尺寸,
    /// 但可能写入 (更大的) 真实结构, 多出的分配让溢出落在我们自己的填充里而不是破坏堆。</summary>
    private static byte[] ReadRaw(IntPtr gpu, uint functionId, int versionNumber, int claimedSize,
        int allocSize, byte[] requestMask)
    {
        // 越过分配的声称尺寸/掩码会让驱动越界写, 或我们自己把掩码写出界 —
        // 那是非托管堆破坏, 不是异常。
        if (claimedSize < 4 || claimedSize > allocSize || 4 + (long)requestMask.Length > allocSize)
        {
            throw new NvCurveException($"原始读 0x{functionId:X8}: 尺寸 {claimedSize} 或 "
                + $"{requestMask.Length} 字节请求掩码装不进 {allocSize} 字节缓冲");
        }

        if (claimedSize > MaxClaimedSize)
        {
            throw new NvCurveException($"原始读 0x{functionId:X8}: 版本字以 16 位编码声称尺寸, "
                + $"最多 {MaxClaimedSize}");
        }

        IntPtr buffer = AllocZeroed(allocSize);
        try
        {
            Marshal.WriteInt32(buffer, MakeVersion(claimedSize, versionNumber));
            WriteRequestMask(buffer, requestMask);
            Check(GetDelegate<GpuStructDelegate>(functionId)(gpu, buffer), $"原始读 0x{functionId:X8}");
            var bytes = new byte[allocSize];
            Marshal.Copy(buffer, bytes, 0, allocSize);
            return bytes;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int NoArgDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int EnumPhysicalGpusDelegate([Out] IntPtr[] handles, out int count);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private delegate int GetErrorMessageDelegate(int status, StringBuilder message);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private delegate int GetFullNameDelegate(IntPtr gpu, StringBuilder name);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GpuStructDelegate(IntPtr gpu, IntPtr data);
}
