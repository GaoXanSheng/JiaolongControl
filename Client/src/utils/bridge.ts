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
          SetCpuShortPower(sp: number): boolean;
          SetCpuLongPower(lp: number): boolean;
          SetCustomMode(open: boolean): boolean;
          GetCustomMode(): boolean;
          SetCPUTempWall(tw: number): boolean;
        };
        Fan: {
          GetFanSpeed(): String;
          SetFanSpeed(fanSpeed: number):boolean;
          SetMaxFanSpeedSwitch(
            maxFanSpeedSwitch: boolean
          ): boolean;
          GetMaxFanSpeedSwitch(): boolean;
        };
        GPU: {
          Get(): GPUMode;
          Set(mode: GPUMode): boolean;
        };
        LogoLight: {
          Get(): number;
          Set(state: ResultState):boolean;
        };
        Keyboard: {
          GetColor(): String;
          SetColor(r: number, g: number, b: number): boolean;
          GetMode(): RGBKeyboardMode;
          SetMode(mode: RGBKeyboardMode): boolean;
          GetLightBrightness(): RGBKeyboardBrightnessLevel;
          SetLightBrightness(
            br: RGBKeyboardBrightnessLevel
          ): boolean;
        };
        PerformanceMode: {
          Get(): SystemPerMode;
          Set(mode: SystemPerMode): boolean;
        };
        Hardware: {
          GetHardwareMonitorInfo(): string;
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
