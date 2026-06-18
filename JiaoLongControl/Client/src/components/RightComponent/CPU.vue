<script setup lang="ts">
import { ref, computed } from 'vue'
import { Message } from '@arco-design/web-vue'
import { CPU, Power } from '@/utils/bridge.ts'
import { useConfigStore } from '@/stores/config'
import SettingCardComponent from "@/components/RightComponent/setting/SettingCardComponent.vue";

const loading = ref(false)
const configStore = useConfigStore()

// 使用 computed 来简化对配置项的访问，并确保响应性
const CPUData = computed(() => configStore.config?.AdvancedCPUSystemConfig)

async function SetCpuLongPower() {
  if (!CPUData.value) return
  loading.value = true
  const res = await CPU.SetCpuLongPower(CPUData.value.CpuLongPower)
  res.Success ? Message.success(res.Message) : Message.error(res.Message)
  configStore.debouncedSave()
  loading.value = false
}

async function SetCPUTempWall() {
  if (!CPUData.value) return
  loading.value = true
  const res = await CPU.SetCPUTempWall(CPUData.value.CpuTempWall)
  res.Success ? Message.success(res.Message) : Message.error(res.Message)
  configStore.debouncedSave()
  loading.value = false
}

async function SetCpuShortPower() {
  if (!CPUData.value) return
  loading.value = true
  const res = await CPU.SetCpuShortPower(CPUData.value.CpuShortPower)
  res.Success ? Message.success(res.Message) : Message.error(res.Message)
  configStore.debouncedSave()
  loading.value = false
}

async function SetCPUMaxFrequency() {
  if (!CPUData.value) return
  loading.value = true
  const res = await Power.SetCPUMaxFrequency(CPUData.value.CpuMaxFrequency)
  res.Success ? Message.success(res.Message) : Message.error(res.Message)
  configStore.debouncedSave()
  loading.value = false
}

async function SetCPUTurbo() {
  if (!CPUData.value) return
  loading.value = true
  if (!CPUData.value.CpuTurbo) {
    const res = await Power.DisableTurbo()
    res.Success ? Message.success(res.Message) : Message.error(res.Message)
  } else {
    const res = await Power.EnableTurbo()
    res.Success ? Message.success(res.Message) : Message.error(res.Message)
  }
  configStore.debouncedSave()
  loading.value = false
}
</script>

<template>
  <div class="p-6 h-full overflow-y-auto bg-gradient-to-br from-[#11121A] to-[#0D0E15] text-white" v-if="CPUData">
    <div class="mb-8">
      <h1 class="text-3xl font-bold tracking-tight">CPU 设置</h1>
      <p class="text-gray-400 mt-1">控制处理器功耗上限、频率及睿频状态</p>
    </div>

    <div class="grid grid-cols-1 gap-6 max-w-4xl">
      <!-- 睿频开关卡片 -->
      <div class="bg-[#1A1B26]/80 backdrop-blur-md border border-white/5 p-6 rounded-2xl shadow-xl hover:border-purple-500/30 transition-all duration-300">
        <div class="flex justify-between items-center">
          <div>
            <h3 class="text-lg font-medium">CPU 睿频 (Turbo Boost)</h3>
            <p class="text-sm text-gray-500">开启以允许 CPU 在负载时超越基础频率</p>
          </div>
          <a-switch
            v-model="CPUData.CpuTurbo"
            :loading="loading"
            @change="SetCPUTurbo"
          >
            <template #checked-icon><icon-check/></template>
            <template #unchecked-icon><icon-close/></template>
          </a-switch>
        </div>
      </div>

      <!-- 滑块卡片组 -->
      <div class="space-y-4">
        <div v-for="item in [
          { label: '短时 CPU 功耗 (PL2)', key: 'CpuShortPower', min: 30, max: 255, unit: 'W', action: SetCpuShortPower },
          { label: '长时 CPU 功耗 (PL1)', key: 'CpuLongPower', min: 30, max: 255, unit: 'W', action: SetCpuLongPower },
          { label: '温度墙 (T-Junction)', key: 'CpuTempWall', min: 1, max: 100, unit: '°C', action: SetCPUTempWall },
          { label: '最大 CPU 频率', key: 'CpuMaxFrequency', min: 0, max: 5400, unit: 'MHz', action: SetCPUMaxFrequency }
        ]" :key="item.key" 
        class="bg-[#1A1B26]/60 border border-white/5 p-6 rounded-2xl hover:bg-[#1A1B26]/80 transition-colors">
          <div class="flex flex-col gap-4">
            <div class="flex justify-between items-center">
              <span class="text-sm font-semibold text-gray-300">{{ item.label }}</span>
              <span class="text-purple-400 font-mono">{{ CPUData[item.key] }}{{ item.unit }}</span>
            </div>
            <div class="flex items-center gap-6">
              <a-slider
                v-model="CPUData[item.key]"
                :min="item.min"
                :max="item.max"
                class="flex-1"
                :style="{ '--color-primary-6': '#8A2BE2' }"
              />
              <a-button 
                type="primary" 
                size="small"
                class="!bg-purple-600 !border-none hover:!bg-purple-500 rounded-lg px-6"
                :loading="loading" 
                @click="item.action"
              >
                应用
              </a-button>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
  <div v-else class="flex items-center justify-center h-full">
    <a-spin dot />
  </div>
</template>

<style lang="scss" scoped>
:deep(.arco-slider-button) {
  border-color: #8A2BE2;
}
:deep(.arco-slider-bar) {
  background-color: #8A2BE2;
}
</style>
