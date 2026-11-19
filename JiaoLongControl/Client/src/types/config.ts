// 与 C# JiaoLongConfig.cs 保持同步
// 新增字段：在 C# 类中加属性，在此 interface 中加对应字段

export interface FanPoint {
  temp: number
  speed: number
}

export type ThemeMode = 'light' | 'dark' | 'system'

export interface AppSectionType {
  BootMinimized: boolean
  BootAdvancedFanControlSystem: boolean
  BootAdvancedCPUSystem: boolean
  BootAdvancedGPUSystem: boolean
  BootSetRyzenSmuCurveOptimizerAll: boolean
  BootSetRyzenSmuCurveOptimizerPerCore: boolean
  BootKeyboardGradient: boolean
  CpuCurveOptimizerPerCore: boolean
  BootGpuUseAdvancedOffsets: boolean
  Theme: ThemeMode
  /** 主窗口记忆宽度 (DIP), 0 = 未记忆, 关闭时写入当前窗口大小 */
  WindowWidth: number
  /** 主窗口记忆高度 (DIP), 0 = 未记忆, 关闭时写入当前窗口大小 */
  WindowHeight: number
}

export interface CpuProfileDataType {
  CpuLongPower: number
  CpuShortPower: number
  CpuTempWall: number
  CpuMaxFrequency: number
  CpuTurbo: boolean
}

export interface CpuSectionType {
  CpuProfile: string
  Default: CpuProfileDataType
  Performance: CpuProfileDataType
  Saving: CpuProfileDataType
  Custom: CpuProfileDataType
}

export interface GpuSectionType {
  GpuClock: number
  MemoryClock: number
  CoreClockOffsetMhz: number
  MemoryClockOffsetMhz: number
}

export interface FanSectionType {
  FanCurveMerge: boolean
  ManualFanSpeed: number
  CpuFanCurve: FanPoint[]
  GpuFanCurve: FanPoint[]
}

export interface SmuSectionType {
  StapmLimit: number
  StapmTime: number
  FastLimit: number
  SlowLimit: number
  SlowTime: number
  PptLimitRsmu: number
  VrmCurrentMp1: number
  VrmCurrentRsmu: number
  EdcLimitMp1: number
  EdcLimitRsmu: number
  TempLimitMp1: number
  TempLimitRsmu: number
  PboScalar: number
  OcClk: number
  OcVolt: number
  CurveOptimizerAll: number
  CurveOptimizerPerCore: number[]
  BootApplyItems: string[]
}

export type OsdPosition = 'TopCenter' | 'BottomCenter' | 'Custom'

export interface OsdSectionType {
  Enabled: boolean
  ShowVolume: boolean
  ShowLockKeys: boolean
  ShowKeyboardBacklight: boolean
  ShowPerformanceMode: boolean
  ShowPerfTelemetry: boolean
  ShowFnLock: boolean
  ShowTouchpad: boolean
  ShowMedia: boolean
  SuppressNativeVolumeOsd: boolean
  AnimationMs: number
  DurationMs: number
  PollIntervalMs: number
  Position: OsdPosition
  CustomX: number
  CustomY: number
  Opacity: number
  Scale: number
}

export interface JiaoLongConfigType {
  Version: string
  App: AppSectionType
  Cpu: CpuSectionType
  Gpu: GpuSectionType
  Fan: FanSectionType
  Smu: SmuSectionType
  Osd: OsdSectionType
}
