import type {HardwareMonitorInfo} from "@/stores/interfaces.ts";

export enum GPUMode {
    HybridMode = 0,
    DiscreteMode = 1,
}

export enum ResultState {
    OFF = 0,
    ON = 1,
}

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

interface HostObjects {
    CPU: {
        SetCpuShortPower(sp: number): Promise<boolean>;
        SetCpuLongPower(lp: number): Promise<boolean>;
        SetCustomMode(open: boolean): Promise<boolean>;
        GetCustomMode(): Promise<boolean>;
        SetCPUTempWall(tw: number): Promise<boolean>;
    };
    Fan: {
        GetFanSpeed(): Promise<string>;
        SetFanSpeed(fanSpeed: number): Promise<boolean>;
        SetMaxFanSpeedSwitch(
            maxFanSpeedSwitch: boolean
        ): Promise<boolean>;
        GetMaxFanSpeedSwitch(): Promise<boolean>;
    };
    GPU: {
        Get(): Promise<GPUMode>;
        Set(mode: GPUMode): Promise<boolean>;
    };
    LogoLight: {
        Get(): Promise<boolean>;
        Set(state: ResultState): Promise<boolean>;
    };
    Keyboard: {
        GetColor(): Promise<string>;
        SetColor(r: number, g: number, b: number): Promise<boolean>;
        GetMode(): Promise<RGBKeyboardMode>;
        SetMode(mode: RGBKeyboardMode): Promise<boolean>;
        GetLightBrightness(): Promise<RGBKeyboardBrightnessLevel>;
        SetLightBrightness(
            br: RGBKeyboardBrightnessLevel
        ): Promise<boolean>;
    };
    PerformanceMode: {
        Get(): Promise<SystemPerMode>;
        Set(mode: SystemPerMode): Promise<boolean>;
    };
    Hardware: {
        GetHardwareMonitorInfo(): Promise<string>;
    };
}

declare global {
    interface Window {
        chrome?: {
            webview?: {
                hostObjects: {
                    bridge: HostObjects;
                }
            };
        };
    }
}
const bridge = window.chrome!.webview!.hostObjects.bridge;

export const CPU = {
    /**
     *  0 - 255
     * @param sp
     * @constructor
     */
    SetCpuShortPower: async (sp: number) => {
        return await bridge.CPU.SetCpuShortPower(toByte(sp))
    },

    SetCpuLongPower: async (lp: number) => {
        return await bridge.CPU.SetCpuLongPower(toByte(lp))
    },
    SetCustomMode: bridge.CPU.SetCustomMode,
    GetCustomMode: bridge.CPU.GetCustomMode,
    SetCPUTempWall: async (tw: number) => {
            return bridge.CPU.SetCPUTempWall(toByte(tw))
    },
};
export const Fan = {
    GetFanSpeed:  async () => {
        return JSON.parse(await bridge.Fan.GetFanSpeed()) as FanSpeedInfo
    },
    SetFanSpeed: async (fanSpeed: number): Promise<boolean> => {
        return await bridge.Fan.SetFanSpeed(toByte(fanSpeed / 100))
    }
    ,
    SetMaxFanSpeedSwitch: bridge.Fan.SetMaxFanSpeedSwitch,
    GetMaxFanSpeedSwitch: bridge.Fan.GetMaxFanSpeedSwitch,
};
export const GPU = bridge.GPU;
export const LogoLight = bridge.LogoLight;
export const Keyboard = {
    GetColor: async () => {
        return JSON.parse(await bridge.Keyboard.GetColor()) as ColorInfo
    },
    SetColor: async (r: number, g: number, b: number) => {
        return await bridge.Keyboard.SetColor(toByte(r), toByte(g), toByte(b))
    },
    GetMode: bridge.Keyboard.GetMode,
    SetMode: bridge.Keyboard.SetMode,
    GetLightBrightness: bridge.Keyboard.GetLightBrightness,
    SetLightBrightness: bridge.Keyboard.SetLightBrightness,
};
export const PerformanceMode = bridge.PerformanceMode;
export const Hardware = {
    GetHardwareMonitorInfo: async () => {
        return JSON.parse(await bridge.Hardware.GetHardwareMonitorInfo()) as HardwareMonitorInfo
    },
};

function toByte(value:number):number {
    if (!Number.isInteger(value)) {
        throw new Error('必须是整数')
    }

    if (value <= 0 || value >= 255) {
        throw new Error('范围必须在0-255')
    }
    return  value
}
