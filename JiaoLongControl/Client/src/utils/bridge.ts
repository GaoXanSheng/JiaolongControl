export enum GPUMode {
  HybridMode = 0,
  DiscreteMode = 1,
}

export enum ResultState {
  OFF = 0,
  ON = 1,
}

export type CommandResult<T = void> = {
  Success: boolean
  Message: string
} & (T extends void ? Record<never, never> : { Data: T })

export enum SystemPerMode {
  PerformanceMode = 0,
  QuietMode = 1,
  BalanceMode = 2,
  CustomMode = 3,
}

export enum RGBKeyboardMode {
  Mode_Off = 0,
  Mode_RGBFixedMode = 2,
}

export enum RGBKeyboardBrightnessLevel {
  Level_0 = 0,
  Level_1 = 1,
  Level_2 = 2,
  Level_3 = 3,
}

export interface FanSpeedInfo {
  CPUFanSpeed: number
  GPUFanSpeed: number
}

export interface ColorInfo {
  red: number
  green: number
  blue: number
}

export interface SystemOverview {
  CpuName: string
  GpuName: string
  OsVersion: string
  MemoryInfo: string
}

export interface GpuStats {
  GpuName: string
  DriverVersion: string
  MemoryTotal: string
  BusWidth: string
  GpuUtilization: string
  MemoryUtilization: string
  CoreClock: string
  MemoryClock: string
  GpuTemperature: string
  FanSpeed: string
  DriverDate: string
}

export interface CpuStatsInfo {
  Temperature: number
  Usage: number
  FrequencyMhz: number
  Voltage: number
  PowerWatts: number
}

export interface CpuInfo {
  Name: string
  Cores: number
  Threads: number
  BaseFreqMhz: number
}

export interface RangeInfo {
  Min: number
  Max: number
}

export interface SmuTelemetry {
  Ppt: number
  Tdc: number | null
  Edc: number | null
  Temp: number
  FreqMhz: number
  Usage: number
}

/**
 * WebView2 hostObjects 返回的 Promise 扩展了 toJson(),
 * 实际数据需经 JSON 反序列化 (见 call()).
 */
type HostBridgePromise<T> = Promise<CommandResult<T>> & { toJson(): string }

