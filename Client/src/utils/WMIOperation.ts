import {Message} from '@arco-design/web-vue'
import {Bridge} from '@/utils/bridge.ts'
import type {HardwareMonitorInfo} from '@/stores/interfaces.ts'
import type {Point} from '@/components/ProSettingComponent/FanCurve/useFanCurve.ts'

export enum GPUMode {
    DiscreteMode,
    HybridMode,
    Unknow = 255
}

export enum ResultState {
    ON,
    OFF,
    Unknow = 255
}

export enum PerformaceMode {
    GamingMode,
    RampageMode,
    OfficeMode,
    Unknow = 255
}

// 通用调用封装
async function wmiInvoke(typeName: string, methodName: string, args: any[] = []) {
    try {
        return await Bridge.invoke('HardwareControl', null, [typeName, methodName, ...args]);
    } catch (error: any) {
        Message.error(error.message || '操作失败');
        return null;
    }
}

const WMIOperation = {
    CPU: {
        SetCpuShortPower: (watte: number) => wmiInvoke('CPU', 'SetCpuShortPower', [watte]),
        SetCpuLongPower: (watte: number) => wmiInvoke('CPU', 'SetCpuLongPower', [watte]),
        OpenCustomMode: (Open: boolean) => wmiInvoke('CPU', 'OpenCustomMode', [Open]),
        GetCustomMode: async () => {
            const res = await wmiInvoke('CPU', 'GetCustomMode');
            return res === 'True';
        },
        SetCPUTempWall: (centigrade: number) => wmiInvoke('CPU', 'SetCPUTempWall', [centigrade])
    },
    Keyboard: {
        Color: {
            Set: (Red: number, Green: number, Blue: number) => wmiInvoke('Keyboard', 'ColorSet', [Red, Green, Blue]),
            Get: async () => {
                return await wmiInvoke('Keyboard', 'ColorGet') || { red: 0, green: 0, blue: 0 };
            }
        },
        Mode: {
            Set: () => wmiInvoke('Keyboard', 'ModeSet'),
            Get: () => wmiInvoke('Keyboard', 'ModeGet')
        },
        LightBrightness: {
            Set: (Brightness: number) => wmiInvoke('Keyboard', 'LightBrightnessSet', [Brightness]),
            Get: async () => {
                const res = await wmiInvoke('Keyboard', 'LightBrightnessGet');
                // 简单的映射
                const levels: Record<string, number> = { 'Level_0': 0, 'Level_1': 1, 'Level_2': 2, 'Level_3': 3 };
                return levels[res] ?? 255;
            }
        }
    },
    LogoLight: {
        Set: (On: ResultState) => wmiInvoke('LogoLight', 'Set', [On]),
        Get: async () => {
            const res = await wmiInvoke('LogoLight', 'Get');
            return res === 'ON' ? ResultState.ON : (res === 'OFF' ? ResultState.OFF : ResultState.Unknow);
        }
    },
    GPUMode: {
        Set: (mode: GPUMode) => wmiInvoke('GPUMode', 'Set', [mode]),
        Get: async () => {
            const res = await wmiInvoke('GPUMode', 'Get');
            return res === 'DiscreteMode' ? GPUMode.DiscreteMode : (res === 'HybridMode' ? GPUMode.HybridMode : GPUMode.Unknow);
        }
    },
    PerformaceMode: {
        Set: (Mode: PerformaceMode) => wmiInvoke('PerformaceMode', 'Set', [Mode]),
        Get: () => wmiInvoke('PerformaceMode', 'Get')
    },
    Fan: {
        SetFanCurve: async (data: Point[]) => {
            // 特殊通道：发送 JSON 字符串
            return await Bridge.invoke('SetFanCurve', JSON.stringify(data));
        },
        GetMaxFanSpeedSwitch: () => wmiInvoke('Fan', 'GetMaxFanSpeedSwitch'),
        SetMaxFanSpeedSwitch: (b: boolean) => wmiInvoke('Fan', 'SetMaxFanSpeedSwitch', [b ? 1 : 0]),
        SetFanSpeed: async (FanSpeed: number) => {
            const speed = parseInt(FanSpeed.toString().slice(0, 2));
            if (speed <= 58) {
                return await wmiInvoke('Fan', 'SetFanSpeed', [speed]);
            }
            return '风扇转速超出范围';
        },
        GetFanSpeed: async () => {
            return await wmiInvoke('Fan', 'GetFanSpeed') || { CPUFanSpeed: 0, GPUFanSpeed: 0 };
        }
    },
    GetHardwareMonitorInfo: async () => {
        // 这个调用可能比较耗时，C# 端已经做了优化
        return await Bridge.invoke('HardwareControl', null, ['GetHardwareMonitorInfo', 'GetHardwareMonitorInfo', '1']);
    }
}

export default WMIOperation;