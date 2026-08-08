<script setup lang="ts">
import {computed, onMounted, ref} from "vue";
import {Message} from '@arco-design/web-vue'
import SettingCardComponent from '@/components/common/SettingCardComponent.vue'
import {useConfigStore} from '@/stores/config'

const configStore = useConfigStore()
const loading = ref(false)
const value = computed(() => configStore.config?.App.BootAdvancedGPUSystem ?? false)

onMounted(() => configStore.fetchConfig())

async function onChange(value: string | number | boolean) {
  if (typeof value !== 'boolean' || !configStore.config) return
  const prev = configStore.config.App.BootAdvancedGPUSystem
  loading.value = true
  try {
    configStore.config.App.BootAdvancedGPUSystem = value
    const res = await configStore.saveConfig()
    if (!res?.Success) {
      configStore.config.App.BootAdvancedGPUSystem = prev
      Message.error(res?.Message || '保存失败')
    }
  } catch (e) {
    configStore.config.App.BootAdvancedGPUSystem = prev
    Message.error('保存失败')
    console.error(e)
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <setting-card-component title="GPU 参数自动应用" description="在软件启动时，自动载入并应用【GPU】设置页面中保存的核心与显存超频、电压曲线、功耗目标等参数">
    <template #extra>
      <a-switch
          :model-value="value"
          :loading="loading"
          @change="onChange($event)"
      >
        <template #checked-icon>
          <icon-check/>
        </template>
        <template #unchecked-icon>
          <icon-close/>
        </template>
      </a-switch>
    </template>
  </setting-card-component>
</template>

<style scoped lang="scss">

</style>
