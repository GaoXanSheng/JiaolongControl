<script async setup lang="ts">
import SettingCardComponent from '@/components/common/SettingCardComponent.vue'

import {onMounted, ref} from "vue";
import {Config} from '@/utils/bridge';

const loading = ref(false)
const BootStartAdvancedFanControlSystem = ref(false)
onMounted(async () => {
  const fullResult = await Config.GetConfig()
  BootStartAdvancedFanControlSystem.value = fullResult.Data.App.BootAdvancedFanControlSystem
})

async function SetBootStartAdvancedFanControlSystem(value: string | number | boolean) {
  if (typeof value !== 'boolean') return
  loading.value = true
  const fullResult = await Config.GetConfig()
  const config = fullResult.Data
  config.App.BootAdvancedFanControlSystem = value;
  await Config.SetConfig(config)
  BootStartAdvancedFanControlSystem.value = value
  loading.value = false
}
</script>

<template>
  <setting-card-component title="自启动高级风扇控制系统" description="启用后，软件将在后台实时监控硬件温度，并依据【风扇曲线】页面中用户自定义的策略来动态调整风扇转速">
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