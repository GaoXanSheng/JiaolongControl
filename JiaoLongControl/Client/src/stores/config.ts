import { defineStore } from 'pinia'
import { Config } from '@/utils/bridge'
import type { JiaoLongConfigType } from '@/types/config'

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
    loading: false,
    error: null as string | null,
  }),

  actions: {
    async fetchConfig(force = false) {
      if (this.config && !force) return

      if (fetchPromise) {
        await fetchPromise.catch(() => {})
        if (fetchPromise) return fetchPromise
      }

      this.loading = true
      fetchPromise = (async () => {
        try {
          const result = await Config.GetConfig()
          if (result.Success && result.Data) {
            this.config = result.Data
            this.error = null
          } else {
            this.error = result.Message || '获取配置失败'
          }
        } catch (err) {
          this.error = err instanceof Error ? err.message : 'Failed to fetch config'
        } finally {
          this.loading = false
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
