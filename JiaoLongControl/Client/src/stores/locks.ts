import {defineStore} from 'pinia'
import type {OsdLockStates} from '@/utils/bridge'

/**
 * 四项锁定状态 (大写/数字/功能键/触摸板) 的实时镜像。
 * 数据来源:
 * 1) C# 检测到状态变化时经 WebView 推送 lock-states-changed 消息 (App.vue 分发到 apply)
 * 2) 常规设置页挂载时的 GetLockStates 拉取
 */
export const useLocksStore = defineStore('locks', {
  state: () => ({
    locks: null as OsdLockStates | null,
  }),
  actions: {
    /** 合并推送/拉取到的状态; 单项读取失败 (null/缺省) 时保留旧值 */
    apply(data: Partial<Record<keyof OsdLockStates, boolean | null>> | null | undefined) {
      if (!data) return
      const cur = this.locks
      this.locks = {
        CapsLock: data.CapsLock ?? cur?.CapsLock ?? false,
        NumLock: data.NumLock ?? cur?.NumLock ?? false,
        FnLock: data.FnLock ?? cur?.FnLock ?? null,
        TouchpadLock: data.TouchpadLock ?? cur?.TouchpadLock ?? null,
      }
    },
  },
})
