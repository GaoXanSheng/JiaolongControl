<script async setup lang="ts">
import SettingCardComponent from '@/components/common/SettingCardComponent.vue'

import {onMounted, ref} from "vue";
import {Config} from '@/utils/bridge';

const loading = ref(false)
const BootStartAdvancedCPUSystem = ref(false)
onMounted(async () => {
  const fullResult = await Config.GetConfig()
  BootStartAdvancedCPUSystem.value = fullResult.Data.App.BootAdvancedCPUSystem
})

async function SetBootStartAdvancedCPUSystem(value: string | number | boolean) {
  if (typeof value !== 'boolean') return
  loading.value = true
  const fullResult = await Config.GetConfig()
  const config = fullResult.Data
  config.App.BootAdvancedCPUSystem = value;
  await Config.SetConfig(config)
  BootStartAdvancedCPUSystem.value = value
  loading.value = false
}
</script>

<template>
  <setting-card-component title="CPU 参数自动应用" description="在软件启动时，自动载入并应用【CPU】设置页面中保存的功耗、频率、温度墙等高级参数">
    <template #extra>
      <a-switch
          :model-value="BootStartAdvancedCPUSystem"
          :loading="loading"
          @change="SetBootStartAdvancedCPUSystem($event)"
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