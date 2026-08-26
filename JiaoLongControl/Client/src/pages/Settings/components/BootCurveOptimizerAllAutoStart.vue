<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { Message } from '@arco-design/web-vue'
import SettingCardComponent from '@/components/common/SettingCardComponent.vue'
import { useConfigStore } from '@/stores/config'

const configStore = useConfigStore()
const loading = ref(false)
const value = computed(() => configStore.config?.App.BootSetRyzenSumCurveOptimizerAll ?? false)

onMounted(() => configStore.fetchConfig())

async function onChange(value: string | number | boolean) {
  if (typeof value !== 'boolean' || !configStore.config) return
  const prev = configStore.config.App.BootSetRyzenSumCurveOptimizerAll
  loading.value = true
  try {
    configStore.config.App.BootSetRyzenSumCurveOptimizerAll = value
    const res = await configStore.saveConfig()
    if (!res?.Success) {
      configStore.config.App.BootSetRyzenSumCurveOptimizerAll = prev
      Message.error(res?.Message || '保存失败')
    }
  } catch (e) {
    configStore.config.App.BootSetRyzenSumCurveOptimizerAll = prev
    Message.error('保存失败')
    console.error(e)
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <setting-card-component
    title="RyzenSMU 全核降压自动应用"
    description="在软件启动时，自动应用【Ryzen SMU】页面中保存的 Curve Optimizer 全核心负压（降压超频）设定"
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
