<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { Message } from '@arco-design/web-vue'
import SettingCardComponent from '@/components/common/SettingCardComponent.vue'
import { useConfigStore } from '@/stores/config'

const props = defineProps<{
  title: string
  description: string
  /** config JSON 内的布尔字段路径, 如 "App.BootAdvancedCPUSystem" */
  configPath: string
}>()

const configStore = useConfigStore()
const loading = ref(false)

onMounted(() => configStore.fetchConfig())

function readValue(): boolean | undefined {
  let node: unknown = configStore.config
  for (const seg of props.configPath.split('.')) {
    if (node == null || typeof node !== 'object') return undefined
    node = (node as Record<string, unknown>)[seg]
  }
  return typeof node === 'boolean' ? node : undefined
}

const value = computed(() => readValue() ?? false)

function writePath(v: unknown) {
  const segs = props.configPath.split('.')
  let node = configStore.config as unknown as Record<string, unknown>
  for (let i = 0; i < segs.length - 1; i++) {
    node = (node[segs[i] ?? ''] ?? {}) as Record<string, unknown>
  }
  node[segs[segs.length - 1] ?? ''] = v
}

async function onChange(next: string | number | boolean) {
  if (typeof next !== 'boolean' || !configStore.config) return
  const prev = readValue()
  loading.value = true
  try {
    writePath(next)
    const res = await configStore.saveConfig()
    if (!res?.Success) {
      writePath(prev)
      Message.error(res?.Message || '保存失败')
    }
  } catch (e) {
    writePath(prev)
    Message.error('保存失败')
    console.error(e)
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <setting-card-component :title="title" :description="description">
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
