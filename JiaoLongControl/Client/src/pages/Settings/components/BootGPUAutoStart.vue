<script async setup lang="ts">
import SettingCardComponent from '@/components/common/SettingCardComponent.vue'

import {onMounted, ref} from "vue";
import {Config} from "@/utils/bridge.ts";

const loading = ref(false)
const BootStartAdvancedGPUSystem = ref(false)
onMounted(async () => {
  const config = (await Config.GetAppConfig()).Data
  BootStartAdvancedGPUSystem.value = config.BootAdvancedGPUSystem
})

async function SetBootStartAdvancedGPUSystem(value: string | number | boolean) {
  if (typeof value !== 'boolean') return
  loading.value = true
  const config = (await Config.GetAppConfig()).Data
  config.BootAdvancedGPUSystem = value;
  await Config.SetAppConfig(config)
  BootStartAdvancedGPUSystem.value = value
  loading.value = false
}
</script>

<template>
  <setting-card-component title="GPU 参数自动应用" description="在软件启动时，自动载入并应用【GPU】设置页面中保存的核心与显存超频、电压曲线、功耗目标等参数。">
    <template #extra>
      <a-switch
          :model-value="BootStartAdvancedGPUSystem"
          :loading="loading"
          @change="SetBootStartAdvancedGPUSystem($event)"
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