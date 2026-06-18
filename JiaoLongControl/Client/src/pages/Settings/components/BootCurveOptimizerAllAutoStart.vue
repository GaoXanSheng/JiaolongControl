<script async setup lang="ts">
import SettingCardComponent from '@/components/common/SettingCardComponent.vue'

import {ref} from "vue";
import {Config} from "@/utils/bridge.ts";

const loading = ref(false)

const config = (await Config.GetConfig()).Data
const BootStart = ref(config.BootSetRyzenSumCurveOptimizerAll)
async function SetBootStart(value: string | number | boolean) {
  if (typeof value !== 'boolean') return
  loading.value = true
  const config = (await Config.GetConfig()).Data
  config.BootSetRyzenSumCurveOptimizerAll = value;
  await Config.SetConfig(config)
  BootStart.value = value
  loading.value = false
}
</script>

<template>
  <setting-card-component title="RyzenSMU 全核降压自动应用" description="在软件启动时，自动应用【Ryzen SMU】页面中保存的 Curve Optimizer 全核心负压（降压超频）设定。">
    <template #extra>
      <a-switch
          :model-value="BootStart"
          :loading="loading"
          @change="SetBootStart($event)"
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