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

interface HostObjects  {
        CPU: {
          SetCpuShortPower(sp: number): Promise<boolean>;
          SetCpuLongPower(lp: number): Promise<boolean>;
          SetCustomMode(open: boolean): Promise<boolean>;
          GetCustomMode(): Promise<boolean>;
          SetCPUTempWall(tw: number): Promise<boolean>;
        };
        Fan: {
          GetFanSpeed(): Promise<String>;
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
          Get(): Promise<number>;
          Set(state: ResultState): Promise<boolean>;
        };
        Keyboard: {
          GetColor(): Promise<String>;
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
                hostObjects:{
                    bridge: HostObjects;
                }
            };
        };
    }
}
const bridge = window.chrome!.webview!.hostObjects.bridge;

export const CPU = bridge.CPU;
export const Fan = bridge.Fan;
export const GPU = bridge.GPU;
export const LogoLight = bridge.LogoLight;
export const Keyboard = bridge.Keyboard;
export const PerformanceMode = bridge.PerformanceMode;
export const Hardware = bridge.Hardware;


export default bridge;
