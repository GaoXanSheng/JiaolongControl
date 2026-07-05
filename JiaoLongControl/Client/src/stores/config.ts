import { defineStore } from 'pinia'
import { Config } from '@/utils/bridge'
import type { JiaoLongConfigType } from '@/types/config'

function debounce(fn: Function, delay: number) {
  let timer: any = null
  return function (this: any, ...args: any[]) {
    if (timer) clearTimeout(timer)
    timer = setTimeout(() => { fn.apply(this, args) }, delay)
  }
}

export const useConfigStore = defineStore('config', {
  state: () => ({
    config: null as JiaoLongConfigType | null,
    loading: false,
    error: null as string | null,
  }),

  actions: {
    async fetchConfig() {
      this.loading = true
      try {
        const result = await Config.GetConfig()
        if (result.Success) this.config = result.Data
        this.error = null
      } catch (err: any) {
        this.error = err.message || 'Failed to fetch config'
      } finally {
        this.loading = false
      }
    },

    async saveConfig() {
      if (this.config) await Config.SetConfig(this.config)
    },

    debouncedSave: debounce(async function (this: any) {
      await this.saveConfig()
    }, 1000),
  }
})
