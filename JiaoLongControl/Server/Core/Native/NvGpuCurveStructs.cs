using System.Runtime.InteropServices;

namespace JiaoLongControl.Server.Core.Native;

// NVAPI 调校结构的打包布局。字段顺序、数组大小与 Pack=8 由驱动 ABI 固定;
// 顶层结构首字段为版本字 (见 NvGpuCurveInterop.MakeVersion)。保留字段命名 Unknown*
// 并在读-改-写中原样往返, 驱动才接受写入。
//
// Portions Copyright (c) 2026 vuplea, simple-nvidia-undervolt, MIT License
// https://github.com/vuplea/simple-nvidia-undervolt

/// <summary>符号 delta 值及其合法范围, 单位为字段自身的单位 (kHz 或 µV)。</summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
internal struct ParameterDelta
{
    public int Value;
    public int ValueMin;
    public int ValueMax;
}

/// <summary>性能状态内的一个时钟域 (NV_GPU_PSTATE20_CLOCK_ENTRY_V1)。</summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
internal struct Pstate20ClockEntry
{
    public uint DomainId;
    public uint TypeId;
    public uint Flags;
    public ParameterDelta FreqDeltaKhz;

    // NV_GPU_PSTATE20_CLOCK_DEPENDENT_INFO: 20 字节联合。范围型时钟下是
    // minFreqKhz/maxFreqKhz/voltageDomain/minVoltageUv/maxVoltageUv;
    // 显存时钟的绝对频率 (随显存偏移浮动) 在 Data0。
    public uint Data0;
    public uint Data1;
    public uint Data2;
    public uint Data3;
    public uint Data4;
}

/// <summary>性能状态内的一个基础电压域 (NV_GPU_PSTATE20_BASE_VOLTAGE_ENTRY_V1)。</summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
internal struct Pstate20BaseVoltageEntry
{
    public uint DomainId;
    public uint Flags;
    public uint Value;
    public ParameterDelta ValueDeltaUv;
}

[StructLayout(LayoutKind.Sequential, Pack = 8)]
internal struct Pstate20
{
    public const int MaxClocks = 8;
    public const int MaxBaseVoltages = 4;

    public uint PstateId;
    public uint Flags;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = MaxClocks)]
    public Pstate20ClockEntry[] Clocks;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = MaxBaseVoltages)]
    public Pstate20BaseVoltageEntry[] BaseVoltages;
}

/// <summary>NV_GPU_PERF_PSTATES20_INFO_V1 (版本 1, 7316 字节 = 0x11C94 版本字)。</summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
internal struct Pstates20InfoV1
{
    public const int MaxPstates = 16;

    public uint Version;
    public uint Flags;
    public uint NumPstates;
    public uint NumClocks;
    public uint NumBaseVoltages;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = MaxPstates)]
    public Pstate20[] Pstates;
}

// V/F 曲线控制表 (ClkVfPointsControl, 版本 1, 9248 字节) 与状态表 (版本 1, 7208 字节)
// 是固定步长的逐点扁平数组, 布局与 SDK 头文件隐含的不一致, 因此在 NvGpuCurveInterop
// 中按原始字节读写 (见其中的偏移常量), 不经封送结构体。

/// <summary>NV_GPU_CLOCK_FREQUENCIES 中的一个公共时钟域 (bIsPresent 位域 + 频率)。</summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
internal struct ClockFrequencyDomain
{
    public uint IsPresent;     // 位 0: 该域有数据; 其余保留
    public uint FrequencyKhz;
}

/// <summary>NV_GPU_CLOCK_FREQUENCIES_V2 (264 字节)。ClockType 选择驱动上报的时钟:
/// 0 = 当前, 1 = 基准 (出厂), 2 = 睿频。基准时钟不受已应用偏移影响,
/// 因此是显存偏移生效时取回出厂显存频率的途径。</summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
internal struct ClockFrequenciesV2
{
    public const int MaxDomains = 32;

    public uint Version;
    public uint ClockType;     // 低 4 位: NV_GPU_CLOCK_FREQUENCIES_CLOCK_TYPE; 其余保留

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = MaxDomains)]
    public ClockFrequencyDomain[] Domains;
}

/// <summary>核心电压提升百分比 — Afterburner 的"Core Voltage (%)"滑条 (版本 1)。</summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
internal struct VoltageBoostPercentV1
{
    public const int MaxUnknown = 8;

    public uint Version;
    public uint Percent;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = MaxUnknown)]
    public uint[] Unknown;
}
