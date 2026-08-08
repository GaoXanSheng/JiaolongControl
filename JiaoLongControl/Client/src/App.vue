<script setup lang="ts">
import { onMounted, onUnmounted } from 'vue'
import { useConfigStore } from '@/stores/config'
import { useSystemInfoStore } from '@/stores/systemInfo'

const systemInfoStore = useSystemInfoStore()
const configStore = useConfigStore()

let stopPolling: () => void;

function hideAppLoader() {
  const loader = document.getElementById('app-loader')
  if (loader) {
    loader.classList.add('fade-out')
    setTimeout(() => {
      loader.remove()
    }, 450)
  }
}

// 后端配置变更通知（ConfigWatcher 触发）：同步刷新本地配置，避免界面显示过期值
function onWebViewMessage(e: MessageEvent) {
  try {
    const data = typeof e.data === 'string' ? JSON.parse(e.data) : e.data
    if (data && data.type === 'config-changed') {
      configStore.refreshIfUnmodified()
    }
  } catch {
    // 忽略非 JSON 消息（窗口控制消息等）
  }
}

onMounted(() => {
  stopPolling = systemInfoStore.startPolling()
  // 订阅后端 WebView 消息（config-changed 等）
  window.chrome?.webview?.addEventListener('message', onWebViewMessage)
  // 在 DOM 挂载和数据初始化后平滑淡出遮罩
  setTimeout(() => {
    hideAppLoader()
  }, 300)
})

onUnmounted(() => {
  if (stopPolling) {
    stopPolling();
  }
  window.chrome?.webview?.removeEventListener('message', onWebViewMessage)
})
</script>

<template>
  <!-- 使用 Suspense 处理异步组件加载 -->
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
  background-color: #0D0E15;
}
</style>