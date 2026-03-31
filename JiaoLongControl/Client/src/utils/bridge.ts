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
    BalanceMode = 0,
    PerformanceMode = 1,
    QuietMode = 2,
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

declare global {
    interface Window {
        chrome?: {
            webview?: {
                hostObjects: {
                    bridge: {
                        CPU: {
                            SetCpuShortPower(sp: number): Promise<any>;
                            SetCpuLongPower(lp: number): Promise<any>;
                            SetCustomMode(open: boolean): Promise<any>;
                            GetCustomMode(): Promise<any>;
                            GetCPUThermometer(): Promise<any>;
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
                        Config: {
                            GetConfig(): Promise<any>;
                            SetConfig(config: string): Promise<any>;
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
                            LockGpuClock(freq: number, gpuIndex?: number): Promise<any>;
                            LockGpuClockRange(minFreq: number, maxFreq: number, gpuIndex?: number): Promise<any>;
                            ResetGpuClock(gpuIndex?: number): Promise<any>;
                            LockMemoryClock(freq: number, gpuIndex?: number): Promise<any>;
                            ResetMemoryClock(gpuIndex?: number): Promise<any>;
                            SetPowerLimit(watts: number, gpuIndex?: number): Promise<any>;
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
                        }
                    };
                };
            };
        };
    }
}

const raw = window.chrome!.webview!.hostObjects.bridge;

async function call<T>(promise: Promise<any>): Promise<CommandResult<T>> {
    // @ts-ignore
    return JSON.parse(await promise.toJson());
}

function toByte(value: number): number {
    if (!Number.isInteger(value)) {
        throw new Error('必须是整数');
    }
    return value;
}

export interface ConfigInterface {
    BootMinimized: boolean;
    BootAdvancedFanControlSystem: boolean;
    AdvancedFanControlSystemConfig: { temp: number; speed: number }[];
    BootAdvancedCPUSystem: boolean;
    BootAdvancedGPUSystem: boolean;
    FanPageStore: {
        FanSpeed: number;
    }
    AdvancedCPUSystemConfig: {
        CpuTurbo: boolean
        CpuMaxFrequency: number;
        CpuShortPower: number;
        CpuLongPower: number;
        CpuTempWall: number;
    };
    NvidiaGpuConfig: {
        GpuClock: number
        MemoryClock: number
        PowerLimit: number
    }
}

export const CPU = {
    SetCpuShortPower: (sp: number) => call(raw.CPU.SetCpuShortPower(toByte(sp))),
    SetCpuLongPower: (lp: number) => call(raw.CPU.SetCpuLongPower(toByte(lp))),
    SetCustomMode: (open: boolean) => call(raw.CPU.SetCustomMode(open)),
    GetCustomMode: () => call(raw.CPU.GetCustomMode()),
    SetCPUTempWall: (tw: number) => call(raw.CPU.SetCPUTempWall(toByte(tw))),
    GetCPUThermometer: () => call<number>(raw.CPU.GetCPUThermometer()),
};

export const Fan = {
    GetFanSpeed: () => call<FanSpeedInfo>(raw.Fan.GetFanSpeed()),
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

export const Config = {
    GetConfig: async () => {
        return await call<ConfigInterface>(raw.Config.GetConfig());
    },
    SetConfig: (config: ConfigInterface) => call(raw.Config.SetConfig(JSON.stringify(config))),
    Boot: {
        Enable: () => call(raw.AutoStart.Enable()),
        Disable: () => call(raw.AutoStart.Disable()),
        IsEnabled: () => call(raw.AutoStart.IsEnabled()),
    },
};

export const AutoFanControl = {
    Start: () => call(raw.AutoFan.Start()),
    Stop: () => call(raw.AutoFan.Stop()),
    IsRunning: () => call(raw.AutoFan.IsRunning()),
};

export const NvidiaGpu = {
    LockGpuClock: (freq: number, gpuIndex?: number) => call(raw.NvidiaGpu.LockGpuClock(freq, gpuIndex)),
    LockGpuClockRange: (minFreq: number, maxFreq: number, gpuIndex?: number) =>
        call(raw.NvidiaGpu.LockGpuClockRange(minFreq, maxFreq, gpuIndex)),
    ResetGpuClock: (gpuIndex?: number) => call(raw.NvidiaGpu.ResetGpuClock(gpuIndex)),
    LockMemoryClock: (freq: number, gpuIndex?: number) => call(raw.NvidiaGpu.LockMemoryClock(freq, gpuIndex)),
    ResetMemoryClock: (gpuIndex?: number) => call(raw.NvidiaGpu.ResetMemoryClock(gpuIndex)),
    SetPowerLimit: (watts: number, gpuIndex?: number) => call(raw.NvidiaGpu.SetPowerLimit(watts, gpuIndex)),
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
};
export const Power = {
    SetCPUMaxFrequency: (mhz: number) => call(raw.Power.SetCPUMaxFrequency(mhz)),
    ResetCPUMaxFrequency: () => call(raw.Power.ResetCPUMaxFrequency()),
    DisableTurbo: () => call(raw.Power.DisableTurbo()),
    EnableTurbo: () => call(raw.Power.EnableTurbo()),
    GetCPUMaxFrequency: () => call<{ ac: number; dc: number }>(raw.Power.GetCPUMaxFrequency()),
    GetTurboEnabled: () => call<{ ac: boolean; dc: boolean }>(raw.Power.GetTurboEnabled()),
};