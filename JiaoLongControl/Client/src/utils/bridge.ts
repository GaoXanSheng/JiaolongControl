export enum GPUMode {
    HybridMode = 0,
    DiscreteMode = 1,
}

export enum ResultState {
    OFF = 0,
    ON = 1,
}

export type CommandResult<T = void> = {
    Success: boolean;
    Message: string;
} & (T extends void ? {} : { Data: T });

export enum SystemPerMode {
    PerformanceMode = 0,
    QuietMode = 1,
    BalanceMode = 2,
    CustomMode = 3
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
    CPUFanSpeed: number;
    GPUFanSpeed: number;
}

export interface ColorInfo {
    red: number;
    green: number;
    blue: number;
}

export interface SystemOverview {
    CpuName: string;
    GpuName: string;
    OsVersion: string;
    MemoryInfo: string;
}

export interface GpuStats {
    GpuName: string;
    DriverVersion: string;
    MemoryTotal: string;
    BusWidth: string;
    GpuUtilization: string;
    MemoryUtilization: string;
    CoreClock: string;
    MemoryClock: string;
    GpuTemperature: string;
    FanSpeed: string;
    DriverDate: string;
}

declare global {
    interface Window {
        chrome?: {
            webview?: {
                postMessage: (message: any) => any;
                addEventListener: (type: string, listener: (e: MessageEvent) => void) => void;
                removeEventListener: (type: string, listener: (e: MessageEvent) => void) => void;
                hostObjects: {
                    bridge: {
                        CPU: {
                            SetCpuShortPower(sp: number): Promise<any>;
                            SetCpuLongPower(lp: number): Promise<any>;
                            SetCustomMode(open: boolean): Promise<any>;
                            GetCustomMode(): Promise<any>;
                            GetCPUThermometer(): Promise<any>;
                            GetCpuUsage(): Promise<any>;
                            GetCpuInfo(): Promise<any>;
                            GetCpuFrequency(): Promise<any>;
                            GetCpuVoltage(): Promise<any>;
                            GetPhysicalCoreCount(): Promise<any>;
                            SetCPUTempWall(tw: number): Promise<any>;
                        };
                        Fan: {
                            GetFanSpeed(): Promise<any>;
                            SetFanSpeed(fanSpeed: number): Promise<any>;
                            RemoveFanSpeed(): Promise<any>;
                            GetMaxFanSpeedSwitch(): Promise<any>;
                            SetMaxFanSpeedSwitch(maxFanSpeedSwitch: boolean): Promise<boolean>;
                        };
                        GPU: {
                            Get(): Promise<any>;
                            Set(mode: GPUMode): Promise<any>;
                        };
                        LogoLight: {
                            Get(): Promise<any>;
                            Set(state: ResultState): Promise<any>;
                        };
                        Keyboard: {
                            GetColor(): Promise<any>;
                            SetColor(r: number, g: number, b: number): Promise<any>;
                            GetMode(): Promise<any>;
                            SetMode(mode: RGBKeyboardMode): Promise<any>;
                            GetLightBrightness(): Promise<any>;
                            SetLightBrightness(br: RGBKeyboardBrightnessLevel): Promise<any>;
                        };
                        PerformanceMode: {
                            Get(): Promise<any>;
                            Set(mode: SystemPerMode): Promise<any>;
                        };
                        ConfigCtrl: {
                            GetConfig(): Promise<any>;
                            SetConfig(configJson: string): Promise<any>;
                        };
                        AutoStart: {
                            Enable(): Promise<any>;
                            Disable(): Promise<any>;
                            IsEnabled(): Promise<any>;
                        };
                        AutoFan: {
                            Start(): Promise<any>;
                            Stop(): Promise<any>;
                            IsRunning(): Promise<any>;
                        };
                        NvidiaGpu: {
                            GetGpuName(gpuIndex?: number): Promise<any>;
                            GetGpuDriverVersion(gpuIndex?: number): Promise<any>;
                            GetGpuDriverDate(gpuIndex?: number): Promise<any>;
                            GetGpuMemoryTotal(gpuIndex?: number): Promise<any>;
                            GetGpuBusWidth(gpuIndex?: number): Promise<any>;
                            GetGpuUtilization(gpuIndex?: number): Promise<any>;
                            GetGpuMemoryUtilization(gpuIndex?: number): Promise<any>;
                            GetGpuCoreClock(gpuIndex?: number): Promise<any>;
                            GetGpuMemoryClock(gpuIndex?: number): Promise<any>;
                            GetGpuTemperature(gpuIndex?: number): Promise<any>;
                            GetGpuFanSpeed(gpuIndex?: number): Promise<any>;
                            GetGpuCoreClockRange(gpuIndex?: number): Promise<any>;
                            GetGpuMemoryClockRange(gpuIndex?: number): Promise<any>;
                            GetGpuPowerLimitRange(gpuIndex?: number): Promise<any>;
                            LockGpuClock(freq: number, gpuIndex?: number): Promise<any>;
                            LockGpuClockRange(minFreq: number, maxFreq: number, gpuIndex?: number): Promise<any>;
                            ResetGpuClock(gpuIndex?: number): Promise<any>;
                            LockMemoryClock(freq: number, gpuIndex?: number): Promise<any>;
                            ResetMemoryClock(gpuIndex?: number): Promise<any>;
                            SetPowerLimit(watts: number, gpuIndex?: number): Promise<any>;
                            GetGpuTemperature(): Promise<any>;
                            GetGpuCoreClockRange(gpuIndex?: number): Promise<any>;
                            GetGpuMemoryClockRange(gpuIndex?: number): Promise<any>;
                            GetGpuPowerLimitRange(gpuIndex?: number): Promise<any>;
                        };
                        Power: {
                            SetCPUMaxFrequency(mhz: number): Promise<any>;
                            ResetCPUMaxFrequency(): Promise<any>;
                            SetCPUMaxState(percent: number): Promise<any>;
                            DisableTurbo(): Promise<any>;
                            EnableTurbo(): Promise<any>;
                            GetCPUMaxFrequency(): Promise<any>;
                            GetCPUMaxState(): Promise<any>;
                            GetTurboEnabled(): Promise<any>;
                        };
                        SystemInfo: {
                            GetSystemOverview(): Promise<any>;
                            OpenUrl(url: string): Promise<any>;
                        };
                        RyzenSmu: {
                            SetStapmLimit(watts: number): Promise<any>;
                            SetStapmTime(seconds: number): Promise<any>;
                            SetFastLimit(watts: number): Promise<any>;
                            SetSlowLimit(watts: number): Promise<any>;
                            SetSlowTime(seconds: number): Promise<any>;
                            SetPptLimitRsmu(watts: number): Promise<any>;
                            SetVrmCurrentMp1(milliamps: number): Promise<any>;
                            SetVrmCurrentRsmu(milliamps: number): Promise<any>;
                            SetTdcLimitMp1(milliamps: number): Promise<any>;
                            SetTdcLimitRsmu(milliamps: number): Promise<any>;
                            SetEdcLimitMp1(milliamps: number): Promise<any>;
                            SetEdcLimitRsmu(milliamps: number): Promise<any>;
                            SetTempLimitMp1(celsius: number): Promise<any>;
                            SetTempLimitRsmu(celsius: number): Promise<any>;
                            SetPboScalar(value: number): Promise<any>;
                            SetOcClk(mhz: number): Promise<any>;
                            SetPerCoreOcClk(coreIdx: number, mhz: number): Promise<any>;
                            SetOcVolt(millivolts: number): Promise<any>;
                            EnableOc(): Promise<any>;
                            DisableOc(): Promise<any>;
                            SetCurveOptimizerAll(value: number): Promise<any>;
                            SetCurveOptimizerPerCore(coreIdx: number, value: number): Promise<any>;
                            GetSmuTelemetry(): Promise<any>;
                        }
                    };
                };
            };
        };
    }
}

