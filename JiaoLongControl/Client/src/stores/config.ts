import { defineStore } from 'pinia'
import { Config, type CpuConfigType, type GpuConfigType, type FanConfigType, type AppConfigType, type SmuConfigType } from '@/utils/bridge.config.gen'

function debounce(fn: Function, delay: number) {
  let timer: any = null
  return function (this: any, ...args: any[]) {
    if (timer) clearTimeout(timer)
    timer = setTimeout(() => { fn.apply(this, args) }, delay)
  }
}

export const useConfigStore = defineStore('config', {
  state: () => ({
    cpu: null as CpuConfigType | null,
    gpu: null as GpuConfigType | null,
    fan: null as FanConfigType | null,
    app: null as AppConfigType | null,
    smu: null as SmuConfigType | null,
    loading: false,
    error: null as string | null,
  }),

  actions: {
    async fetchAllConfigs() {
      this.loading = true
      try {
        const [cpu, gpu, fan, app, smu] = await Promise.all([
          Config.GetCpuConfig(), Config.GetGpuConfig(), Config.GetFanConfig(),
          Config.GetAppConfig(), Config.GetSmuConfig(),
        ])
        if (cpu.Success) this.cpu = cpu.Data
        if (gpu.Success) this.gpu = gpu.Data
        if (fan.Success) this.fan = fan.Data
        if (app.Success) this.app = app.Data
        if (smu.Success) this.smu = smu.Data
        this.error = null
      } catch (err: any) {
        this.error = err.message || 'Failed to fetch config'
      } finally {
        this.loading = false
      }
    },

    async saveCpuConfig() { if (this.cpu) await Config.SetCpuConfig(this.cpu) },
    async saveGpuConfig() { if (this.gpu) await Config.SetGpuConfig(this.gpu) },
    async saveFanConfig() { if (this.fan) await Config.SetFanConfig(this.fan) },
    async saveAppConfig() { if (this.app) await Config.SetAppConfig(this.app) },
    async saveSmuConfig() { if (this.smu) await Config.SetSmuConfig(this.smu) },

    saveAllConfigs() {
      return Promise.all([
        this.saveCpuConfig(), this.saveGpuConfig(), this.saveFanConfig(),
        this.saveAppConfig(), this.saveSmuConfig(),
      ])
    },

    debouncedSave: debounce(async function (this: any) {
      await this.saveAllConfigs()
    }, 1000),

    async reloadCpuConfig() {
      try {
        const result = await Config.GetCpuConfig()
        if (result.Success) this.cpu = result.Data
      } catch (err) {
        console.error('Failed to load CpuConfig:', err)
      }
    },
    async reloadGpuConfig() {
      try {
        const result = await Config.GetGpuConfig()
        if (result.Success) this.gpu = result.Data
      } catch (err) {
        console.error('Failed to load GpuConfig:', err)
      }
    },
    async reloadFanConfig() {
      try {
        const result = await Config.GetFanConfig()
        if (result.Success) this.fan = result.Data
      } catch (err) {
        console.error('Failed to load FanConfig:', err)
      }
    },
    async reloadAppConfig() {
      try {
        const result = await Config.GetAppConfig()
        if (result.Success) this.app = result.Data
      } catch (err) {
        console.error('Failed to load AppConfig:', err)
      }
    },
    async reloadSmuConfig() {
      try {
        const result = await Config.GetSmuConfig()
        if (result.Success) this.smu = result.Data
      } catch (err) {
        console.error('Failed to load SmuConfig:', err)
      }
    },
  }
})
