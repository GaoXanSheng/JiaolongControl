import { defineStore } from 'pinia'
import { Config, type ConfigInterface } from '@/utils/bridge'

// --- Utility Functions ---

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

// 深合并函数
function deepMerge(target: any, source: any) {
  for (const key in source) {
    if (source.hasOwnProperty(key)) {
      if (source[key] instanceof Object && key in target) {
        target[key] = deepMerge(target[key], source[key]);
      } else {
        target[key] = source[key];
      }
    }
  }
  return target;
}


// --- Pinia Store Definition ---

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
            if (result.Success && result.Data) {
              this.config = result.Data as ConfigInterface;
            } else {
              this.error = result.Message || 'Failed to parse config data'
            }
          } catch (err: any) {
            this.error = err.message || 'Failed to fetch config'
          } finally {
            this.loading = false
          }
        },

    updateConfig(newConfig: Partial<ConfigInterface>) {
      if (!this.config) return
      
      // 使用深合并代替浅合并
      this.config = deepMerge(this.config, newConfig);
      
      // 触发自动保存
      this.debouncedSave()
    },

    async saveConfig() {
      if (!this.config) return
      try {
        await Config.SetConfig(this.config)
      } catch (err) {
        console.error('Failed to save config:', err)
      }
    },

    debouncedSave: debounce(async function(this: any) {
      await this.saveConfig()
    }, 1000)
  }
})