/**
 * 惰性获取 WebView2 bridge。
 * 不能在模块顶层直接访问 window.chrome.webview.hostObjects.bridge：
 * 一旦获取失败，整个模块会抛异常导致 main.ts 无法执行、页面白屏。
 * 这里通过 Proxy 在真正调用时才解析，模块加载永不失败；成功获取后缓存引用。
 */
let cachedBridge: any = null;

function getBridge(): any {
    const bridge = cachedBridge ?? window.chrome?.webview?.hostObjects?.bridge;
    if (!bridge) {
        throw new Error('WebView2 bridge 不可用（请通过 JiaoLongControl 主窗口使用）');
    }
    cachedBridge = bridge;
    return bridge;
}

export const raw: any = new Proxy({} as any, {
    get: (_, prop: string | symbol) => getBridge()[prop],
});

export async function call<T>(promise: Promise<any>): Promise<CommandResult<T>> {
    // @ts-ignore
    return JSON.parse(await promise.toJson());
}

// ===== 读接口结果缓存 =====
// 监控/静态信息类接口被多个轮询源（systemInfo 5s、FanSpeed ~1s、RyzenSmu 3s）反复调用，
// 每次都穿透到后端驱动调用是一笔开销。这里在指定毫秒内对相同方法+参数复用上一条结果，
// 减少后端/驱动调用量。写/动作类接口（Set*/Enable/Disable/Lock/Reset/Start/Stop 等）一律不缓存。
const CACHE_TTL_MS = 1000          // 动态监控类：1 秒内复用，保证实时性
const STATIC_TTL_MS = 60 * 1000    // 静态信息类（硬件名/驱动版本等）：1 分钟内复用