/** 后端 Bridge 端点全量类型映射: 与 C# Server/Bridge 及 Linux Server/bridge.py 保持一致 */
export interface BridgeApi {
  CPU: {
    SetCpuShortPower(sp: number): HostBridgePromise<void>
    SetCpuLongPower(lp: number): HostBridgePromise<void>
    SetCustomMode(open: boolean): HostBridgePromise<void>
    GetCustomMode(): HostBridgePromise<boolean>
    GetCPUThermometer(): HostBridgePromise<number>
    GetCpuUsage(): HostBridgePromise<number>
    GetCpuInfo(): HostBridgePromise<CpuInfo>
    GetCpuFrequency(): HostBridgePromise<number>
    GetCpuVoltage(): HostBridgePromise<number>
    GetPhysicalCoreCount(): HostBridgePromise<number>
    SetCPUTempWall(tw: number): HostBridgePromise<void>
  }
  Fan: {
    GetFanSpeed(): HostBridgePromise<FanSpeedInfo>
    SetFanSpeed(fanSpeed: number): HostBridgePromise<void>
    RemoveFanSpeed(): HostBridgePromise<void>
    GetMaxFanSpeedSwitch(): HostBridgePromise<boolean>
    SetMaxFanSpeedSwitch(maxFanSpeedSwitch: boolean): HostBridgePromise<void>
  }
  GPU: {
    Get(): HostBridgePromise<GPUMode>
    Set(mode: GPUMode): HostBridgePromise<void>
  }
  LogoLight: {
    Get(): HostBridgePromise<ResultState>
    Set(state: ResultState): HostBridgePromise<void>
  }
  Keyboard: {
    GetColor(): HostBridgePromise<ColorInfo>
    SetColor(r: number, g: number, b: number): HostBridgePromise<void>
    GetMode(): HostBridgePromise<RGBKeyboardMode>
    SetMode(mode: RGBKeyboardMode): HostBridgePromise<void>
    GetLightBrightness(): HostBridgePromise<RGBKeyboardBrightnessLevel>
    SetLightBrightness(br: RGBKeyboardBrightnessLevel): HostBridgePromise<void>
  }
  PerformanceMode: {
    Get(): HostBridgePromise<SystemPerMode>
    Set(mode: SystemPerMode): HostBridgePromise<void>
  }
  ConfigCtrl: {
    GetConfig(): HostBridgePromise<import('@/types/config').JiaoLongConfigType>
    SetConfig(configJson: string): HostBridgePromise<void>
  }
  AutoStart: {
    Enable(): HostBridgePromise<void>
    Disable(): HostBridgePromise<void>
    IsEnabled(): HostBridgePromise<boolean>
  }
  AutoFan: {
    Start(): HostBridgePromise<void>
    Stop(): HostBridgePromise<void>
    IsRunning(): HostBridgePromise<boolean>
  }
  KeyboardGradient: {
    Start(): HostBridgePromise<void>
    Stop(): HostBridgePromise<void>
    IsRunning(): HostBridgePromise<boolean>
  }
  NvidiaGpu: {
    GetGpuName(gpuIndex?: number): HostBridgePromise<string>
    GetGpuDriverVersion(gpuIndex?: number): HostBridgePromise<string>
    GetGpuDriverDate(gpuIndex?: number): HostBridgePromise<string>
    GetGpuMemoryTotal(gpuIndex?: number): HostBridgePromise<string>
    GetGpuBusWidth(gpuIndex?: number): HostBridgePromise<string>
    GetGpuUtilization(gpuIndex?: number): HostBridgePromise<number>
    GetGpuMemoryUtilization(gpuIndex?: number): HostBridgePromise<number>
    GetGpuCoreClock(gpuIndex?: number): HostBridgePromise<number>
    GetGpuMemoryClock(gpuIndex?: number): HostBridgePromise<number>
    GetGpuTemperature(gpuIndex?: number): HostBridgePromise<number>
    GetGpuFanSpeed(gpuIndex?: number): HostBridgePromise<number>
    GetGpuCoreClockRange(gpuIndex?: number): HostBridgePromise<RangeInfo>
    GetGpuMemoryClockRange(gpuIndex?: number): HostBridgePromise<RangeInfo>
    GetGpuPowerLimitRange(gpuIndex?: number): HostBridgePromise<RangeInfo>
    LockGpuClock(freq: number, gpuIndex?: number): HostBridgePromise<void>
    LockGpuClock(minFreq: number, maxFreq: number, gpuIndex?: number): HostBridgePromise<void>
    LockGpuClockRange(minFreq: number, maxFreq: number, gpuIndex?: number): HostBridgePromise<void>
    ResetGpuClock(gpuIndex?: number): HostBridgePromise<void>
    LockMemoryClock(freq: number, gpuIndex?: number): HostBridgePromise<void>
    ResetMemoryClock(gpuIndex?: number): HostBridgePromise<void>
    SetPowerLimit(watts: number, gpuIndex?: number): HostBridgePromise<void>
  }
  Power: {
    SetCPUMaxFrequency(mhz: number): HostBridgePromise<void>
    ResetCPUMaxFrequency(): HostBridgePromise<void>
    SetCPUMaxState(percent: number): HostBridgePromise<void>
    DisableTurbo(): HostBridgePromise<void>
    EnableTurbo(): HostBridgePromise<void>
    GetCPUMaxFrequency(): HostBridgePromise<{ ac: number; dc: number }>
    GetCPUMaxState(): HostBridgePromise<number>
    GetTurboEnabled(): HostBridgePromise<{ ac: boolean; dc: boolean }>
  }
  SystemInfo: {
    GetSystemOverview(): HostBridgePromise<SystemOverview>
    OpenUrl(url: string): HostBridgePromise<void>
  }
  RyzenSmu: {
    SetStapmLimit(watts: number): HostBridgePromise<void>
    SetStapmTime(seconds: number): HostBridgePromise<void>
    SetFastLimit(watts: number): HostBridgePromise<void>
    SetSlowLimit(watts: number): HostBridgePromise<void>
    SetSlowTime(seconds: number): HostBridgePromise<void>
    SetPptLimitRsmu(watts: number): HostBridgePromise<void>
    SetVrmCurrentMp1(milliamps: number): HostBridgePromise<void>
    SetVrmCurrentRsmu(milliamps: number): HostBridgePromise<void>
    SetTdcLimitMp1(milliamps: number): HostBridgePromise<void>
    SetTdcLimitRsmu(milliamps: number): HostBridgePromise<void>
    SetEdcLimitMp1(milliamps: number): HostBridgePromise<void>
    SetEdcLimitRsmu(milliamps: number): HostBridgePromise<void>
    SetTempLimitMp1(celsius: number): HostBridgePromise<void>
    SetTempLimitRsmu(celsius: number): HostBridgePromise<void>
    SetPboScalar(value: number): HostBridgePromise<void>
    SetOcClk(mhz: number): HostBridgePromise<void>
    SetPerCoreOcClk(coreIdx: number, mhz: number): HostBridgePromise<void>
    SetOcVolt(millivolts: number): HostBridgePromise<void>
    EnableOc(): HostBridgePromise<void>
    DisableOc(): HostBridgePromise<void>
    SetCurveOptimizerAll(value: number): HostBridgePromise<void>
    SetCurveOptimizerPerCore(coreIdx: number, value: number): HostBridgePromise<void>
    GetSmuTelemetry(): HostBridgePromise<SmuTelemetry>
  }
}

