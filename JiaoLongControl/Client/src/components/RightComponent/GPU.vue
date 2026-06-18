<script setup lang="ts">
import { ref, computed } from "vue";
import { Message } from "@arco-design/web-vue";
import { NvidiaGpu } from "@/utils/bridge.ts";
import { useConfigStore } from '@/stores/config'
import GPUDirectConnection from "@/components/RightComponent/setting/GPUDirectConnection.vue";
import GPUUnlockDB from "@/components/RightComponent/setting/GPUUnlockDB.vue";

const configStore = useConfigStore()
const loading = ref(false)

const GPUData = computed(() => configStore.config?.NvidiaGpuConfig)

async function SetGPUClock() {
  if (!GPUData.value) return
  loading.value = true
  const res = await NvidiaGpu.LockGpuClock(GPUData.value.GpuClock)
  res.Success ? Message.success(res.Message) : Message.error(res.Message)
  configStore.debouncedSave()
  loading.value = false
}

async function RemoveGPUClock() {
  loading.value = true
  const res = await NvidiaGpu.ResetGpuClock()
  res.Success ? Message.success(res.Message) : Message.error(res.Message)
  configStore.debouncedSave()
  loading.value = false
}

async function SetGPUMemoryClock() {
  if (!GPUData.value) return
  loading.value = true
  const res = await NvidiaGpu.LockMemoryClock(GPUData.value.MemoryClock)
  res.Success ? Message.success(res.Message) : Message.error(res.Message)
  configStore.debouncedSave()
  loading.value = false
}

async function RemoveGPUMemoryClock() {
  loading.value = true
  const res = await NvidiaGpu.ResetMemoryClock()
  res.Success ? Message.success(res.Message) : Message.error(res.Message)
  configStore.debouncedSave()
  loading.value = false
}

async function SetGPUPower() {
  if (!GPUData.value) return
  loading.value = true
  const res = await NvidiaGpu.SetPowerLimit(GPUData.value.PowerLimit)
  res.Success ? Message.success(res.Message) : Message.error(res.Message)
  configStore.debouncedSave()
  loading.value = false
}
</script>

<template>
  <div class="p-6 h-full overflow-y-auto bg-gradient-to-br from-[#11121A] to-[#0D0E15] text-white" v-if="GPUData">
    <div class="mb-8">
      <h1 class="text-3xl font-bold tracking-tight">GPU 设置</h1>
      <p class="text-gray-400 mt-1">优化显卡核心频率、显存频率及功耗释放</p>
    </div>

    <div class="grid grid-cols-1 lg:grid-cols-2 gap-6 max-w-6xl mb-8">
      <GPUDirectConnection />
      <GPUUnlockDB />
    </div>

    <div class="grid grid-cols-1 gap-4 max-w-4xl">
      <div v-for="item in [
        { label: '核心时钟频率', key: 'GpuClock', min: 0, max: 3000, unit: 'MHz', action: SetGPUClock, reset: RemoveGPUClock },
        { label: '显存时钟频率', key: 'MemoryClock', min: 0, max: 10000, unit: 'MHz', action: SetGPUMemoryClock, reset: RemoveGPUMemoryClock },
        { label: '功率限制 (TGP)', key: 'PowerLimit', min: 0, max: 140, unit: 'W', action: SetGPUPower }
      ]" :key="item.key" 
      class="bg-[#1A1B26]/60 border border-white/5 p-6 rounded-2xl hover:bg-[#1A1B26]/80 transition-all duration-300 shadow-lg">
        <div class="flex flex-col gap-4">
          <div class="flex justify-between items-center">
            <span class="text-sm font-semibold text-gray-300 uppercase tracking-wider">{{ item.label }}</span>
            <div class="flex items-center gap-2">
              <span class="text-2xl font-bold font-mono text-blue-400">{{ GPUData[item.key] }}</span>
              <span class="text-xs text-gray-500 font-bold">{{ item.unit }}</span>
            </div>
          </div>
          
          <div class="flex items-center gap-6">
            <a-slider
              v-model="GPUData[item.key]"
              :min="item.min"
              :max="item.max"
              class="flex-1"
              :style="{ '--color-primary-6': '#3B82F6' }"
            />
            <div class="flex gap-2">
              <a-button 
                v-if="item.reset"
                size="small"
                class="!bg-white/5 !border-white/10 hover:!bg-white/10 !text-gray-300 rounded-lg px-4"
                :loading="loading" 
                @click="item.reset"
              >
                重置
              </a-button>
              <a-button 
                type="primary" 
                size="small"
                class="!bg-blue-600 !border-none hover:!bg-blue-500 rounded-lg px-6 font-bold"
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
  border-color: #3B82F6;
}
:deep(.arco-slider-bar) {
  background-color: #3B82F6;
}
</style>
