/** 业务常量: 硬件限值与轮询间隔 (与 Server 端 C#/Python 约定保持一致) */

/** 风扇手动转速区间 (RPM) */
export const FAN_MAX_RPM = 5800
export const FAN_MIN_RPM = 1500

/** 遥测轮询间隔 (ms) */
export const POLL_INTERVAL_SYSTEM_INFO = 5000
export const POLL_INTERVAL_SMU = 3000
export const POLL_INTERVAL_FAN_SPEED = 2000