declare global {
  interface Window {
    chrome?: {
      webview?: {
        postMessage: (message: unknown) => unknown
        addEventListener: (type: string, listener: (e: MessageEvent) => void) => void
        removeEventListener: (type: string, listener: (e: MessageEvent) => void) => void
        hostObjects: {
          bridge: BridgeApi
        }
      }
    }
  }
}

/**
 * 惰性获取 WebView2 bridge。
 * 不能在模块顶层直接访问 window.chrome.webview.hostObjects.bridge：
 * 一旦获取失败，整个模块会抛异常导致 main.ts 无法执行、页面白屏。
 * 这里通过 Proxy 在真正调用时才解析，模块加载永不失败；成功获取后缓存引用。
 */
let cachedBridge: BridgeApi | null = null

function getBridge(): BridgeApi {
  const bridge = cachedBridge ?? window.chrome?.webview?.hostObjects?.bridge
  if (!bridge) {
    throw new Error('WebView2 bridge 不可用（请通过 JiaoLongControl 主窗口使用）')
  }
  cachedBridge = bridge
  return bridge
}

export const raw: BridgeApi = new Proxy({} as BridgeApi, {
  get: (_target, prop: string | symbol) => {
    const bridge = getBridge()
    const member = (bridge as unknown as Record<string | symbol, unknown>)[prop]
    if (member !== undefined) return member as BridgeApi[keyof BridgeApi]
    // 桥接代理可能缓存了旧的成员列表 (例如新增控制器属性), 重新解析一次桥接对象再取
    cachedBridge = null
    return getBridge()[prop as keyof BridgeApi]
  },
})

export async function call<T>(promise: HostBridgePromise<T>): Promise<CommandResult<T>> {
  return JSON.parse(await promise.toJson())
}
const CACHE_TTL_MS = 1000 // 动态监控类：1 秒内复用，保证实时性
const STATIC_TTL_MS = 30 * 1000 // 静态信息类（硬件名/驱动版本等）：30S 内复用

const readCache = new Map<string, { result: unknown; ts: number }>()

function cached<T>(
  ttlMs: number,
  key: string,
  exec: () => Promise<CommandResult<T>>,
): Promise<CommandResult<T>> {
  const now = Date.now()
  const hit = readCache.get(key)
  if (hit && now - hit.ts < ttlMs) {
    return Promise.resolve(hit.result as CommandResult<T>)
  }
  return exec().then((result) => {
    readCache.set(key, { result, ts: now })
    return result
  })
}

export function toByte(value: number): number {
  if (!Number.isInteger(value)) {
    throw new Error('必须是整数')
  }
  return value
}