const readCache = new Map<string, { result: any; ts: number }>()

function cached<T>(ttlMs: number, key: string, exec: () => Promise<CommandResult<T>>): Promise<CommandResult<T>> {
    const now = Date.now()
    const hit = readCache.get(key)
    if (hit && now - hit.ts < ttlMs) {
        return Promise.resolve(hit.result)
    }
    return exec().then((result) => {
        readCache.set(key, { result, ts: now })
        return result
    })
}

function toByte(value: number): number {
    if (!Number.isInteger(value)) {
        throw new Error('必须是整数');
    }
    return value;
}

export interface CpuStatsInfo {
    Temperature: number;
    Usage: number;
    FrequencyMhz: number;
    Voltage: number;
    PowerWatts: number;
}

export interface CpuInfo {
    Name: string;
    Cores: number;
    Threads: number;
    BaseFreqMhz: number;
}

export const CPU = {
    SetCpuShortPower: (sp: number) => call(raw.CPU.SetCpuShortPower(toByte(sp))),
    SetCpuLongPower: (lp: number) => call(raw.CPU.SetCpuLongPower(toByte(lp))),
    SetCustomMode: (open: boolean) => call(raw.CPU.SetCustomMode(open)),
    GetCustomMode: () => call(raw.CPU.GetCustomMode()),
    SetCPUTempWall: (tw: number) => call(raw.CPU.SetCPUTempWall(toByte(tw))),
    GetCPUThermometer: () => cached(CACHE_TTL_MS, 'CPU.GetCPUThermometer', () => call<number>(raw.CPU.GetCPUThermometer())),
    GetCpuUsage: () => cached(CACHE_TTL_MS, 'CPU.GetCpuUsage', () => call<number>(raw.CPU.GetCpuUsage())),
    GetCpuInfo: () => cached(STATIC_TTL_MS, 'CPU.GetCpuInfo', () => call<{Name: string, Cores: number, Threads: number, BaseFreqMhz: number}>(raw.CPU.GetCpuInfo())),
    GetCpuFrequency: () => cached(CACHE_TTL_MS, 'CPU.GetCpuFrequency', () => call<number>(raw.CPU.GetCpuFrequency())),
    GetCpuVoltage: () => cached(CACHE_TTL_MS, 'CPU.GetCpuVoltage', () => call<number>(raw.CPU.GetCpuVoltage())),
    GetPhysicalCoreCount: () => cached(STATIC_TTL_MS, 'CPU.GetPhysicalCoreCount', () => call<number>(raw.CPU.GetPhysicalCoreCount())),
};

export const Fan = {
    GetFanSpeed: () => cached(CACHE_TTL_MS, 'Fan.GetFanSpeed', () => call<FanSpeedInfo>(raw.Fan.GetFanSpeed())),
    SetFanSpeed: (fanSpeed: number) => call(raw.Fan.SetFanSpeed(toByte(fanSpeed / 100))),
    RemoveFanSpeed: () => call(raw.Fan.RemoveFanSpeed()),
};

export const GPU = {
    Get: () => call<GPUMode>(raw.GPU.Get()),
    Set: (mode: GPUMode) => call(raw.GPU.Set(mode)),
};

export const LogoLight = {
    Get: () => call<ResultState>(raw.LogoLight.Get()),
    Set: (state: ResultState) => call(raw.LogoLight.Set(state)),
};

