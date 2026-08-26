/** 业务常量: 硬件限值与轮询间隔 (与 Server 端 C#/Python 约定保持一致) */

import type { CpuProfileDataType } from '@/types/config'

/** 风扇手动转速区间 (RPM) */
export const FAN_MAX_RPM = 5800
export const FAN_MIN_RPM = 1500

/** 遥测轮询间隔 (ms) */
export const POLL_INTERVAL_SYSTEM_INFO = 5000
export const POLL_INTERVAL_SMU = 3000
export const POLL_INTERVAL_FAN_SPEED = 2000

/** 各档位出厂默认参数 (与 Server system_info._default_config 保持一致) */
export const CPU_PROFILE_DEFAULTS: Record<
  'Default' | 'Performance' | 'Saving' | 'Custom',
  CpuProfileDataType
> = {
  Default: { CpuLongPower: 45, CpuShortPower: 60, CpuTempWall: 85, CpuMaxFrequency: 5150, CpuTurbo: true },
  Performance: { CpuLongPower: 54, CpuShortPower: 75, CpuTempWall: 90, CpuMaxFrequency: 5150, CpuTurbo: true },
  Saving: { CpuLongPower: 30, CpuShortPower: 45, CpuTempWall: 75, CpuMaxFrequency: 3200, CpuTurbo: true },
  Custom: { CpuLongPower: 45, CpuShortPower: 60, CpuTempWall: 85, CpuMaxFrequency: 5150, CpuTurbo: true },
}