export const CPU = {
  SetCpuShortPower: (sp: number) => call(raw.CPU.SetCpuShortPower(toByte(sp))),
  SetCpuLongPower: (lp: number) => call(raw.CPU.SetCpuLongPower(toByte(lp))),
  SetCustomMode: (open: boolean) => call(raw.CPU.SetCustomMode(open)),
  GetCustomMode: () => call(raw.CPU.GetCustomMode()),
  SetCPUTempWall: (tw: number) => call(raw.CPU.SetCPUTempWall(toByte(tw))),
  GetCPUThermometer: () =>
    cached(CACHE_TTL_MS, 'CPU.GetCPUThermometer', () => call(raw.CPU.GetCPUThermometer())),
  GetCpuUsage: () => cached(CACHE_TTL_MS, 'CPU.GetCpuUsage', () => call(raw.CPU.GetCpuUsage())),
  GetCpuInfo: () => cached(STATIC_TTL_MS, 'CPU.GetCpuInfo', () => call(raw.CPU.GetCpuInfo())),
  GetCpuFrequency: () =>
    cached(CACHE_TTL_MS, 'CPU.GetCpuFrequency', () => call(raw.CPU.GetCpuFrequency())),
  GetCpuVoltage: () =>
    cached(CACHE_TTL_MS, 'CPU.GetCpuVoltage', () => call(raw.CPU.GetCpuVoltage())),
  GetPhysicalCoreCount: () =>
    cached(STATIC_TTL_MS, 'CPU.GetPhysicalCoreCount', () => call(raw.CPU.GetPhysicalCoreCount())),
}

export const Fan = {
  GetFanSpeed: () => cached(CACHE_TTL_MS, 'Fan.GetFanSpeed', () => call(raw.Fan.GetFanSpeed())),
  SetFanSpeed: (fanSpeed: number) => call(raw.Fan.SetFanSpeed(toByte(fanSpeed / 100))),
  RemoveFanSpeed: () => call(raw.Fan.RemoveFanSpeed()),
}

export const GPU = {
  Get: () => call(raw.GPU.Get()),
  Set: (mode: GPUMode) => call(raw.GPU.Set(mode)),
}

export const LogoLight = {
  Get: () => call(raw.LogoLight.Get()),
  Set: (state: ResultState) => call(raw.LogoLight.Set(state)),
}

export const Keyboard = {
  GetColor: () => call(raw.Keyboard.GetColor()),
  SetColor: (r: number, g: number, b: number) =>
    call(raw.Keyboard.SetColor(toByte(r), toByte(g), toByte(b))),
  GetMode: () => call(raw.Keyboard.GetMode()),
  SetMode: (mode: RGBKeyboardMode) => call(raw.Keyboard.SetMode(mode)),
  GetLightBrightness: () => call(raw.Keyboard.GetLightBrightness()),
  SetLightBrightness: (br: RGBKeyboardBrightnessLevel) => call(raw.Keyboard.SetLightBrightness(br)),
}

export const PerformanceMode = {
  Get: () => call(raw.PerformanceMode.Get()),
  Set: (mode: SystemPerMode) => call(raw.PerformanceMode.Set(mode)),
}

export const Boot = {
  Enable: () => call(raw.AutoStart.Enable()),
  Disable: () => call(raw.AutoStart.Disable()),
  IsEnabled: () => call(raw.AutoStart.IsEnabled()),
}

export const AutoFanControl = {
  Start: () => call(raw.AutoFan.Start()),
  Stop: () => call(raw.AutoFan.Stop()),
  IsRunning: () => call(raw.AutoFan.IsRunning()),
}

export const KeyboardGradient = {
  Start: () => call(raw.KeyboardGradient.Start()),
  Stop: () => call(raw.KeyboardGradient.Stop()),
  IsRunning: () => call(raw.KeyboardGradient.IsRunning()),
}

