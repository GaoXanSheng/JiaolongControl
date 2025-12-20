<script async setup lang="ts">
import SettingCardComponent from "@/components/RightComponent/setting/SettingCardComponent.vue";
import {onMounted, ref} from "vue";
import {Config} from "@/utils/bridge.ts";

const loading = ref(false)
const EnableAdvancedFanControlSystem = ref(false)
const BootStartAdvancedFanControlSystem = ref(false)
onMounted(async () => {
  const config = await Config.GetConfig()
  EnableAdvancedFanControlSystem.value = config.EnableAdvancedFanControlSystem
  BootStartAdvancedFanControlSystem.value = config.BootStartAdvancedFanControlSystem
})

async function SetEnableAdvancedFanControlSystem(value: string | number | boolean) {
  if (typeof value !== 'boolean') return
  loading.value = true
  const config = await Config.GetConfig()
  config.EnableAdvancedFanControlSystem = value;
  await Config.SetConfig(config)
  EnableAdvancedFanControlSystem.value = value
  loading.value = false
}

async function SetBootStartAdvancedFanControlSystem(value: string | number | boolean) {
  if (typeof value !== 'boolean') return
  loading.value = true
  const config = await Config.GetConfig()
  config.BootStartAdvancedFanControlSystem = value;
  await Config.SetConfig(config)
  BootStartAdvancedFanControlSystem.value = value
  loading.value = false
}
</script>

<template>
  <setting-card-component title="启用高级风扇控制系统">
    <template #extra>
      <a-switch
          :model-value="EnableAdvancedFanControlSystem"
          :loading="loading"
          @change="SetEnableAdvancedFanControlSystem($event)"
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
  <setting-card-component v-if="EnableAdvancedFanControlSystem" title="自启动高级风扇控制系统">
    <template #extra>
      <a-switch
          :model-value="BootStartAdvancedFanControlSystem"
          :loading="loading"
          @change="SetBootStartAdvancedFanControlSystem($event)"
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