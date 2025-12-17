<script setup lang="ts">
import RightSide from '@/components/rightSide.vue'
import useStore, { HomeCardType } from '@/stores'
import { HomeTab } from '@/stores/HomeTab.ts'
const store = useStore()

function onClickMenuItem(key: any) {
  store.SwitchPages = key as HomeTab;
}
</script>

<template>
  <a-layout class="layout-container">
    <a-layout-sider collapsible breakpoint="xl" :width="200" class="sider">
      <a-menu :selected-keys="[store.SwitchPages]" @menu-item-click="onClickMenuItem" class="menu">
        <a-menu-item v-for="item in HomeCardType" :key="item.eum">
          <template #icon>
            <img class="menu-icon" :src="item.icon" alt="icon" />
          </template>
          {{ item.title }}
        </a-menu-item>

<!--        <a-menu-item :key="HomeTab.OpenProSettings">-->
<!--          <template #icon>-->
<!--            <img class="menu-icon" :src="information" alt="icon" />-->
<!--          </template>-->
<!--          高级监控-->
<!--        </a-menu-item>-->
      </a-menu>
    </a-layout-sider>

    <a-layout-content class="content">
      <RightSide />
    </a-layout-content>
  </a-layout>
</template>

<style lang="scss" scoped>

.layout-container {
  width: 100vw;
  height: 100vh;
  background: var(--color-bg-1);
}

.sider {
  height: 100%;
  background: var(--color-bg-2);
}

.menu {
  height: 100%;
}

.menu-icon {
  width: 16px;
  height: 16px;
  transition: filter 0.3s;
}

.content {
  display: flex;
  flex-direction: column;
  justify-content: center;
  background: var(--color-bg-3);
  padding: 2px;
  color: var(--color-text-1);
}

:global(body[arco-theme='dark']) .menu-icon {
  filter: invert(100%);
}

:global(body[arco-theme='light']) .menu-icon {
  filter: invert(0);
}
</style>