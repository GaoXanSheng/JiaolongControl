<script setup lang="ts">
import { onMounted } from 'vue'
import useStore from '@/stores'

const store = useStore()

function loadConfig() {
  Object.keys(store.$state).forEach((key) => {
    const value = localStorage.getItem(key)
    if (value) {
      try {
        // @ts-ignore
        store.$state[key] = JSON.parse(value)
      } catch (e) { console.error('Config load error', e) }
    }
  })
  // 强制关闭 RGB 循环状态，因为启动时后端也是关闭的
  store.RgbEventLoop = false

  store.$subscribe(() => {
    Object.keys(store.$state).forEach((key) => {
      // @ts-ignore
      localStorage.setItem(key, JSON.stringify(store.$state[key]))
    })
  })
}

onMounted(() => {
  loadConfig()
  document.body.setAttribute('arco-theme', store.theme)
  // 移除 index.html 可能存在的 loader
  const loader = document.querySelector('.loader')
  if (loader) loader.remove()
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

<style lang="scss">
body {
  margin: 0;
  overflow: hidden; // 禁止 WebView 出现滚动条
  font-family: 'Segoe UI', sans-serif;
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
  background-color: #202020;
}
</style>