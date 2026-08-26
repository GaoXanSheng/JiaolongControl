<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { Message } from '@arco-design/web-vue'
import SettingCardComponent from '@/components/common/SettingCardComponent.vue'
import { useConfigStore } from '@/stores/config'

const configStore = useConfigStore()
const loading = ref(false)
const value = computed(() => configStore.config?.Fan.FanCurveMerge ?? false)

onMounted(() => configStore.fetchConfig())

async function onChange(value: string | number | boolean) {
  if (typeof value !== 'boolean' || !configStore.config) return
  const prev = configStore.config.Fan.FanCurveMerge
  loading.value = true
  try {
    configStore.config.Fan.FanCurveMerge = value
    const res = await configStore.saveConfig()
    if (!res?.Success) {
      configStore.config.Fan.FanCurveMerge = prev
      Message.error(res?.Message || '保存失败')
    }
  } catch (e) {
    configStore.config.Fan.FanCurveMerge = prev
    Message.error('保存失败')
    console.error(e)
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <setting-card-component
    title="风扇曲线合并"
    description="启用后，软件将在【风扇曲线】页面中将所有风扇的曲线合并为一条曲线，方便用户统一调整风扇转速"
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
