<script setup lang="ts">
import { onMounted, onUnmounted } from 'vue'
import { useConfigStore } from '@/stores/config'
import { useSystemInfoStore } from '@/stores/systemInfo'

const systemInfoStore = useSystemInfoStore()

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

onMounted(() => {
  stopPolling = systemInfoStore.startPolling()
  // 在 DOM 挂载和数据初始化后平滑淡出遮罩
  setTimeout(() => {
    hideAppLoader()
  }, 300)
})

onUnmounted(() => {
  if (stopPolling) {
    stopPolling();
  }
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