export const NvidiaGpu = {
  GetGpuName: (gpuIndex?: number) =>
    cached(STATIC_TTL_MS, `NvidiaGpu.GetGpuName(${gpuIndex ?? ''})`, () =>
      call(raw.NvidiaGpu.GetGpuName(gpuIndex)),
    ),
  GetGpuDriverVersion: (gpuIndex?: number) =>
    cached(STATIC_TTL_MS, `NvidiaGpu.GetGpuDriverVersion(${gpuIndex ?? ''})`, () =>
      call(raw.NvidiaGpu.GetGpuDriverVersion(gpuIndex)),
    ),
  GetGpuDriverDate: (gpuIndex?: number) =>
    cached(STATIC_TTL_MS, `NvidiaGpu.GetGpuDriverDate(${gpuIndex ?? ''})`, () =>
      call(raw.NvidiaGpu.GetGpuDriverDate(gpuIndex)),
    ),
  GetGpuMemoryTotal: (gpuIndex?: number) =>
    cached(STATIC_TTL_MS, `NvidiaGpu.GetGpuMemoryTotal(${gpuIndex ?? ''})`, () =>
      call(raw.NvidiaGpu.GetGpuMemoryTotal(gpuIndex)),
    ),
  GetGpuBusWidth: (gpuIndex?: number) =>
    cached(STATIC_TTL_MS, `NvidiaGpu.GetGpuBusWidth(${gpuIndex ?? ''})`, () =>
      call(raw.NvidiaGpu.GetGpuBusWidth(gpuIndex)),
    ),
  GetGpuUtilization: (gpuIndex?: number) =>
    cached(CACHE_TTL_MS, `NvidiaGpu.GetGpuUtilization(${gpuIndex ?? ''})`, () =>
      call(raw.NvidiaGpu.GetGpuUtilization(gpuIndex)),
    ),
  GetGpuMemoryUtilization: (gpuIndex?: number) =>
    cached(CACHE_TTL_MS, `NvidiaGpu.GetGpuMemoryUtilization(${gpuIndex ?? ''})`, () =>
      call(raw.NvidiaGpu.GetGpuMemoryUtilization(gpuIndex)),
    ),
  GetGpuCoreClock: (gpuIndex?: number) =>
    cached(CACHE_TTL_MS, `NvidiaGpu.GetGpuCoreClock(${gpuIndex ?? ''})`, () =>
      call(raw.NvidiaGpu.GetGpuCoreClock(gpuIndex)),
    ),
  GetGpuMemoryClock: (gpuIndex?: number) =>
    cached(CACHE_TTL_MS, `NvidiaGpu.GetGpuMemoryClock(${gpuIndex ?? ''})`, () =>
      call(raw.NvidiaGpu.GetGpuMemoryClock(gpuIndex)),
    ),
  GetGpuTemperature: (gpuIndex?: number) =>
    cached(CACHE_TTL_MS, `NvidiaGpu.GetGpuTemperature(${gpuIndex ?? ''})`, () =>
      call(raw.NvidiaGpu.GetGpuTemperature(gpuIndex)),
    ),
  GetGpuFanSpeed: (gpuIndex?: number) =>
    cached(CACHE_TTL_MS, `NvidiaGpu.GetGpuFanSpeed(${gpuIndex ?? ''})`, () =>
      call(raw.NvidiaGpu.GetGpuFanSpeed(gpuIndex)),
    ),
  GetGpuCoreClockRange: (gpuIndex?: number) =>
    cached(STATIC_TTL_MS, `NvidiaGpu.GetGpuCoreClockRange(${gpuIndex ?? ''})`, () =>
      call(raw.NvidiaGpu.GetGpuCoreClockRange(gpuIndex)),
    ),
  GetGpuMemoryClockRange: (gpuIndex?: number) =>
    cached(STATIC_TTL_MS, `NvidiaGpu.GetGpuMemoryClockRange(${gpuIndex ?? ''})`, () =>
      call(raw.NvidiaGpu.GetGpuMemoryClockRange(gpuIndex)),
    ),
  GetGpuPowerLimitRange: (gpuIndex?: number) =>
    cached(STATIC_TTL_MS, `NvidiaGpu.GetGpuPowerLimitRange(${gpuIndex ?? ''})`, () =>
      call(raw.NvidiaGpu.GetGpuPowerLimitRange(gpuIndex)),
    ),
  LockGpuClock: (freq: number, gpuIndex?: number) =>
    call(raw.NvidiaGpu.LockGpuClock(freq, gpuIndex)),
  LockGpuClockRange: (minFreq: number, maxFreq: number, gpuIndex?: number) =>
    call(raw.NvidiaGpu.LockGpuClock(minFreq, maxFreq, gpuIndex ?? -1)),
  ResetGpuClock: (gpuIndex?: number) => call(raw.NvidiaGpu.ResetGpuClock(gpuIndex)),
  LockMemoryClock: (freq: number, gpuIndex?: number) =>
    call(raw.NvidiaGpu.LockMemoryClock(freq, gpuIndex)),
  ResetMemoryClock: (gpuIndex?: number) => call(raw.NvidiaGpu.ResetMemoryClock(gpuIndex)),
  SetPowerLimit: (watts: number, gpuIndex?: number) =>
    call(raw.NvidiaGpu.SetPowerLimit(watts, gpuIndex)),
}

