<script async setup lang="ts">
import SettingCardComponent from "@/components/RightComponent/setting/SettingCardComponent.vue";
import {onMounted, ref} from "vue";
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
  <setting-card-component title="BootSetRyzenSumCurveOptimizerAll">
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