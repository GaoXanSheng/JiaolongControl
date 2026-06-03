<script setup lang="ts">
import {ref} from "vue";

const loading = ref(false)

const config = (await Config.GetConfig()).Data
const FanCurveMerge = ref(config.FanCurveMerge)
import SettingCardComponent from "@/components/RightComponent/setting/SettingCardComponent.vue";
import {Config} from "@/utils/bridge.ts";
async function setFanCurveMerge(value: string | number | boolean) {
  if (typeof value !== 'boolean') return
  loading.value = true
  const config = (await Config.GetConfig()).Data
  config.FanCurveMerge = value;
  await Config.SetConfig(config)
  FanCurveMerge.value = value
  loading.value = false
}
</script>

<template>
  <setting-card-component title="传统风扇曲线">
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