<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { Message } from '@arco-design/web-vue'
import SettingCardComponent from '@/components/common/SettingCardComponent.vue'
import { useConfigStore } from '@/stores/config'

const configStore = useConfigStore()
const loading = ref(false)
const value = computed(() => configStore.config?.App.BootAdvancedFanControlSystem ?? false)

onMounted(() => configStore.fetchConfig())

async function onChange(value: string | number | boolean) {
  if (typeof value !== 'boolean' || !configStore.config) return
  const prev = configStore.config.App.BootAdvancedFanControlSystem
  loading.value = true
  try {
    configStore.config.App.BootAdvancedFanControlSystem = value
    const res = await configStore.saveConfig()
    if (!res?.Success) {
      configStore.config.App.BootAdvancedFanControlSystem = prev
      Message.error(res?.Message || '保存失败')
    }
  } catch (e) {
    configStore.config.App.BootAdvancedFanControlSystem = prev
    Message.error('保存失败')
    console.error(e)
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <setting-card-component
    title="自启动高级风扇控制系统"
    description="启用后，软件将在后台实时监控硬件温度，并依据【风扇曲线】页面中用户自定义的策略来动态调整风扇转速"
  >
    <template #extra>
      <a-switch :model-value="value" :loading="loading" @change="onChange($event)">
        <template #checked-icon>
          <icon-check />
        </template>
        <template #unchecked-icon>
          <icon-close />
        </template>
      </a-switch>
    </template>
  </setting-card-component>
</template>

<style scoped lang="scss"></style>
