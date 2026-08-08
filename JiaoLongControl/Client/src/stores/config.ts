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

// 模块级状态（不进入响应式 state）：并发拉取去重 + 未保存修改检测
let fetchPromise: Promise<void> | null = null
let lastSavedJson = ''

/**
 * 配置单一数据源。
 * 所有页面/组件统一通过本 store 读写配置，避免各处自行 GetConfig/SetConfig
 * 造成「基于旧副本整包回写」的并发覆盖。后端配置变化通过 config-changed 消息
 * 驱动 refreshIfUnmodified() 同步。
 */
export const useConfigStore = defineStore('config', {
  state: () => ({
    config: null as JiaoLongConfigType | null,
    loading: false,
    error: null as string | null,
  }),

  actions: {
    /**
     * 拉取后端配置。
     * - 已有本地配置时（非 force）直接返回，避免重复请求；
     * - 并发调用共享同一个进行中的请求；force 时等待在途请求结束再重新拉取，确保拿到最新值；
     * - 拉取在途期间若本地配置被修改，返回时放弃覆盖，避免丢失用户正在编辑的内容。
     */
    async fetchConfig(force = false) {
      if (this.config && !force) return

      // 发起时的配置基线：必须在等待在途请求之前捕获，
      // 否则等待窗口内的编辑会逃过 dirty 检测导致覆盖
      const baseline = this.config ? JSON.stringify(this.config) : null

      if (fetchPromise) {
        await fetchPromise.catch(() => {})
        // finally 会将 fetchPromise 置 null；兜底防御
        if (fetchPromise) return fetchPromise
      }

      this.loading = true
      fetchPromise = (async () => {
        try {
          const result = await Config.GetConfig()
          if (result.Success && result.Data) {
            const dirty = this.config !== null && baseline !== null &&
              JSON.stringify(this.config) !== baseline
            if (!dirty) {
              this.config = result.Data
              lastSavedJson = JSON.stringify(result.Data)
            }
            this.error = null
          } else {
            this.error = result.Message || '获取配置失败'
          }
        } catch (err: any) {
          this.error = err.message || 'Failed to fetch config'
        } finally {
          this.loading = false
          fetchPromise = null
        }
      })()
      return fetchPromise
    },

    /**
     * 保存当前配置（整包回写后端）；成功后更新未保存基线。
     * 返回后端结果，失败时调用方应回滚本地修改并提示用户。
     */
    async saveConfig() {
      if (!this.config) return
      const result = await Config.SetConfig(this.config)
      if (result.Success) {
        lastSavedJson = JSON.stringify(this.config)
      }
      return result
    },

    /**
     * 收到后端 config-changed 消息时调用：
     * 仅当本地没有未保存的修改时才重新拉取，避免覆盖用户正在编辑的内容。
     */
    async refreshIfUnmodified() {
      if (this.config && JSON.stringify(this.config) !== lastSavedJson) return
      await this.fetchConfig(true)
    },

    debouncedSave: debounce(async function (this: any) {
      try {
        await this.saveConfig()
      } catch (e) {
        console.error('配置保存失败', e)
      }
    }, 1000),
  }
})
