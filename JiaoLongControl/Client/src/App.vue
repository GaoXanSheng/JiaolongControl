<script setup lang="ts">
import { onMounted, onUnmounted } from 'vue'
import { useConfigStore } from '@/stores/config'
import { useSystemInfoStore } from '@/stores/systemInfo'

const systemInfoStore = useSystemInfoStore()
const configStore = useConfigStore()

let stopPolling: () => void

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
    }
  } catch {
    // 来自 WebView2 外部消息, 非 JSON 时忽略
  }
}

onMounted(() => {
  stopPolling = systemInfoStore.startPolling()
  window.chrome?.webview?.addEventListener('message', onWebViewMessage)
  setTimeout(() => {
    hideAppLoader()
  }, 300)
})

onUnmounted(() => {
  if (stopPolling) {
    stopPolling()
  }
  window.chrome?.webview?.removeEventListener('message', onWebViewMessage)
})
</script>

<template>
  <Suspense>
    <template #default>
      <router-view class="app-view"></router-view>
    </template>
    <template #fallback>
      <div class="loading-state">Loading Hardware Info...</div>
    </template>
  </Suspense>
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

.loading-state {
  display: flex;
  justify-content: center;
  align-items: center;
  height: 100vh;
  color: #fff;
  background-color: #0d0e15;
}
</style>
