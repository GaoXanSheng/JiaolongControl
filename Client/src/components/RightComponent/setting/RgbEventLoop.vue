<script setup lang="ts">
import useStore from '@/stores'
import SettingCardComponent from './SettingCardComponent.vue'
import { ref } from 'vue'
import { Bridge } from '@/utils/bridge'

const store = useStore()
const loading = ref(false)

async function RGB_handleClick() {
  loading.value = true
  try {
    await Bridge.invoke('RgbEventLoop', { enabled: store.RgbEventLoop })
  } catch (e) {
    store.RgbEventLoop = !store.RgbEventLoop
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <setting-card-component title="RGB键盘灯循环 (彩虹)">
    <template #extra>
      <a-switch v-model="store.RgbEventLoop" :loading="loading" @change="RGB_handleClick">
        <template #checked-icon><icon-check /></template>
        <template #unchecked-icon><icon-close /></template>
      </a-switch>
    </template>
  </setting-card-component>
</template>