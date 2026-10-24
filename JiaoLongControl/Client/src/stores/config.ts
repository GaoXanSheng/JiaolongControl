import {defineStore} from 'pinia'
import {Config} from '@/utils/bridge'
import type {JiaoLongConfigType} from '@/types/config'

function debounce(fn: (...args: unknown[]) => unknown, delay: number) {
  let timer: ReturnType<typeof setTimeout> | null = null
  return function (this: unknown, ...args: unknown[]) {
    if (timer) clearTimeout(timer)
    timer = setTimeout(() => {
      fn.apply(this, args)
    }, delay)
  }
}

let fetchPromise: Promise<void> | null = null

export const useConfigStore = defineStore('config', {
  state: () => ({
    config: null as JiaoLongConfigType | null,
  }),

  actions: {
    async fetchConfig(force = false) {
      if (this.config && !force) return

      if (fetchPromise) return fetchPromise

      fetchPromise = (async () => {
        try {
          const result = await Config.GetConfig()
          if (result.Success && result.Data) {
            this.config = result.Data
          } else {
            console.warn('获取配置失败:', result.Message)
          }
        } catch (err) {
          console.warn('获取配置失败:', err)
        } finally {
          fetchPromise = null
        }
      })()
      return fetchPromise
    },
    async saveConfig() {
      if (!this.config) return
      return await Config.SetConfig(this.config)
    },
    async refresh() {
      await this.fetchConfig(true)
    },

    debouncedSave: debounce(async function (this: { saveConfig: () => Promise<unknown> }) {
      try {
        await this.saveConfig()
      } catch (e) {
        console.error('配置保存失败', e)
      }
    }, 1000),
  },
})