export const Keyboard = {
    GetColor: () => call<ColorInfo>(raw.Keyboard.GetColor()),
    SetColor: (r: number, g: number, b: number) => call(raw.Keyboard.SetColor(toByte(r), toByte(g), toByte(b))),
    GetMode: () => call<RGBKeyboardMode>(raw.Keyboard.GetMode()),
    SetMode: (mode: RGBKeyboardMode) => call(raw.Keyboard.SetMode(mode)),
    GetLightBrightness: () => call<RGBKeyboardBrightnessLevel>(raw.Keyboard.GetLightBrightness()),
    SetLightBrightness: (br: RGBKeyboardBrightnessLevel) => call(raw.Keyboard.SetLightBrightness(br)),
};

export const PerformanceMode = {
    Get: () => call<SystemPerMode>(raw.PerformanceMode.Get()),
    Set: (mode: SystemPerMode) => call(raw.PerformanceMode.Set(mode)),
};

export const Boot = {
    Enable: () => call(raw.AutoStart.Enable()),
    Disable: () => call(raw.AutoStart.Disable()),
    IsEnabled: () => call(raw.AutoStart.IsEnabled()),
};

export const AutoFanControl = {
    Start: () => call(raw.AutoFan.Start()),
    Stop: () => call(raw.AutoFan.Stop()),
    IsRunning: () => call(raw.AutoFan.IsRunning()),
};

export const NvidiaGpu = {
    GetGpuName: (gpuIndex?: number) => cached(STATIC_TTL_MS, `NvidiaGpu.GetGpuName(${gpuIndex ?? ''})`, () => call<string>(raw.NvidiaGpu.GetGpuName(gpuIndex))),
    GetGpuDriverVersion: (gpuIndex?: number) => cached(STATIC_TTL_MS, `NvidiaGpu.GetGpuDriverVersion(${gpuIndex ?? ''})`, () => call<string>(raw.NvidiaGpu.GetGpuDriverVersion(gpuIndex))),
    GetGpuDriverDate: (gpuIndex?: number) => cached(STATIC_TTL_MS, `NvidiaGpu.GetGpuDriverDate(${gpuIndex ?? ''})`, () => call<string>(raw.NvidiaGpu.GetGpuDriverDate(gpuIndex))),
    GetGpuMemoryTotal: (gpuIndex?: number) => cached(STATIC_TTL_MS, `NvidiaGpu.GetGpuMemoryTotal(${gpuIndex ?? ''})`, () => call<string>(raw.NvidiaGpu.GetGpuMemoryTotal(gpuIndex))),
    GetGpuBusWidth: (gpuIndex?: number) => cached(STATIC_TTL_MS, `NvidiaGpu.GetGpuBusWidth(${gpuIndex ?? ''})`, () => call<string>(raw.NvidiaGpu.GetGpuBusWidth(gpuIndex))),
    GetGpuUtilization: (gpuIndex?: number) => cached(CACHE_TTL_MS, `NvidiaGpu.GetGpuUtilization(${gpuIndex ?? ''})`, () => call<number>(raw.NvidiaGpu.GetGpuUtilization(gpuIndex))),
    GetGpuMemoryUtilization: (gpuIndex?: number) => cached(CACHE_TTL_MS, `NvidiaGpu.GetGpuMemoryUtilization(${gpuIndex ?? ''})`, () => call<number>(raw.NvidiaGpu.GetGpuMemoryUtilization(gpuIndex))),
    GetGpuCoreClock: (gpuIndex?: number) => cached(CACHE_TTL_MS, `NvidiaGpu.GetGpuCoreClock(${gpuIndex ?? ''})`, () => call<number>(raw.NvidiaGpu.GetGpuCoreClock(gpuIndex))),
    GetGpuMemoryClock: (gpuIndex?: number) => cached(CACHE_TTL_MS, `NvidiaGpu.GetGpuMemoryClock(${gpuIndex ?? ''})`, () => call<number>(raw.NvidiaGpu.GetGpuMemoryClock(gpuIndex))),
    GetGpuTemperature: (gpuIndex?: number) => cached(CACHE_TTL_MS, `NvidiaGpu.GetGpuTemperature(${gpuIndex ?? ''})`, () => call<number>(raw.NvidiaGpu.GetGpuTemperature(gpuIndex))),
    GetGpuFanSpeed: (gpuIndex?: number) => cached(CACHE_TTL_MS, `NvidiaGpu.GetGpuFanSpeed(${gpuIndex ?? ''})`, () => call<number>(raw.NvidiaGpu.GetGpuFanSpeed(gpuIndex))),
    GetGpuCoreClockRange: (gpuIndex?: number) => cached(STATIC_TTL_MS, `NvidiaGpu.GetGpuCoreClockRange(${gpuIndex ?? ''})`, () => call<{Min: number, Max: number}>(raw.NvidiaGpu.GetGpuCoreClockRange(gpuIndex))),
    GetGpuMemoryClockRange: (gpuIndex?: number) => cached(STATIC_TTL_MS, `NvidiaGpu.GetGpuMemoryClockRange(${gpuIndex ?? ''})`, () => call<{Min: number, Max: number}>(raw.NvidiaGpu.GetGpuMemoryClockRange(gpuIndex))),
    GetGpuPowerLimitRange: (gpuIndex?: number) => cached(STATIC_TTL_MS, `NvidiaGpu.GetGpuPowerLimitRange(${gpuIndex ?? ''})`, () => call<{Min: number, Max: number}>(raw.NvidiaGpu.GetGpuPowerLimitRange(gpuIndex))),
    LockGpuClock: (freq: number, gpuIndex?: number) => call(raw.NvidiaGpu.LockGpuClock(freq, gpuIndex)),
    LockGpuClockRange: (minFreq: number, maxFreq: number, gpuIndex?: number) =>
        call(raw.NvidiaGpu.LockGpuClockRange(minFreq, maxFreq, gpuIndex)),
    ResetGpuClock: (gpuIndex?: number) => call(raw.NvidiaGpu.ResetGpuClock(gpuIndex)),
    LockMemoryClock: (freq: number, gpuIndex?: number) => call(raw.NvidiaGpu.LockMemoryClock(freq, gpuIndex)),
    ResetMemoryClock: (gpuIndex?: number) => call(raw.NvidiaGpu.ResetMemoryClock(gpuIndex)),
    SetPowerLimit: (watts: number, gpuIndex?: number) => call(raw.NvidiaGpu.SetPowerLimit(watts, gpuIndex)),
};

