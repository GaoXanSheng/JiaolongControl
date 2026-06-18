<script setup lang="ts">
import { ref, computed } from 'vue'
import { Message } from '@arco-design/web-vue'
import { AutoFanControl, Fan } from '@/utils/bridge'
import { useConfigStore } from '@/stores/config'
import FanSpeed from "@/components/ProSettingComponent/FanCurve/FanSpeed.vue";

const loading = ref(false)
const visible = ref(false)
const configStore = useConfigStore()

const FanPageStore = computed(() => configStore.config?.FanPageStore)

const handleClick = () => {
  if (!FanPageStore.value) return
  if (FanPageStore.value.FanSpeed > 5800 || FanPageStore.value.FanSpeed < 1500) {
    visible.value = true
  } else {
    handleOk()
  }
}

const handleOk = async () => {
  if (!FanPageStore.value) return
  visible.value = false
  loading.value = true
  const isRunningRes = await AutoFanControl.IsRunning()
  if (isRunningRes.Success && isRunningRes.Data) {
    await AutoFanControl.Stop()
  }
  const res = await Fan.SetFanSpeed(FanPageStore.value.FanSpeed)
  res.Success ? Message.success(res.Message) : Message.error(res.Message)
  configStore.debouncedSave()
  loading.value = false
}

const handleCancel = () => {
  visible.value = false
}

async function handleRemoveFanClick() {
  const isRunningRes = await AutoFanControl.IsRunning()
  if (isRunningRes.Success && isRunningRes.Data) {
    await AutoFanControl.Stop()
  }
  const res = await Fan.RemoveFanSpeed()
  res.Success ? Message.success(res.Message) : Message.error(res.Message)
}
</script>

<template>
  <div class="p-6 h-full overflow-y-auto bg-gradient-to-br from-[#11121A] to-[#0D0E15] text-white" v-if="FanPageStore">
    <div class="mb-8">
      <h1 class="text-3xl font-bold tracking-tight">风扇控制</h1>
      <p class="text-gray-400 mt-1">手动调节风扇转速或恢复自动控制</p>
    </div>

    <div class="grid grid-cols-1 gap-8 max-w-2xl">
      <!-- 转速调节卡片 -->
      <div class="bg-[#1A1B26]/80 backdrop-blur-md border border-white/5 p-8 rounded-3xl shadow-2xl">
        <div class="flex flex-col gap-6">
          <div class="flex justify-between items-end">
            <div>
              <span class="text-sm font-medium text-purple-400 uppercase tracking-wider">Manual Control</span>
              <h2 class="text-xl font-bold mt-1">目标转速设定</h2>
            </div>
            <div class="text-right">
              <span class="text-4xl font-black font-mono text-white">{{ FanPageStore.FanSpeed }}</span>
              <span class="text-gray-500 ml-2 font-bold">RPM</span>
            </div>
          </div>

          <a-slider
            v-model="FanPageStore.FanSpeed"
            :min="0"
            :max="8000"
            :step="100"
            class="mt-4"
            :style="{ '--color-primary-6': '#8A2BE2' }"
          />

          <div class="grid grid-cols-2 gap-4 mt-4">
            <a-button 
              type="primary" 
              size="large"
              class="!h-12 !bg-purple-600 !border-none hover:!bg-purple-500 !rounded-xl font-bold shadow-lg shadow-purple-900/20"
              :loading="loading" 
              @click="handleClick"
            >
              应用设定
            </a-button>
            <a-button 
              size="large"
              class="!h-12 !bg-white/5 !border-white/10 hover:!bg-white/10 !text-white !rounded-xl font-bold"
              @click="handleRemoveFanClick"
            >
              移除限制
            </a-button>
          </div>
        </div>
      </div>

      <!-- 实时状态展示 -->
      <FanSpeed />

      <a-modal v-model:visible="visible" @ok="handleOk" @cancel="handleCancel" simple>
        <template #title>安全警告</template>
        <div class="text-gray-300">
          设定转速高于 <span class="text-red-500 font-bold">5800 RPM</span> 或低于 <span class="text-red-500 font-bold">1500 RPM</span> 可能会导致系统硬件异常或噪音过大。请确认您的操作。
        </div>
      </a-modal>
    </div>
  </div>
  <div v-else class="flex items-center justify-center h-full">
    <a-spin dot />
  </div>
</template>

<style lang="scss" scoped>
:deep(.arco-slider-button) {
  width: 16px;
  height: 16px;
  border-width: 3px;
  border-color: #8A2BE2;
  background-color: #fff;
}
:deep(.arco-slider-bar) {
  height: 6px;
  background-color: #8A2BE2;
}
:deep(.arco-slider-rail) {
  height: 6px;
  background-color: rgba(255, 255, 255, 0.1);
}
</style>