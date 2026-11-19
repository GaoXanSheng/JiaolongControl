import {defineStore} from 'pinia'
import {CPU, Fan, type FanSpeedInfo, NvidiaGpu} from '@/utils/bridge'
import {POLL_INTERVAL_SYSTEM_INFO} from '@/constants'
import {timing} from '@/utils/timing'

let systemInfoTimed = false
let systemInfoInflight: Promise<void> | null = null

export interface CpuStatsInfo {
  FrequencyMhz: number
  Voltage: number
  Usage: number
  Temperature: number
}

/** GPU 实时监控数据: 名称类为字符串, 读数类直接存数值 (失败置 0) */
export interface GpuStats {
  GpuName: string
  DriverVersion: string
  DriverDate: string
  MemoryTotal: string
  BusWidth: string
  GpuUtilization: number
  MemoryUtilization: number
  CoreClock: number
  MemoryClock: number
  FanSpeed: number
}

export interface SystemInfoState {
  cpuTemp: number
  cpuStats: CpuStatsInfo | null
  gpuTemp: number
  fanSpeed: FanSpeedInfo
  gpuStats: GpuStats | null
}

export const useSystemInfoStore = defineStore('systemInfo', {
  state: (): SystemInfoState => ({
    cpuTemp: 0,
    cpuStats: null,
    gpuTemp: 0,
    fanSpeed: {
      CPUFanSpeed: 0,
      GPUFanSpeed: 0,
    },
    gpuStats: null,
  }),

  getters: {
    gpuName: (state) => state.gpuStats?.GpuName || 'N/A',
    gpuDriverVersion: (state) => state.gpuStats?.DriverVersion || 'N/A',
    gpuDriverDate: (state) => state.gpuStats?.DriverDate || 'N/A',
    gpuMemoryTotal: (state) => state.gpuStats?.MemoryTotal || 'N/A',
    gpuBusWidth: (state) => state.gpuStats?.BusWidth || 'N/A',
    gpuUtilization: (state) => state.gpuStats?.GpuUtilization ?? 0,
    gpuMemoryUtilization: (state) => state.gpuStats?.MemoryUtilization ?? 0,
    gpuCoreClock: (state) => state.gpuStats?.CoreClock ?? 0,
    gpuMemoryClock: (state) => state.gpuStats?.MemoryClock ?? 0,
    gpuFanSpeed: (state) => state.gpuStats?.FanSpeed ?? 0,
  },

  actions: {
    async fetchSystemInfo() {
      if (systemInfoInflight) return systemInfoInflight
      systemInfoInflight = this.fetchSystemInfoOnce()
      try {
        await systemInfoInflight
      } finally {
        systemInfoInflight = null
      }
    },

    async fetchSystemInfoOnce() {
      const started = performance.now()
      try {
        const [
          cpuTemp,
          cpuUsage,
          cpuFreq,
          cpuVolt,
          gTemp,
          fSpeed,
          gpuName,
          gpuDriverVersion,
          gpuDriverDate,
          gpuMemoryTotal,
          gpuBusWidth,
          gpuUtil,
          gpuMemUtil,
          gpuCoreClock,
          gpuMemClock,
          gpuFanSpeed,
        ] = await Promise.all([
          CPU.GetCPUThermometer(),
          CPU.GetCpuUsage(),
          CPU.GetCpuFrequency(),
          CPU.GetCpuVoltage(),
          NvidiaGpu.GetGpuTemperature(),
          Fan.GetFanSpeed(),
          NvidiaGpu.GetGpuName(),
          NvidiaGpu.GetGpuDriverVersion(),
          NvidiaGpu.GetGpuDriverDate(),
          NvidiaGpu.GetGpuMemoryTotal(),
          NvidiaGpu.GetGpuBusWidth(),
          NvidiaGpu.GetGpuUtilization(),
          NvidiaGpu.GetGpuMemoryUtilization(),
          NvidiaGpu.GetGpuCoreClock(),
          NvidiaGpu.GetGpuMemoryClock(),
          NvidiaGpu.GetGpuFanSpeed(),
        ])

        this.cpuStats = {
          FrequencyMhz: cpuFreq.Success ? cpuFreq.Data : 0,
          Voltage: cpuVolt.Success ? cpuVolt.Data : 0,
          Usage: cpuUsage.Success ? cpuUsage.Data : 0,
          Temperature: cpuTemp.Success ? cpuTemp.Data : 0,
        }
        this.cpuTemp = cpuTemp.Success ? cpuTemp.Data : 0

        this.gpuStats = {
          GpuName: gpuName.Success ? gpuName.Data : 'N/A',
          DriverVersion: gpuDriverVersion.Success ? gpuDriverVersion.Data : 'N/A',
          DriverDate: gpuDriverDate.Success ? gpuDriverDate.Data : 'N/A',
          MemoryTotal: gpuMemoryTotal.Success ? gpuMemoryTotal.Data : 'N/A',
          BusWidth: gpuBusWidth.Success ? gpuBusWidth.Data : 'N/A',
          GpuUtilization: gpuUtil.Success ? gpuUtil.Data : 0,
          MemoryUtilization: gpuMemUtil.Success ? gpuMemUtil.Data : 0,
          CoreClock: gpuCoreClock.Success ? gpuCoreClock.Data : 0,
          MemoryClock: gpuMemClock.Success ? gpuMemClock.Data : 0,
          FanSpeed: gpuFanSpeed.Success ? gpuFanSpeed.Data : 0,
        }
        this.gpuTemp = gTemp.Success ? gTemp.Data : 0

        if (fSpeed.Success) {
          this.fanSpeed = fSpeed.Data
        }
      } catch (err) {
        console.warn('刷新系统信息失败', err)
      }
      if (!systemInfoTimed) {
        systemInfoTimed = true
        timing('systemInfo first fetch', started)
      }
    },

    startPolling(interval = POLL_INTERVAL_SYSTEM_INFO) {
      this.fetchSystemInfo()
      const polling = setInterval(() => this.fetchSystemInfo(), interval)
      return () => clearInterval(polling)
    },
  },
})
