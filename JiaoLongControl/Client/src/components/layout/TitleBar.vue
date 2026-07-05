<script setup lang="ts">
import { Window } from '@/utils/bridge'

function handleMouseDown(e: MouseEvent) {
  if (e.button === 0) {
    Window.Drag()
  }
}
</script>

<template>
  <div class="title-bar">
    <div class="drag-region" @mousedown="handleMouseDown">
      <img src="@/assets/icon/icon.png" class="logo" alt="logo" />
      <span class="title">JiaoLong Control</span>
    </div>
    <div class="window-actions">
      <div class="action-btn" @click="Window.Minimize()">
        <icon-minus />
      </div>
      <div class="action-btn" @click="Window.Maximize()">
        <icon-fullscreen />
      </div>
      <div class="action-btn close" @click="Window.Close()">
        <icon-close />
      </div>
    </div>
  </div>
</template>

<style scoped lang="scss">
.title-bar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  height: 50px;
  user-select: none;

  .drag-region {
    flex: 1;
    height: 100%;
    display: flex;
    align-items: center;
    padding-left: 12px;
    -webkit-app-region: drag;

    .logo {
      width: 16px;
      height: 16px;
      margin-right: 8px;
    }

    .title {
      color: white;
      font-size: 12px;
      font-weight: 500;
    }
  }

  .window-actions {
    display: flex;

    -webkit-app-region: no-drag;

    .action-btn {
      display: flex;
      align-items: center;
      justify-content: center;
      height: 50px;
      width: 50px;
      cursor: pointer;
      transition: background-color 0.2s;
      font-size: 14px;

      &:hover {
        background-color: var(--color-fill-3);
      }

      &.close:hover {
        background-color: #e81123;
        color: white;
      }
    }
  }
}
</style>
