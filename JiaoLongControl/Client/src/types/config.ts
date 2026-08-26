// 与 C# JiaoLongConfig.cs 保持同步
// 新增字段：在 C# 类中加属性，在此 interface 中加对应字段

export interface FanPoint {
  temp: number
  speed: number
}

export interface AppSectionType {
  BootMinimized: boolean
  BootAdvancedFanControlSystem: boolean
  BootAdvancedCPUSystem: boolean
  BootAdvancedGPUSystem: boolean
  BootSetRyzenSumCurveOptimizerAll: boolean
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
  PowerLimit: number
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
  TdcLimitMp1: number
  TdcLimitRsmu: number
  EdcLimitMp1: number
  EdcLimitRsmu: number
  TempLimitMp1: number
  TempLimitRsmu: number
  PboScalar: number
  OcClk: number
  OcVolt: number
  CurveOptimizerAll: number
}

export interface JiaoLongConfigType {
  Version: string
  App: AppSectionType
  Cpu: CpuSectionType
  Gpu: GpuSectionType
  Fan: FanSectionType
  Smu: SmuSectionType
}