export const SystemInfo = {
    GetSystemOverview: () => cached(STATIC_TTL_MS, 'SystemInfo.GetSystemOverview', () => call<SystemOverview>(raw.SystemInfo.GetSystemOverview())),
    OpenUrl: (url: string) => call(raw.SystemInfo.OpenUrl(url)),
};

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
    SetPerCoreOcClk: (coreIdx: number, mhz: number) => call(raw.RyzenSmu.SetPerCoreOcClk(coreIdx, mhz)),
    SetOcVolt: (millivolts: number) => call(raw.RyzenSmu.SetOcVolt(millivolts)),
    EnableOc: () => call(raw.RyzenSmu.EnableOc()),
    DisableOc: () => call(raw.RyzenSmu.DisableOc()),
    SetCurveOptimizerAll: (value: number) => call(raw.RyzenSmu.SetCurveOptimizerAll(value)),
    SetCurveOptimizerPerCore: (coreIdx: number, value: number) => call(raw.RyzenSmu.SetCurveOptimizerPerCore(coreIdx, value)),
    GetSmuTelemetry: () => cached(CACHE_TTL_MS, 'RyzenSmu.GetSmuTelemetry', () => call<{ Ppt: number; Tdc: number; Edc: number; Temp: number; FreqMhz: number; Usage: number }>(raw.RyzenSmu.GetSmuTelemetry())),
};
export const Power = {
    SetCPUMaxFrequency: (mhz: number) => call(raw.Power.SetCPUMaxFrequency(mhz)),
    ResetCPUMaxFrequency: () => call(raw.Power.ResetCPUMaxFrequency()),
    DisableTurbo: () => call(raw.Power.DisableTurbo()),
    EnableTurbo: () => call(raw.Power.EnableTurbo()),
    GetCPUMaxFrequency: () => call<{ ac: number; dc: number }>(raw.Power.GetCPUMaxFrequency()),
    GetTurboEnabled: () => call<{ ac: boolean; dc: boolean }>(raw.Power.GetTurboEnabled()),
};

export const Config = {
    GetConfig: () => call<import('@/types/config').JiaoLongConfigType>(raw.ConfigCtrl.GetConfig()),
    SetConfig: (config: import('@/types/config').JiaoLongConfigType) => call(raw.ConfigCtrl.SetConfig(JSON.stringify(config))),
};
const postMessage = (message: any) => {
    if (!window.chrome?.webview) {
        throw new Error('WebView2 不可用');
    }
    return window.chrome.webview.postMessage(message);
};

export const Window = {
    Minimize: () => postMessage('window-minimize'),
    Maximize: () => postMessage('window-maximize'),
    Drag: () => postMessage('window-drag'),
    Close: () => postMessage('window-close'),
};