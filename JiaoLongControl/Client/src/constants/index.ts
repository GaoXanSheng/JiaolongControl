/** 业务常量: 硬件限值与轮询间隔 (与 Server 端 C# 配置保持一致) */

import type {CpuProfileDataType} from '@/types/config'

/** 风扇手动转速区间 (RPM) */
export const FAN_MAX_RPM = 5800
export const FAN_MIN_RPM = 1500

/** 遥测轮询间隔 (ms) */
export const POLL_INTERVAL_SYSTEM_INFO = 5000
export const POLL_INTERVAL_SMU = 3000
export const POLL_INTERVAL_FAN_SPEED = 2000

/** 各档位出厂默认参数 (与 Server/Core/Models/JiaoLongConfig.cs 的 CpuProfileData 默认值保持一致) */
export const CPU_PROFILE_DEFAULTS: Record<
  'Default' | 'Performance' | 'Saving' | 'Custom',
  CpuProfileDataType
> = {
  Default: { CpuLongPower: 45, CpuShortPower: 65, CpuTempWall: 90, CpuMaxFrequency: 4400, CpuTurbo: true },
  Performance: { CpuLongPower: 65, CpuShortPower: 90, CpuTempWall: 95, CpuMaxFrequency: 4700, CpuTurbo: true },
  Saving: { CpuLongPower: 30, CpuShortPower: 45, CpuTempWall: 85, CpuMaxFrequency: 3800, CpuTurbo: true },
  Custom: { CpuLongPower: 45, CpuShortPower: 55, CpuTempWall: 95, CpuMaxFrequency: 4800, CpuTurbo: true },
}
