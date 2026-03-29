<script async setup lang="ts">
import SettingCardComponent from "@/components/RightComponent/setting/SettingCardComponent.vue";
import {onMounted, ref} from "vue";
import {Config} from "@/utils/bridge.ts";

const loading = ref(false)
const BootStartAdvancedGPUSystem = ref(false)
onMounted(async () => {
  const config = (await Config.GetConfig()).Data
  BootStartAdvancedGPUSystem.value = config.BootAdvancedGPUSystem
})

async function SetBootStartAdvancedGPUSystem(value: string | number | boolean) {
  if (typeof value !== 'boolean') return
  loading.value = true
  const config = (await Config.GetConfig()).Data
  config.BootAdvancedGPUSystem = value;
  await Config.SetConfig(config)
  BootStartAdvancedGPUSystem.value = value
  loading.value = false
}
</script>

<template>
  <setting-card-component title="启用GPU参数自动应用">
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