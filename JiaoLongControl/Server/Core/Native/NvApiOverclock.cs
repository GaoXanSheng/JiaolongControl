using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace JiaoLongControl.Server.Core.Native
{
    /// <summary>
    /// NVIDIA 超频私有 NVAPI 接口封装（Afterburner 同款路径，全程用户态，无需内核驱动）。
    /// 结构体布局与 NvAPIWrapper (falahati, MIT) 反汇编布局一致，函数 ID 为 NVAPI 私有 ID。
    /// </summary>
    internal static class NvApiOverclock
    {
        private const string DllName = "nvapi64.dll";

        // NVAPI 私有函数 ID
        private const uint IdInitialize = 0x0150E828;
        private const uint IdEnumPhysicalGPUs = 0xE5AC921F;
        private const uint IdGetClockBoostRanges = 0x64B43A6A;
        private const uint IdGetClockBoostTable = 0x23F1B133;
        private const uint IdSetClockBoostTable = 0x0733E009;
        private const uint IdGetClockVfStatus = 0x21537AD4;
        private const uint IdGetClockBoostMask = 0x507B4B59;
        private const uint IdGetClockBoostLock = 0xE440B867;
        private const uint IdSetClockBoostLock = 0x39442CFB;
        private const uint IdGetCoreVoltageBoostPercent = 0x9DF23CA1;
        private const uint IdSetCoreVoltageBoostPercent = 0xB9306D9B;
        private const uint IdPowerPoliciesGetInfo = 0x34206D86;
        private const uint IdPowerPoliciesGetStatus = 0x70916171;
        private const uint IdPowerPoliciesSetStatus = 0xAD95F5ED;
        private const uint IdThermalPoliciesGetInfo = 0x0D258BB5;
        private const uint IdThermalPoliciesGetStatus = 0xE9C425A1;
        private const uint IdThermalPoliciesSetStatus = 0x34C0B13D;
        private const uint IdFanCoolersGetInfo = 0xFB85B01E;
        private const uint IdFanCoolersGetControl = 0x814B209F;
        private const uint IdFanCoolersGetStatus = 0x35AED5E8;
        private const uint IdFanCoolersSetControl = 0xA58971A5;

        // 时钟域 (NV_GPU_PUBLIC_CLOCK)
        public const int ClockDomainGraphics = 0;
        public const int ClockDomainMemory = 4;

        // 曲线锁定模式 (NV_GPU_CLOCK_LOCK_MODE)
        public const int LockModeNoLock = 0;
        public const int LockModeManual = 3;

        public const int FanControlAuto = 0;
        public const int FanControlManual = 1;

        private const int MaxPhysicalGPUs = 64;
        private const int ClockBoostRangeCount = 32;
        private const int ClockBoostLockCount = 32;
        private const int PowerPolicyEntryCount = 4;
        private const int ThermalPolicyEntryCount = 4;
        private const int FanCoolerCount = 32;

        #region 原生入口

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr nvapi_QueryInterface(uint functionId);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int InitializeDelegate();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int EnumPhysicalGPUsDelegate([Out] IntPtr[] handles, out uint count);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int ClockBoostRangesDelegate(IntPtr gpu, ref ClockBoostRanges ranges);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int ClockBoostMasksDelegate(IntPtr gpu, ref ClockBoostMasks masks);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int ClockBoostTableDelegate(IntPtr gpu, ref ClockBoostTable table);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int ClockVfStatusDelegate(IntPtr gpu, ref ClockVfStatus status);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int ClockBoostLockDelegate(IntPtr gpu, ref ClockBoostLock locks);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int VoltageBoostPercentDelegate(IntPtr gpu, ref VoltageBoostPercent percent);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int PowerPoliciesInfoDelegate(IntPtr gpu, ref PowerPoliciesInfo info);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int PowerPoliciesStatusDelegate(IntPtr gpu, ref PowerPoliciesStatus status);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int ThermalPoliciesInfoDelegate(IntPtr gpu, ref ThermalPoliciesInfo info);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int ThermalPoliciesStatusDelegate(IntPtr gpu, ref ThermalPoliciesStatus status);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int FanCoolersInfoDelegate(IntPtr gpu, ref FanCoolersInfo info);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int FanCoolersControlDelegate(IntPtr gpu, ref FanCoolersControl control);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int FanCoolersStatusDelegate(IntPtr gpu, ref FanCoolersStatus status);

        private static readonly object InitLock = new();
        private static bool _initialized;
        private static readonly Dictionary<uint, Delegate> DelegateCache = new();

        private static T GetDelegate<T>(uint id) where T : Delegate
        {
            lock (DelegateCache)
            {
                if (DelegateCache.TryGetValue(id, out var cached))
                    return (T)cached;
                var ptr = nvapi_QueryInterface(id);
                if (ptr == IntPtr.Zero)
                    throw new NotSupportedException($"NVAPI 函数 0x{id:X8} 不可用");
                var del = Marshal.GetDelegateForFunctionPointer(ptr, typeof(T));
                DelegateCache[id] = del;
                return (T)del;
            }
        }

        private static void EnsureInitialized()
        {
            if (_initialized) return;
            lock (InitLock)
            {
                if (_initialized) return;
                var init = GetDelegate<InitializeDelegate>(IdInitialize);
                Check(init());
                _initialized = true;
            }
        }

        private static void Check(int status)
        {
            if (status != 0)
                throw new NvApiStatusException(status);
        }

        public static IntPtr GetGpuHandle(int index)
        {
            EnsureInitialized();
            var enumGpus = GetDelegate<EnumPhysicalGPUsDelegate>(IdEnumPhysicalGPUs);
            var handles = new IntPtr[MaxPhysicalGPUs];
            Check(enumGpus(handles, out uint count));
            if (count == 0)
                throw new InvalidOperationException("没有找到 NVIDIA GPU");
            if (index < 0 || index >= count)
                index = 0;
            return handles[index];
        }

        #endregion

        #region 结构体（布局与 NvAPIWrapper 一致，pack=8）

        public sealed class NvApiStatusException : Exception
        {
            public int Status { get; }

            public NvApiStatusException(int status)
                : base($"NVAPI 调用失败: {DescribeStatus(status)} (0x{status:X8})")
            {
                Status = status;
            }

            internal static string DescribeStatus(int status) => status switch
            {
                0 => "OK",
                -1 => "通用错误",
                -2 => "库未找到",
                -3 => "驱动未实现该接口",
                -4 => "NVAPI 未初始化",
                -5 => "参数无效",
                -6 => "未找到 NVIDIA 设备",
                -8 => "句柄无效",
                -9 => "结构体版本不兼容",
                -15 => "需要物理 GPU 句柄",
                -17 => "无效组合",
                -18 => "不支持（可能被 OEM/驱动锁定）",
                -22 => "驱动不兼容",
                -23 => "超时",
                -24 => "缓冲区不足",
                _ => $"状态码 {status}"
            };
        }

        private static uint MakeVersion(int structSize, int version) => (uint)(structSize | (version << 16));

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        public struct VfControlEntry
        {
            public int FrequencyOffsetKHz;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)] public uint[] Reserved;
        }

        /// <summary>
        /// V/F 曲线偏移表 (0x23F1B133/0x0733E009), 9248 字节。
        /// 布局与 LACT #936 在 RTX 5090 (driver 590) 实测协议一致:
        /// 0x00 version | 0x04 mask(128bit, 每次调用仅允许一位置 1) | 0x20 起 128 个 72 字节条目 (频率偏移 kHz 在 +0x00)。
        /// </summary>
        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        public struct ClockBoostTable
        {
            public uint Version;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public uint[] Mask;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)] public uint[] HeaderReserved;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)] public VfControlEntry[] Entries;

            public static ClockBoostTable Allocate()
            {
                var table = new ClockBoostTable
                {
                    Version = MakeVersion(Marshal.SizeOf(typeof(ClockBoostTable)), 1),
                    Mask = new uint[4],
                    HeaderReserved = new uint[3],
                    Entries = new VfControlEntry[128]
                };
                for (int i = 0; i < table.Entries.Length; i++)
                    table.Entries[i].Reserved = new uint[17];
                return table;
            }

            public ClockBoostTable Clone()
            {
                var copy = Allocate();
                Array.Copy(Mask, copy.Mask, Mask.Length);
                Array.Copy(HeaderReserved, copy.HeaderReserved, HeaderReserved.Length);
                for (int i = 0; i < Entries.Length; i++)
                {
                    copy.Entries[i].FrequencyOffsetKHz = Entries[i].FrequencyOffsetKHz;
                    Array.Copy(Entries[i].Reserved, copy.Entries[i].Reserved, 17);
                }
                return copy;
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        public struct ClockBoostMaskEntry
        {
            public uint Unknown1;
            public uint Unknown2;
            public uint Unknown3;
            public uint Unknown4;
            public int MemoryDelta;
            public int GpuDelta;
        }

        /// <summary>V/F 曲线点掩码表: Masks 位图的 bit i 对应 GpuDeltas[i] 是否生效。</summary>
        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        public struct ClockBoostMasks
        {
            public uint Version;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public uint[] Masks;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)] public uint[] Unknown1;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 103)] public ClockBoostMaskEntry[] Entries;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 916)] public uint[] Unknown2;

            public static ClockBoostMasks Allocate()
            {
                return new ClockBoostMasks
                {
                    Version = MakeVersion(Marshal.SizeOf(typeof(ClockBoostMasks)), 1),
                    Masks = new uint[4],
                    Unknown1 = new uint[8],
                    Entries = new ClockBoostMaskEntry[103],
                    Unknown2 = new uint[916]
                };
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        public struct VfStatusEntry
        {
            public uint FrequencyKHz;
            public uint VoltageMicroV;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)] public uint[] Reserved;
        }

        /// <summary>V/F 曲线读取 (0x21537AD4), 7208 字节: 0x48 起 128 个 28 字节条目 (频率 kHz/电压 µV)。</summary>
        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        public struct ClockVfStatus
        {
            public uint Version;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public uint[] Mask;
            public uint RequestField;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 12)] public uint[] Reserved;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)] public VfStatusEntry[] Entries;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 888)] public uint[] Trailing;

            public static ClockVfStatus Allocate()
            {
                var status = new ClockVfStatus
                {
                    Version = MakeVersion(Marshal.SizeOf(typeof(ClockVfStatus)), 1),
                    Mask = new uint[4],
                    RequestField = 15,
                    Reserved = new uint[12],
                    Entries = new VfStatusEntry[128],
                    Trailing = new uint[888]
                };
                for (int i = 0; i < 4; i++)
                    status.Mask[i] = 0xFFFFFFFF;
                for (int i = 0; i < status.Entries.Length; i++)
                    status.Entries[i].Reserved = new uint[5];
                return status;
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        public struct ClockBoostRangeEntry
        {
            public uint Unknown1;
            public int ClockType;
            public uint Unknown2;
            public uint Unknown3;
            public uint Unknown4;
            public uint Unknown5;
            public uint Unknown6;
            public uint Unknown7;
            public uint Unknown8;
            public uint Unknown9;
            public int RangeMaximumInkHz;
            public int RangeMinimumInkHz;
            public int MaximumTemperature;
            public uint Unknown10;
            public uint Unknown11;
            public uint Unknown12;
            public uint Unknown13;
            public uint Unknown14;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        public struct ClockBoostRanges
        {
            public uint Version;
            public uint Count;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)] public uint[] Unknown;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = ClockBoostRangeCount)] public ClockBoostRangeEntry[] Entries;

            public static ClockBoostRanges Allocate()
            {
                return new ClockBoostRanges
                {
                    Version = MakeVersion(Marshal.SizeOf(typeof(ClockBoostRanges)), 1),
                    Unknown = new uint[8],
                    Entries = new ClockBoostRangeEntry[ClockBoostRangeCount]
                };
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        public struct ClockBoostLockEntry
        {
            public int ClockDomain;
            public uint Unknown1;
            public int LockMode;
            public uint Unknown2;
            public uint FreqKhzOrVoltageMicroV;
            public uint Unknown3;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        public struct ClockBoostLock
        {
            public uint Version;
            public uint Unknown;
            public uint Count;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = ClockBoostLockCount)] public ClockBoostLockEntry[] Entries;

            public static ClockBoostLock Allocate()
            {
                return new ClockBoostLock
                {
                    Version = MakeVersion(Marshal.SizeOf(typeof(ClockBoostLock)), 2),
                    Entries = new ClockBoostLockEntry[ClockBoostLockCount]
                };
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        public struct VoltageBoostPercent
        {
            public uint Version;
            public uint Percent;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)] public uint[] Unknown;

            public static VoltageBoostPercent Allocate()
            {
                return new VoltageBoostPercent
                {
                    Version = MakeVersion(Marshal.SizeOf(typeof(VoltageBoostPercent)), 1),
                    Unknown = new uint[8]
                };
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        public struct PowerPolicyInfoEntry
        {
            public int StateId;
            public uint Unknown1;
            public uint Unknown2;
            public uint MinimumPower;
            public uint Unknown3;
            public uint Unknown4;
            public uint DefaultPower;
            public uint Unknown5;
            public uint Unknown6;
            public uint MaximumPower;
            public uint Unknown7;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        public struct PowerPoliciesInfo
        {
            public uint Version;
            public byte Valid;
            public byte EntryCount;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = PowerPolicyEntryCount)] public PowerPolicyInfoEntry[] Entries;

            public static PowerPoliciesInfo Allocate()
            {
                return new PowerPoliciesInfo
                {
                    Version = MakeVersion(Marshal.SizeOf(typeof(PowerPoliciesInfo)), 1),
                    Entries = new PowerPolicyInfoEntry[PowerPolicyEntryCount]
                };
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        public struct PowerPolicyStatusEntry
        {
            public int StateId;
            public uint Unknown1;
            public uint PowerTarget;
            public uint Unknown2;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        public struct PowerPoliciesStatus
        {
            public uint Version;
            public uint EntryCount;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = PowerPolicyEntryCount)] public PowerPolicyStatusEntry[] Entries;

            public static PowerPoliciesStatus Allocate()
            {
                return new PowerPoliciesStatus
                {
                    Version = MakeVersion(Marshal.SizeOf(typeof(PowerPoliciesStatus)), 1),
                    Entries = new PowerPolicyStatusEntry[PowerPolicyEntryCount]
                };
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        public struct ThermalPoliciesInfoEntry
        {
            public int Controller;
            public uint Unknown1;
            public int MinimumTemperature;
            public int DefaultTemperature;
            public int MaximumTemperature;
            public uint Unknown2;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        public struct ThermalPoliciesInfo
        {
            public uint Version;
            public byte EntryCount;
            public byte Unknown;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = ThermalPolicyEntryCount)] public ThermalPoliciesInfoEntry[] Entries;

            public static ThermalPoliciesInfo Allocate()
            {
                return new ThermalPoliciesInfo
                {
                    Version = MakeVersion(Marshal.SizeOf(typeof(ThermalPoliciesInfo)), 2),
                    Entries = new ThermalPoliciesInfoEntry[ThermalPolicyEntryCount]
                };
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        public struct ThermalPoliciesStatusEntry
        {
            public int Controller;
            public int TargetTemperature;
            public int StateId;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        public struct ThermalPoliciesStatus
        {
            public uint Version;
            public uint EntryCount;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = ThermalPolicyEntryCount)] public ThermalPoliciesStatusEntry[] Entries;

            public static ThermalPoliciesStatus Allocate()
            {
                return new ThermalPoliciesStatus
                {
                    Version = MakeVersion(Marshal.SizeOf(typeof(ThermalPoliciesStatus)), 2),
                    Entries = new ThermalPoliciesStatusEntry[ThermalPolicyEntryCount]
                };
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        public struct FanCoolersInfoEntry
        {
            public uint CoolerId;
            public uint Unknown1;
            public uint Unknown2;
            public uint MaximumRpm;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)] public uint[] Reserved;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        public struct FanCoolersInfo
        {
            public uint Version;
            public uint Unknown;
            public uint EntryCount;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)] public uint[] Reserved;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = FanCoolerCount)] public FanCoolersInfoEntry[] Entries;

            public static FanCoolersInfo Allocate()
            {
                return new FanCoolersInfo
                {
                    Version = MakeVersion(Marshal.SizeOf(typeof(FanCoolersInfo)), 1),
                    Reserved = new uint[8],
                    Entries = new FanCoolersInfoEntry[FanCoolerCount]
                };
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        public struct FanCoolersControlEntry
        {
            public uint CoolerId;
            public uint Level;
            public int ControlMode;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)] public uint[] Reserved;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        public struct FanCoolersControl
        {
            public uint Version;
            public uint Unknown;
            public uint EntryCount;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)] public uint[] Reserved;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = FanCoolerCount)] public FanCoolersControlEntry[] Entries;

            public static FanCoolersControl Allocate()
            {
                return new FanCoolersControl
                {
                    Version = MakeVersion(Marshal.SizeOf(typeof(FanCoolersControl)), 1),
                    Reserved = new uint[8],
                    Entries = new FanCoolersControlEntry[FanCoolerCount]
                };
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        public struct FanCoolersStatusEntry
        {
            public uint CoolerId;
            public uint CurrentRpm;
            public uint CurrentMinimumLevel;
            public uint CurrentMaximumLevel;
            public uint CurrentLevel;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)] public uint[] Reserved;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        public struct FanCoolersStatus
        {
            public uint Version;
            public uint EntryCount;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)] public uint[] Reserved;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = FanCoolerCount)] public FanCoolersStatusEntry[] Entries;

            public static FanCoolersStatus Allocate()
            {
                return new FanCoolersStatus
                {
                    Version = MakeVersion(Marshal.SizeOf(typeof(FanCoolersStatus)), 1),
                    Reserved = new uint[8],
                    Entries = new FanCoolersStatusEntry[FanCoolerCount]
                };
            }
        }

        #endregion

        #region 高层操作

        public sealed class ClockOffsetRange
        {
            public int CoreMinMhz;
            public int CoreMaxMhz;
            public int MemoryMinMhz;
            public int MemoryMaxMhz;
        }

        public sealed class ClockOffsets
        {
            public int CoreMhz;
            public int MemoryMhz;
        }

        public sealed class PowerPolicySnapshot
        {
            public int CurrentWatts;
            public int MinWatts;
            public int DefaultWatts;
            public int MaxWatts;
        }

        public sealed class ThermalPolicySnapshot
        {
            public int CurrentTemp;
            public int MinTemp;
            public int DefaultTemp;
            public int MaxTemp;
        }

        public sealed class FanControlSnapshot
        {
            public int CoolerCount;
            public int CoolerId;
            public int ControlMode;
            public int Level;
            public int Rpm;
            public int MaxRpm;
        }

        public static ClockOffsets GetClockOffsets(IntPtr gpu)
        {
            var table = ReadTable(gpu);
            int core = 0;
            for (int i = 0; i < table.Entries.Length; i++)
            {
                if (table.Entries[i].FrequencyOffsetKHz != 0)
                {
                    core = table.Entries[i].FrequencyOffsetKHz / 1000;
                    break;
                }
            }
            return new ClockOffsets { CoreMhz = core, MemoryMhz = 0 };
        }

        public static ClockOffsetRange GetClockOffsetRange(IntPtr gpu)
        {
            var ranges = ReadRanges(gpu);
            var result = new ClockOffsetRange();
            int count = (int)Math.Min(ranges.Count, ClockBoostRangeCount);
            for (int i = 0; i < count; i++)
            {
                var entry = ranges.Entries[i];
                if (entry.RangeMaximumInkHz == 0 && entry.RangeMinimumInkHz == 0)
                    continue;
                // 条目 ClockType 字段为时钟域: 0=核心, 4=显存 (真机探测: 核心±1000MHz, 显存-1000~+3000MHz)
                if (entry.ClockType == ClockDomainGraphics)
                {
                    result.CoreMaxMhz = entry.RangeMaximumInkHz / 1000;
                    result.CoreMinMhz = entry.RangeMinimumInkHz / 1000;
                }
                else if (entry.ClockType == ClockDomainMemory)
                {
                    result.MemoryMaxMhz = entry.RangeMaximumInkHz / 1000;
                    result.MemoryMinMhz = entry.RangeMinimumInkHz / 1000;
                }
            }
            return result;
        }

        /// <summary>
        /// 设置核心 V/F 曲线偏移。驱动要求每次调用 mask 仅一位置 1 (LACT #936 在 RTX 5090 实测协议)，
        /// 因此逐点提交。显存偏移在现驱动上不走该表，传非 0 会抛 NotSupportedException。
        /// </summary>
        public static void SetClockOffsets(IntPtr gpu, int coreDeltaMhz, int memoryDeltaMhz)
        {
            if (memoryDeltaMhz != 0)
                throw new NotSupportedException("当前驱动不支持通过该接口设置显存频率偏移 (可使用锁频功能替代)");

            // 重置时若当前本就无偏移, 跳过逐点写入
            if (coreDeltaMhz == 0 && GetClockOffsets(gpu).CoreMhz == 0)
                return;

            var points = GetActiveCurvePoints(gpu);
            int khz = coreDeltaMhz * 1000;

            // 金丝雀点: 先写一个点读回验证。部分 OEM 驱动会静默忽略偏移写入,
            // 此时立即中止, 避免上百次无效调用阻塞 UI 线程
            int canary = points[points.Length / 2];
            int before = GetCurvePointFrequencyMhz(gpu, canary);
            SetClockPointOffset(gpu, canary, khz);
            int after = GetCurvePointFrequencyMhz(gpu, canary);
            if (Math.Abs((after - before) - coreDeltaMhz) > 40)
                throw new NotSupportedException("驱动未接受频率偏移——本机驱动可能已锁定超频 (OEM 限制)");

            int failed = 0;
            foreach (int point in points)
            {
                try
                {
                    SetClockPointOffset(gpu, point, khz);
                }
                catch (NvApiStatusException)
                {
                    // 个别曲线点 (如省电拐点) 可能被驱动拒绝, 跳过继续
                    failed++;
                }
            }
            if (failed >= points.Length)
                throw new NvApiStatusException(-1);
        }

        public static int[] GetActiveCurvePoints(IntPtr gpu)
        {
            try
            {
                var status = ReadClockVfStatus(gpu);
                var points = new List<int>();
                for (int i = 0; i < status.Entries.Length; i++)
                    if (status.Entries[i].FrequencyKHz > 0)
                        points.Add(i);
                if (points.Count > 0)
                    return points.ToArray();
            }
            catch (NvApiStatusException)
            {
                // 曲线读取不可用时退化为全部点
            }
            return Enumerable.Range(0, 128).ToArray();
        }

        public static void SetClockPointOffset(IntPtr gpu, int point, int offsetKHz)
        {
            if (point < 0 || point >= 128)
                throw new ArgumentOutOfRangeException(nameof(point));
            var table = ClockBoostTable.Allocate();
            table.Mask[point >> 5] = 1u << (point & 31);
            table.Entries[point].FrequencyOffsetKHz = offsetKHz;
            WriteTable(gpu, ref table);
        }

        public static ClockVfStatus ReadClockVfStatus(IntPtr gpu)
        {
            var status = ClockVfStatus.Allocate();
            Check(GetDelegate<ClockVfStatusDelegate>(IdGetClockVfStatus)(gpu, ref status));
            return status;
        }

        public static int GetCurvePointFrequencyMhz(IntPtr gpu, int point)
        {
            var status = ReadClockVfStatus(gpu);
            return (int)(status.Entries[point].FrequencyKHz / 1000);
        }

        public static ClockBoostTable ReadTable(IntPtr gpu)
        {
            var table = ClockBoostTable.Allocate();
            Check(GetDelegate<ClockBoostTableDelegate>(IdGetClockBoostTable)(gpu, ref table));
            return table;
        }

        /// <summary>带表头域字段读取 (HeaderReserved[0]=0 核心, 探测值 4 疑似显存域)。</summary>
        public static ClockBoostTable ReadTable(IntPtr gpu, uint domain)
        {
            var table = ClockBoostTable.Allocate();
            table.HeaderReserved[0] = domain;
            Check(GetDelegate<ClockBoostTableDelegate>(IdGetClockBoostTable)(gpu, ref table));
            return table;
        }

        /// <summary>直接写回整张 V/F 偏移表（可用于逐点曲线编辑或恢复原始表）。</summary>
        public static void WriteTable(IntPtr gpu, ref ClockBoostTable table)
        {
            Check(GetDelegate<ClockBoostTableDelegate>(IdSetClockBoostTable)(gpu, ref table));
        }

        public static ClockBoostRanges ReadRanges(IntPtr gpu)
        {
            var ranges = ClockBoostRanges.Allocate();
            Check(GetDelegate<ClockBoostRangesDelegate>(IdGetClockBoostRanges)(gpu, ref ranges));
            return ranges;
        }

        public static ClockBoostMasks ReadClockBoostMasks(IntPtr gpu)
        {
            var masks = ClockBoostMasks.Allocate();
            Check(GetDelegate<ClockBoostMasksDelegate>(IdGetClockBoostMask)(gpu, ref masks));
            return masks;
        }

        public static ClockBoostLock ReadClockBoostLock(IntPtr gpu)
        {
            var locks = ClockBoostLock.Allocate();
            Check(GetDelegate<ClockBoostLockDelegate>(IdGetClockBoostLock)(gpu, ref locks));
            return locks;
        }

        public static void SetClockBoostLock(IntPtr gpu, int clockDomain, int lockMode, uint freqKhzOrVoltageMicroV)
        {
            var locks = ReadClockBoostLock(gpu);
            locks.Entries[0].ClockDomain = clockDomain;
            locks.Entries[0].LockMode = lockMode;
            locks.Entries[0].FreqKhzOrVoltageMicroV = freqKhzOrVoltageMicroV;
            locks.Count = Math.Max(locks.Count, 1);
            Check(GetDelegate<ClockBoostLockDelegate>(IdSetClockBoostLock)(gpu, ref locks));
        }

        public static int GetVoltageBoostPercent(IntPtr gpu)
        {
            var percent = VoltageBoostPercent.Allocate();
            Check(GetDelegate<VoltageBoostPercentDelegate>(IdGetCoreVoltageBoostPercent)(gpu, ref percent));
            return unchecked((int)percent.Percent);
        }

        public static void SetVoltageBoostPercent(IntPtr gpu, int percent)
        {
            var value = VoltageBoostPercent.Allocate();
            Check(GetDelegate<VoltageBoostPercentDelegate>(IdGetCoreVoltageBoostPercent)(gpu, ref value));
            value.Percent = unchecked((uint)Math.Clamp(percent, 0, 100));
            Check(GetDelegate<VoltageBoostPercentDelegate>(IdSetCoreVoltageBoostPercent)(gpu, ref value));
        }

        public static PowerPoliciesInfo ReadPowerPoliciesInfo(IntPtr gpu)
        {
            var info = PowerPoliciesInfo.Allocate();
            Check(GetDelegate<PowerPoliciesInfoDelegate>(IdPowerPoliciesGetInfo)(gpu, ref info));
            return info;
        }

        public static PowerPoliciesStatus ReadPowerPoliciesStatus(IntPtr gpu)
        {
            var status = PowerPoliciesStatus.Allocate();
            Check(GetDelegate<PowerPoliciesStatusDelegate>(IdPowerPoliciesGetStatus)(gpu, ref status));
            return status;
        }

        public static PowerPolicySnapshot GetPowerPolicy(IntPtr gpu)
        {
            var info = ReadPowerPoliciesInfo(gpu);
            var status = ReadPowerPoliciesStatus(gpu);

            if (info.EntryCount == 0 || status.EntryCount == 0)
                throw new NotSupportedException("驱动未返回功耗策略信息");

            var infoEntry = info.Entries[0];
            var statusEntry = status.Entries[0];
            return new PowerPolicySnapshot
            {
                CurrentWatts = (int)(statusEntry.PowerTarget / 1000),
                MinWatts = (int)(infoEntry.MinimumPower / 1000),
                DefaultWatts = (int)(infoEntry.DefaultPower / 1000),
                MaxWatts = (int)(infoEntry.MaximumPower / 1000)
            };
        }

        public static void SetPowerPolicy(IntPtr gpu, int watts)
        {
            var snapshot = GetPowerPolicy(gpu);
            int clamped = Math.Clamp(watts, snapshot.MinWatts, snapshot.MaxWatts) * 1000;

            var status = PowerPoliciesStatus.Allocate();
            Check(GetDelegate<PowerPoliciesStatusDelegate>(IdPowerPoliciesGetStatus)(gpu, ref status));
            status.Entries[0].PowerTarget = unchecked((uint)clamped);
            Check(GetDelegate<PowerPoliciesStatusDelegate>(IdPowerPoliciesSetStatus)(gpu, ref status));
        }

        public static ThermalPolicySnapshot GetThermalPolicy(IntPtr gpu)
        {
            var info = ThermalPoliciesInfo.Allocate();
            Check(GetDelegate<ThermalPoliciesInfoDelegate>(IdThermalPoliciesGetInfo)(gpu, ref info));
            var status = ThermalPoliciesStatus.Allocate();
            Check(GetDelegate<ThermalPoliciesStatusDelegate>(IdThermalPoliciesGetStatus)(gpu, ref status));

            if (info.EntryCount == 0 || status.EntryCount == 0)
                throw new NotSupportedException("驱动未返回温度策略信息");

            var infoEntry = info.Entries[0];
            var statusEntry = status.Entries[0];
            // 温度字段为 1/256 ℃ 定点数 (真机探测: 87℃ 存为 22272)
            return new ThermalPolicySnapshot
            {
                CurrentTemp = FixedPointToCelsius(statusEntry.TargetTemperature),
                MinTemp = FixedPointToCelsius(infoEntry.MinimumTemperature),
                DefaultTemp = FixedPointToCelsius(infoEntry.DefaultTemperature),
                MaxTemp = FixedPointToCelsius(infoEntry.MaximumTemperature)
            };
        }

        public static void SetThermalPolicy(IntPtr gpu, int tempCelsius)
        {
            var snapshot = GetThermalPolicy(gpu);
            int clamped = Math.Clamp(tempCelsius, snapshot.MinTemp, snapshot.MaxTemp);

            var status = ThermalPoliciesStatus.Allocate();
            Check(GetDelegate<ThermalPoliciesStatusDelegate>(IdThermalPoliciesGetStatus)(gpu, ref status));
            status.Entries[0].TargetTemperature = clamped << 8;
            Check(GetDelegate<ThermalPoliciesStatusDelegate>(IdThermalPoliciesSetStatus)(gpu, ref status));
        }

        private static int FixedPointToCelsius(int fixedPoint) => (int)Math.Round(fixedPoint / 256.0);

        public static FanControlSnapshot GetFanControl(IntPtr gpu)
        {
            var info = FanCoolersInfo.Allocate();
            Check(GetDelegate<FanCoolersInfoDelegate>(IdFanCoolersGetInfo)(gpu, ref info));
            var control = FanCoolersControl.Allocate();
            Check(GetDelegate<FanCoolersControlDelegate>(IdFanCoolersGetControl)(gpu, ref control));
            var status = FanCoolersStatus.Allocate();
            Check(GetDelegate<FanCoolersStatusDelegate>(IdFanCoolersGetStatus)(gpu, ref status));

            var result = new FanControlSnapshot
            {
                CoolerCount = (int)Math.Min(info.EntryCount, status.EntryCount)
            };
            if (result.CoolerCount > 0)
            {
                result.CoolerId = (int)status.Entries[0].CoolerId;
                result.ControlMode = control.Entries[0].ControlMode;
                result.Level = (int)status.Entries[0].CurrentLevel;
                result.Rpm = (int)status.Entries[0].CurrentRpm;
                result.MaxRpm = (int)info.Entries[0].MaximumRpm;
            }
            return result;
        }

        public static void SetFanControl(IntPtr gpu, int levelPercent)
        {
            var control = FanCoolersControl.Allocate();
            Check(GetDelegate<FanCoolersControlDelegate>(IdFanCoolersGetControl)(gpu, ref control));
            control.Entries[0].CoolerId = 0;
            control.Entries[0].Level = unchecked((uint)Math.Clamp(levelPercent, 0, 100));
            control.Entries[0].ControlMode = levelPercent < 0 ? FanControlAuto : FanControlManual;
            control.EntryCount = Math.Max(control.EntryCount, 1);
            Check(GetDelegate<FanCoolersControlDelegate>(IdFanCoolersSetControl)(gpu, ref control));
        }

        #endregion
    }
}
