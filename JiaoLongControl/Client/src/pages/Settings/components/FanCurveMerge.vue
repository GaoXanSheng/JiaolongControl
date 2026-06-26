<script setup lang="ts">
import {ref} from "vue";

const loading = ref(false)

const config = (await Config.GetFanConfig()).Data
const FanCurveMerge = ref(config.FanCurveMerge)
import SettingCardComponent from '@/components/common/SettingCardComponent.vue'

import {Config} from '@/utils/bridge.config.gen';
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
  <setting-card-component title="风扇曲线合并" description="启用后，软件将在【风扇曲线】页面中将所有风扇的曲线合并为一条曲线，方便用户统一调整风扇转速">
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