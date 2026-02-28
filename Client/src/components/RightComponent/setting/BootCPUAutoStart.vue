<script async setup lang="ts">
import SettingCardComponent from "@/components/RightComponent/setting/SettingCardComponent.vue";
import {onMounted, ref} from "vue";
import {Config} from "@/utils/bridge.ts";

const loading = ref(false)
const BootStartAdvancedCPUSystem = ref(false)
onMounted(async () => {
  const config = await Config.GetConfig()
  BootStartAdvancedCPUSystem.value = config.BootAdvancedCPUSystem
})

async function SetBootStartAdvancedCPUSystem(value: string | number | boolean) {
  if (typeof value !== 'boolean') return
  loading.value = true
  const config = await Config.GetConfig()
  config.BootAdvancedCPUSystem = value;
  await Config.SetConfig(config)
  BootStartAdvancedCPUSystem.value = value
  loading.value = false
}
</script>

<template>
  <setting-card-component title="启用CPU参数自动应用">
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