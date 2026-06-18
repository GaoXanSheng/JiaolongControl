import { defineStore } from 'pinia'
import { Config, type ConfigInterface } from '@/utils/bridge'

// 防抖函数
function debounce(fn: Function, delay: number) {
  let timer: any = null
  return function (this: any, ...args: any[]) {
    if (timer) clearTimeout(timer)
    timer = setTimeout(() => {
      fn.apply(this, args)
    }, delay)
  }
}

export const useConfigStore = defineStore('config', {
  state: () => ({
    config: null as ConfigInterface | null,
    loading: false,
    error: null as string | null,
  }),

  actions: {
    async fetchConfig() {
      this.loading = true
      try {
        const result = await Config.GetConfig()
        if (result.Success) {
          this.config = result.Data
        } else {
          this.error = result.Message
        }
      } catch (err: any) {
        this.error = err.message || 'Failed to fetch config'
      } finally {
        this.loading = false
      }
    },

    async updateConfig(newConfig: Partial<ConfigInterface>) {
      if (!this.config) return
      
      // 合并新配置
      this.config = { ...this.config, ...newConfig }
      
      // 触发自动保存
      this.debouncedSave()
    },

    // 内部使用的保存逻辑
    async saveConfig() {
      if (!this.config) return
      try {
        await Config.SetConfig(this.config)
      } catch (err) {
        console.error('Failed to save config:', err)
      }
    },

    // 暴露给外部的手动保存（可选）
    debouncedSave: debounce(async function(this: any) {
      await this.saveConfig()
    }, 1000)
  }
})
