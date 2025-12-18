<script async setup lang="ts">
import SettingCardComponent from '@/components/RightComponent/setting/SettingCardComponent.vue'
import {onMounted, ref} from 'vue'
import bridge, {CPU} from '@/utils/bridge.ts'

const cpuboost = ref()
onMounted(async () => {
  cpuboost.value = await bridge.CPU.GetCustomMode();
});
const loading = ref(false)

async function CpuBoost_handleClick() {
  loading.value = true
  await CPU.SetCustomMode(cpuboost.value)
  loading.value = false
}
</script>

<template>
  <setting-card-component title="CPU增压">
    <template #extra>
      <a-switch v-model="cpuboost" :loading="loading" @click="CpuBoost_handleClick">
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

<style scoped></style>
