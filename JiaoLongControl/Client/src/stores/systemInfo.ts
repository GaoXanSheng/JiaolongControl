import { defineStore } from 'pinia'
import { CPU, Fan, NvidiaGpu, type FanSpeedInfo, type GpuStats } from '@/utils/bridge'
import { POLL_INTERVAL_SYSTEM_INFO } from '@/constants'

export interface CpuStatsInfo {
  FrequencyMhz: number
  Voltage: number
  Usage: number
  Temperature: number
}

export interface SystemInfoState {
  cpuTemp: number
  cpuStats: CpuStatsInfo | null
  gpuTemp: number
  fanSpeed: FanSpeedInfo
  gpuStats: GpuStats | null
  error: string | null
  loading: boolean
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
    error: null,
    loading: false,
  }),

  getters: {
    gpuName: (state) => state.gpuStats?.GpuName || 'N/A',
    gpuDriverVersion: (state) => state.gpuStats?.DriverVersion || 'N/A',
    gpuDriverDate: (state) => state.gpuStats?.DriverDate || 'N/A',
    gpuMemoryTotal: (state) => state.gpuStats?.MemoryTotal || 'N/A',
    gpuBusWidth: (state) => state.gpuStats?.BusWidth || 'N/A',
    gpuUtilization: (state) => parseInt(state.gpuStats?.GpuUtilization || '0', 10),
    gpuMemoryUtilization: (state) => parseInt(state.gpuStats?.MemoryUtilization || '0', 10),
    gpuCoreClock: (state) => parseInt(state.gpuStats?.CoreClock || '0', 10),
    gpuMemoryClock: (state) => parseInt(state.gpuStats?.MemoryClock || '0', 10),
    gpuFanSpeed: (state) => parseInt(state.gpuStats?.FanSpeed || '0', 10),
  },

  actions: {
    async fetchSystemInfo() {
      this.loading = true
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
          GpuUtilization: gpuUtil.Success ? String(gpuUtil.Data) : '0',
          MemoryUtilization: gpuMemUtil.Success ? String(gpuMemUtil.Data) : '0',
          CoreClock: gpuCoreClock.Success ? String(gpuCoreClock.Data) : '0',
          MemoryClock: gpuMemClock.Success ? String(gpuMemClock.Data) : '0',
          FanSpeed: gpuFanSpeed.Success ? String(gpuFanSpeed.Data) : '0',
          GpuTemperature: gTemp.Success ? String(gTemp.Data) : '0',
        }
        this.gpuTemp = gTemp.Success ? gTemp.Data : 0

        if (fSpeed.Success) {
          this.fanSpeed = fSpeed.Data
        }

        this.error = null
      } catch (err) {
        this.error = err instanceof Error ? err.message : 'Failed to fetch system info'
      } finally {
        this.loading = false
      }
    },

    startPolling(interval = POLL_INTERVAL_SYSTEM_INFO) {
      this.fetchSystemInfo()
      const polling = setInterval(() => this.fetchSystemInfo(), interval)
      return () => clearInterval(polling)
    },
  },
})
