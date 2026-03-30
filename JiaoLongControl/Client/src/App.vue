<script setup lang="ts">
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