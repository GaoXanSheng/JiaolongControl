import { defineStore } from 'pinia';
import { CPU, Fan, NvidiaGpu, type FanSpeedInfo, type GpuStats } from '@/utils/bridge';

export interface SystemInfoState {
  cpuTemp: number;
  gpuTemp: number;
  fanSpeed: FanSpeedInfo;
  gpuStats: GpuStats | null;
  error: string | null;
  loading: boolean;
}

export const useSystemInfoStore = defineStore('systemInfo', {
  state: (): SystemInfoState => ({
    cpuTemp: 0,
    gpuTemp: 0,
    fanSpeed: {
        CPUFanSpeed: 0,
        GPUFanSpeed: 0,
    },
    gpuStats: null,
    error: null,
    loading: false,
  }),

  actions: {
    async fetchSystemInfo() {
      this.loading = true;
      try {
        const [cTemp, gTemp, fSpeed, gpuStats] = await Promise.all([
          CPU.GetCPUThermometer(),
          NvidiaGpu.GetGpuTemperature(),
          Fan.GetFanSpeed(),
          NvidiaGpu.GetGpuAllStats(),
        ]);

        if (cTemp.Success) {
          this.cpuTemp = cTemp.Data;
        }
        if (gTemp.Success) {
          this.gpuTemp = gTemp.Data;
        }
        if (fSpeed.Success) {
          this.fanSpeed = fSpeed.Data;
        }
        if (gpuStats.Success) {
          this.gpuStats = gpuStats.Data;
        }

        this.error = null;
      } catch (err: any) {
        this.error = err.message || 'Failed to fetch system info';
      } finally {
        this.loading = false;
      }
    },

    startPolling(interval = 5000) {
        this.fetchSystemInfo();
        const polling = setInterval(() => this.fetchSystemInfo(), interval);
        
        // Return a function to stop the polling
        return () => clearInterval(polling);
    }
  },
});
