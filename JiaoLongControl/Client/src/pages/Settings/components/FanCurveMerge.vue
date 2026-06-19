<script setup lang="ts">
import {ref} from "vue";

const loading = ref(false)

const config = (await Config.GetFanConfig()).Data
const FanCurveMerge = ref(config.FanCurveMerge)
import SettingCardComponent from '@/components/common/SettingCardComponent.vue'

import {Config} from "@/utils/bridge.ts";
async function setFanCurveMerge(value: string | number | boolean) {
  if (typeof value !== 'boolean') return
  loading.value = true
  const config = (await Config.GetFanConfig()).Data
  config.FanCurveMerge = value;
  await Config.SetFanConfig(config)
  FanCurveMerge.value = value
  loading.value = false
}
</script>

<template>
  <setting-card-component title="切换为合并风扇曲线模式" description="使风扇转速同步">
    <template #extra>
      <a-switch
          :model-value="FanCurveMerge"
          :loading="loading"
          @change="setFanCurveMerge($event)"
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