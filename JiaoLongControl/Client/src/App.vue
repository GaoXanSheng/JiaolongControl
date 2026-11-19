<script setup lang="ts">
import {onMounted, onUnmounted, watch} from 'vue'
import {useConfigStore} from '@/stores/config'
import {useLocksStore} from '@/stores/locks'
import {useSystemInfoStore} from '@/stores/systemInfo'
import {applyTheme} from '@/theme/theme'
import {timing} from '@/utils/timing'

const systemInfoStore = useSystemInfoStore()
const configStore = useConfigStore()
const locksStore = useLocksStore()

let stopPolling: (() => void) | null = null
let pollDelayTimer: ReturnType<typeof setTimeout> | null = null

function hideAppLoader() {
  const loader = document.getElementById('app-loader')
  if (loader) {
    loader.classList.add('fade-out')
    setTimeout(() => {
      loader.remove()
    }, 450)
  }
}

function onWebViewMessage(e: MessageEvent) {
  try {
    const data = typeof e.data === 'string' ? JSON.parse(e.data) : e.data
    if (data && data.type === 'config-changed') {
      configStore.refresh()
    } else if (data && data.type === 'lock-states-changed') {
      locksStore.apply(data)
    }
  } catch {
    // 来自 WebView2 外部消息, 非 JSON 时忽略
  }
}

// 主题跟随配置(含 config-changed 触发的 refresh); 配置拉取失败时保持
// index.html 内联脚本按 localStorage 缓存设置的主题
watch(
  () => configStore.config?.App.Theme,
  (mode) => {
    if (mode) applyTheme(mode)
  },
  {immediate: true},
)

onMounted(() => {
  pollDelayTimer = setTimeout(() => {
    stopPolling = systemInfoStore.startPolling()
  }, 800)
  window.chrome?.webview?.addEventListener('message', onWebViewMessage)
  hideAppLoader()
  timing('App onMounted (loader hidden)')
})

onUnmounted(() => {
  if (pollDelayTimer) {
    clearTimeout(pollDelayTimer)
    pollDelayTimer = null
  }
  stopPolling?.()
  window.chrome?.webview?.removeEventListener('message', onWebViewMessage)
})
</script>

<template>
  <router-view class="app-view"></router-view>
</template>

<style>
body {
  margin: 0;
  overflow: hidden;
  font-family: 'Inter', 'Segoe UI', sans-serif;
}

.app-view {
  user-select: none;
  width: 100vw;
  height: 100vh;
}
</style>
