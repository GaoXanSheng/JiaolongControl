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

function withTimeout<T>(p: Promise<T>, ms: number): Promise<T> {
  return Promise.race([
    p,
    new Promise<T>((_, reject) => setTimeout(() => reject(new Error('bridge 响应超时')), ms)),
  ])
}

export const useConfigStore = defineStore('config', {
  state: () => ({
    config: null as JiaoLongConfigType | null,
  }),

  actions: {
    async fetchConfig(force = false) {
      if (this.config && !force) return

      if (fetchPromise) return fetchPromise

      fetchPromise = (async () => {
        for (let attempt = 1; attempt <= 3; attempt++) {
          try {
            const result = await withTimeout(Config.GetConfig(), 3000)
            if (result.Success && result.Data) {
              this.config = result.Data
            } else {
              console.warn('获取配置失败:', result.Message)
            }
            return
          } catch (err) {
            if (attempt === 3) {
              console.warn('获取配置失败:', err)
            } else {
              await new Promise((r) => setTimeout(r, 500))
            }
          }
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