export const SystemInfo = {
  GetSystemOverview: () =>
    cached(STATIC_TTL_MS, 'SystemInfo.GetSystemOverview', () =>
      call(raw.SystemInfo.GetSystemOverview()),
    ),
  OpenUrl: (url: string) => call(raw.SystemInfo.OpenUrl(url)),
}

export const RyzenSmu = {
  SetStapmLimit: (watts: number) => call(raw.RyzenSmu.SetStapmLimit(watts)),
  SetStapmTime: (seconds: number) => call(raw.RyzenSmu.SetStapmTime(seconds)),
  SetFastLimit: (watts: number) => call(raw.RyzenSmu.SetFastLimit(watts)),
  SetSlowLimit: (watts: number) => call(raw.RyzenSmu.SetSlowLimit(watts)),
  SetSlowTime: (seconds: number) => call(raw.RyzenSmu.SetSlowTime(seconds)),
  SetPptLimitRsmu: (watts: number) => call(raw.RyzenSmu.SetPptLimitRsmu(watts)),
  SetVrmCurrentMp1: (milliamps: number) => call(raw.RyzenSmu.SetVrmCurrentMp1(milliamps)),
  SetVrmCurrentRsmu: (milliamps: number) => call(raw.RyzenSmu.SetVrmCurrentRsmu(milliamps)),
  SetEdcLimitMp1: (milliamps: number) => call(raw.RyzenSmu.SetEdcLimitMp1(milliamps)),
  SetEdcLimitRsmu: (milliamps: number) => call(raw.RyzenSmu.SetEdcLimitRsmu(milliamps)),
  SetTempLimitMp1: (celsius: number) => call(raw.RyzenSmu.SetTempLimitMp1(celsius)),
  SetTempLimitRsmu: (celsius: number) => call(raw.RyzenSmu.SetTempLimitRsmu(celsius)),
  SetPboScalar: (value: number) => call(raw.RyzenSmu.SetPboScalar(value)),
  SetOcClk: (mhz: number) => call(raw.RyzenSmu.SetOcClk(mhz)),
  SetPerCoreOcClk: (coreIdx: number, mhz: number) =>
    call(raw.RyzenSmu.SetPerCoreOcClk(coreIdx, mhz)),
  SetOcVolt: (millivolts: number) => call(raw.RyzenSmu.SetOcVolt(millivolts)),
  EnableOc: () => call(raw.RyzenSmu.EnableOc()),
  DisableOc: () => call(raw.RyzenSmu.DisableOc()),
  SetCurveOptimizerAll: (value: number) => call(raw.RyzenSmu.SetCurveOptimizerAll(value)),
  SetCurveOptimizerPerCore: (coreIdx: number, value: number) =>
    call(raw.RyzenSmu.SetCurveOptimizerPerCore(coreIdx, value)),
  GetSmuTelemetry: () =>
    cached(CACHE_TTL_MS, 'RyzenSmu.GetSmuTelemetry', () => call(raw.RyzenSmu.GetSmuTelemetry())),
}
export const Power = {
  SetCPUMaxFrequency: (mhz: number) => call(raw.Power.SetCPUMaxFrequency(mhz)),
  ResetCPUMaxFrequency: () => call(raw.Power.ResetCPUMaxFrequency()),
  DisableTurbo: () => call(raw.Power.DisableTurbo()),
  EnableTurbo: () => call(raw.Power.EnableTurbo()),
  GetCPUMaxFrequency: () => call(raw.Power.GetCPUMaxFrequency()),
  GetTurboEnabled: () => call(raw.Power.GetTurboEnabled()),
}

export const Config = {
  GetConfig: () => call(raw.ConfigCtrl.GetConfig()),
  SetConfig: (config: import('@/types/config').JiaoLongConfigType) =>
    call(raw.ConfigCtrl.SetConfig(JSON.stringify(config))),
}
const postMessage = (message: unknown) => {
  if (!window.chrome?.webview) {
    throw new Error('WebView2 不可用')
  }
  return window.chrome.webview.postMessage(message)
}

export const Window = {
  Minimize: () => postMessage('window-minimize'),
  Maximize: () => postMessage('window-maximize'),
  Drag: () => postMessage('window-drag'),
  Close: () => postMessage('window-close'),
}
