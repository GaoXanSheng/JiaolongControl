<script setup lang="ts">
import {ref} from "vue";

const loading = ref(false)

const fullResult = await Config.GetConfig()
const FanCurveMerge = ref(fullResult.Data.Fan.FanCurveMerge)
import SettingCardComponent from '@/components/common/SettingCardComponent.vue'

import {Config} from '@/utils/bridge';
async function setFanCurveMerge(value: string | number | boolean) {
  if (typeof value !== 'boolean') return
  loading.value = true
  const fullResult = await Config.GetConfig()
  const config = fullResult.Data
  config.Fan.FanCurveMerge = value;
  await Config.SetConfig(config)
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