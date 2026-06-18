<script setup lang="ts">
import { ref, computed } from "vue";
import { Message } from "@arco-design/web-vue";
import { NvidiaGpu } from "@/utils/bridge.ts";
import { useConfigStore } from '@/stores/config'

const configStore = useConfigStore()
const loading = ref(false)

// 绑定 Store 中的 GPU 数据
const GPUData = computed(() => configStore.config?.NvidiaGpuConfig)

// 页面内控模拟状态（确保设计图中的非物理控制项在 UI 中能够完美交互且不发生编译报错）
const selectedGpu = ref('rtx4060')
const currentMode = ref('performance') // 性能模式切换：quiet | balance | performance | custom
const gpuVoltageOffset = ref(0)       // 核心电压偏移
const gpuTempLimit = ref(83)          // 温度限制
const fanSpeedMode = ref('auto')       // 风扇转速

// 统一应用设置逻辑
async function handleApplyAll() {
  if (!GPUData.value) return
  loading.value = true
  try {
    // 1. 设置核心频率限制
    await NvidiaGpu.LockGpuClock(GPUData.value.GpuClock)
    // 2. 设置显存频率限制
    await NvidiaGpu.LockMemoryClock(GPUData.value.MemoryClock)
    // 3. 设置功耗限制
    await NvidiaGpu.SetPowerLimit(GPUData.value.PowerLimit)

    configStore.debouncedSave()
    Message.success('显卡设置已成功应用并保存')
  } catch (error) {
    Message.error('应用设置失败，请检查显卡驱动及桥接服务')
  } finally {
    loading.value = false
  }
}

// 统一重置逻辑
async function handleResetAll() {
  loading.value = true
  try {
    await NvidiaGpu.ResetGpuClock()
    await NvidiaGpu.ResetMemoryClock()

    // 重设 UI 预设
    if (GPUData.value) {
      GPUData.value.GpuClock = 120
      GPUData.value.MemoryClock = 500
      GPUData.value.PowerLimit = 100
    }
    gpuVoltageOffset.value = 0
    gpuTempLimit.value = 80
    currentMode.value = 'balance'

    Message.info('显卡参数已恢复至默认设置')
  } catch (error) {
    Message.error('重置失败')
  } finally {
    loading.value = false
  }
}

function handleCancel() {
  configStore.loadConfig?.()
  Message.info('已取消本次修改')
}

// 快速应用下方的预设方案
function applyPreset(preset: string) {
  if (!GPUData.value) return
  if (preset === 'default') {
    GPUData.value.GpuClock = 0
    GPUData.value.MemoryClock = 0
    GPUData.value.PowerLimit = 100
    gpuTempLimit.value = 80
  } else if (preset === 'game') {
    GPUData.value.GpuClock = 150
    GPUData.value.MemoryClock = 600
    GPUData.value.PowerLimit = 110
    gpuTempLimit.value = 85
  } else if (preset === 'creative') {
    GPUData.value.GpuClock = 100
    GPUData.value.MemoryClock = 800
    GPUData.value.PowerLimit = 105
    gpuTempLimit.value = 83
  } else if (preset === 'eco') {
    GPUData.value.GpuClock = -50
    GPUData.value.MemoryClock = -200
    GPUData.value.PowerLimit = 80
    gpuTempLimit.value = 75
  }
  Message.success(`预设「${preset}」参数已加载`);
}
</script>

<template>
  <div class="h-full overflow-y-auto text-white p-6 no-scrollbar" v-if="GPUData">
    <div class="max-w-[1300px] mx-auto flex flex-col lg:flex-row gap-6">

      <!-- ==================== 左/中：显卡主要设置区 ==================== -->
      <div class="flex-1 space-y-6">
        <!-- 头部标题 -->
        <div>
          <h1 class="text-2xl font-bold tracking-wide">GPU 设置</h1>
          <p class="text-[13px] text-gray-500 mt-1">调整 GPU 的性能参数，发挥显卡最佳性能。</p>
        </div>

        <!-- 1. 选择 GPU 与卡片详情 -->
        <div class="bg-[#121320]/60 backdrop-blur-md border border-white/[0.05] rounded-xl p-5 shadow-lg flex flex-col md:flex-row justify-between gap-6">
          <div class="space-y-3 md:w-1/2">
            <span class="text-[11px] text-gray-500 font-semibold block uppercase">选择 GPU</span>
            <a-select v-model="selectedGpu" class="w-full select-dark" :bordered="false">
              <a-option value="rtx4060">
                <span class="flex items-center gap-2">
                  <!-- 绿色 NVIDIA 圆点指示器 -->
                  <span class="w-1.5 h-1.5 rounded-full bg-[#76B900]"></span>
                  NVIDIA GeForce RTX 4060
                </span>
              </a-option>
            </a-select>
          </div>

          <!-- 驱动与显存等参数规格详情 -->
          <div class="grid grid-cols-2 gap-x-6 gap-y-3 text-[11px] text-gray-400 md:w-1/2 md:border-l md:border-white/[0.05] md:pl-6 pt-1">
            <div>
              <span class="text-gray-600 block mb-0.5">驱动版本</span>
              <span class="text-white font-medium font-mono">551.86</span>
            </div>
            <div>
              <span class="text-gray-600 block mb-0.5">显存容量</span>
              <span class="text-white font-medium font-mono">8 GB GDDR6</span>
            </div>
            <div>
              <span class="text-gray-600 block mb-0.5">驱动日期</span>
              <span class="text-white font-medium font-mono">2024-05-10</span>
            </div>
            <div>
              <span class="text-gray-600 block mb-0.5">总线宽度</span>
              <span class="text-white font-medium font-mono">128 bit</span>
            </div>
          </div>
        </div>
        <!-- 3. 核心设置（偏移与限制调节） -->
        <div class="bg-[#121320]/60 backdrop-blur-md border border-white/[0.05] rounded-xl p-5 shadow-lg space-y-5">
          <h2 class="text-[13px] font-semibold text-gray-300">核心设置</h2>

          <div class="space-y-5">
            <!-- 核心频率偏移 -->
            <div class="space-y-2">
              <div class="flex justify-between items-center text-xs">
                <span class="text-gray-300 flex items-center gap-1">核心频率偏移 <span class="text-gray-500 cursor-pointer text-[10px]">ⓘ</span></span>
                <span class="text-purple-400 font-medium font-mono">+{{ GPUData.GpuClock }} MHz</span>
              </div>
              <a-slider v-model="GPUData.GpuClock" :min="0" :max="500" class="w-full" />
            </div>

            <!-- 显存频率偏移 -->
            <div class="space-y-2">
              <div class="flex justify-between items-center text-xs">
                <span class="text-gray-300 flex items-center gap-1">显存频率偏移 <span class="text-gray-500 cursor-pointer text-[10px]">ⓘ</span></span>
                <span class="text-purple-400 font-medium font-mono">+{{ GPUData.MemoryClock }} MHz</span>
              </div>
              <a-slider v-model="GPUData.MemoryClock" :min="0" :max="1500" class="w-full" />
            </div>

            <!-- 核心电压偏移 (模拟核心限制) -->
            <div class="space-y-2">
              <div class="flex justify-between items-center text-xs">
                <span class="text-gray-300 flex items-center gap-1">核心电压偏移 <span class="text-gray-500 cursor-pointer text-[10px]">ⓘ</span></span>
                <span class="text-purple-400 font-medium font-mono">{{ gpuVoltageOffset }} mV</span>
              </div>
              <a-slider v-model="gpuVoltageOffset" :min="-100" :max="100" class="w-full" />
            </div>

            <!-- 功耗限制 -->
            <div class="space-y-2">
              <div class="flex justify-between items-center text-xs">
                <span class="text-gray-300 flex items-center gap-1">功耗限制 <span class="text-gray-500 cursor-pointer text-[10px]">ⓘ</span></span>
                <span class="text-purple-400 font-medium font-mono">{{ GPUData.PowerLimit }} %</span>
              </div>
              <a-slider v-model="GPUData.PowerLimit" :min="50" :max="140" class="w-full" />
            </div>
          </div>
        </div>

        <!-- 5. 全局应用控制栏 -->
        <div class="flex justify-between items-center pt-2">
          <button
              @click="handleResetAll"
              class="flex items-center gap-2 text-xs text-gray-400 hover:text-white border border-white/10 hover:border-white/20 bg-white/[0.02] hover:bg-white/[0.05] px-4 py-2 rounded-lg transition-colors"
          >
            <svg class="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 1121.21 7.89M9 11l3-3 3 3m-3-3v12" />
            </svg>
            重置
          </button>

          <div class="flex gap-3">
            <button
                @click="handleCancel"
                class="text-xs text-gray-400 hover:text-white border border-white/5 bg-transparent hover:bg-white/[0.03] px-5 py-2 rounded-lg transition-colors"
            >
              取消
            </button>
            <button
                @click="handleApplyAll"
                :disabled="loading"
                class="text-xs font-medium text-white bg-gradient-to-r from-purple-700 to-indigo-600 hover:from-purple-600 hover:to-indigo-500 disabled:opacity-50 px-6 py-2 rounded-lg transition-all shadow-[0_0_15px_rgba(138,43,226,0.3)]"
            >
              {{ loading ? '应用中...' : '应用' }}
            </button>
          </div>
        </div>

      </div>

      <!-- ==================== 右侧：显卡信息与实时监控栏 ==================== -->
      <div class="w-full lg:w-[360px] shrink-0 space-y-6">

        <!-- 1. GPU 信息详情卡 -->
        <div class="bg-[#121320]/60 backdrop-blur-md border border-white/[0.05] rounded-xl p-5 shadow-lg">
          <h2 class="text-[13px] font-semibold text-gray-300 mb-4">GPU 信息</h2>

          <div class="flex items-center gap-4">
            <!-- 显卡结构高保真 SVG -->
            <div class="w-16 h-14 bg-white/[0.02] border border-white/[0.05] rounded-xl flex items-center justify-center relative shrink-0">
              <svg class="w-12 h-10 text-purple-500/80 opacity-80" viewBox="0 0 80 50" fill="none" stroke="currentColor" stroke-width="1.2">
                <rect x="5" y="10" width="70" height="30" rx="3" />
                <circle cx="25" cy="25" r="9" />
                <circle cx="55" cy="25" r="9" stroke-dasharray="3 2" />
                <circle cx="25" cy="25" r="2" fill="currentColor" />
                <circle cx="55" cy="25" r="2" fill="currentColor" />
                <line x1="15" y1="40" x2="45" y2="40" stroke-width="2" />
                <line x1="75" y1="15" x2="75" y2="35" stroke-width="1.5" />
                <path d="M8,15 L12,15 M8,20 L12,20 M8,25 L12,25" />
              </svg>
            </div>

            <div class="space-y-1 text-[11px] text-gray-400 w-full">
              <div class="text-[12px] font-bold text-white">NVIDIA GeForce RTX 4060</div>
              <div class="grid grid-cols-2 gap-y-1 gap-x-2 pt-1 font-mono">
                <div>核心代号: <span class="text-white">AD107</span></div>
                <div>工艺制程: <span class="text-white">5 nm</span></div>
                <div>核心频率: <span class="text-white">1830 MHz</span></div>
                <div>Boost 频率: <span class="text-white">2460 MHz</span></div>
                <div>显存频率: <span class="text-white">15000 MHz</span></div>
                <div>CUDA 核心: <span class="text-white">3072</span></div>
              </div>
            </div>
          </div>
        </div>

        <!-- 2. 实时监控面板（带 Sparklines 迷你波形图） -->
        <div class="bg-[#121320]/60 backdrop-blur-md border border-white/[0.05] rounded-xl p-5 shadow-lg space-y-4">
          <div class="flex justify-between items-center">
            <h2 class="text-[13px] font-semibold text-gray-300">实时监控</h2>
            <button class="bg-white/[0.04] border border-white/10 hover:bg-white/[0.08] text-[9px] text-gray-400 hover:text-white px-2 py-0.5 rounded transition">隐藏图表</button>
          </div>

          <!-- 2x3 网格状态块 -->
          <div class="grid grid-cols-2 gap-3">

            <!-- GPU 使用率 -->
            <div class="bg-white/[0.02] border border-white/[0.04] p-3 rounded-lg flex flex-col justify-between">
              <div>
                <span class="text-[10px] text-gray-500 block">GPU 使用率</span>
                <span class="text-base font-bold text-white font-mono">68 <span class="text-[10px] text-gray-500 font-bold">%</span></span>
              </div>
              <svg class="w-full h-8 opacity-70 mt-1" viewBox="0 0 160 40" preserveAspectRatio="none">
                <defs>
                  <linearGradient id="g-purple" x1="0" y1="0" x2="0" y2="1"><stop offset="0%" stop-color="#8A2BE2" stop-opacity="0.3" /><stop offset="100%" stop-color="#8A2BE2" stop-opacity="0" /></linearGradient>
                </defs>
                <path d="M0,32 C20,30 30,10 45,20 C60,30 75,5 90,25 C105,40 120,12 135,10 C150,8 160,22 160,22" fill="none" stroke="#8A2BE2" stroke-width="1.2" />
                <path d="M0,32 C20,30 30,10 45,20 C60,30 75,5 90,25 C105,40 120,12 135,10 C150,8 160,22 160,22 L160,40 L0,40 Z" fill="url(#g-purple)" />
              </svg>
            </div>

            <!-- 显存使用率 -->
            <div class="bg-white/[0.02] border border-white/[0.04] p-3 rounded-lg flex flex-col justify-between">
              <div>
                <span class="text-[10px] text-gray-500 block">显存使用率</span>
                <span class="text-base font-bold text-white font-mono">42 <span class="text-[10px] text-gray-500 font-bold">%</span></span>
              </div>
              <svg class="w-full h-8 opacity-70 mt-1" viewBox="0 0 160 40" preserveAspectRatio="none">
                <defs>
                  <linearGradient id="g-blue" x1="0" y1="0" x2="0" y2="1"><stop offset="0%" stop-color="#3B82F6" stop-opacity="0.3" /><stop offset="100%" stop-color="#3B82F6" stop-opacity="0" /></linearGradient>
                </defs>
                <path d="M0,28 C15,35 30,15 45,25 C60,35 75,12 90,20 C105,28 120,10 135,18 C150,25 160,15 160,15" fill="none" stroke="#3B82F6" stroke-width="1.2" />
                <path d="M0,28 C15,35 30,15 45,25 C60,35 75,12 90,20 C105,28 120,10 135,18 C150,25 160,15 160,15 L160,40 L0,40 Z" fill="url(#g-blue)" />
              </svg>
            </div>

            <!-- 核心频率 -->
            <div class="bg-white/[0.02] border border-white/[0.04] p-3 rounded-lg flex flex-col justify-between">
              <div>
                <span class="text-[10px] text-gray-500 block">核心频率</span>
                <span class="text-base font-bold text-white font-mono">2145 <span class="text-[9px] text-gray-500 font-bold">MHz</span></span>
              </div>
              <svg class="w-full h-8 opacity-70 mt-1" viewBox="0 0 160 40" preserveAspectRatio="none">
                <path d="M0,35 Q15,10 30,30 T60,10 T90,25 T120,5 T150,20 L160,20" fill="none" stroke="#8A2BE2" stroke-width="1.2" />
                <path d="M0,35 Q15,10 30,30 T60,10 T90,25 T120,5 T150,20 L160,20 L160,40 L0,40 Z" fill="url(#g-purple)" />
              </svg>
            </div>

            <!-- 显存频率 -->
            <div class="bg-white/[0.02] border border-white/[0.04] p-3 rounded-lg flex flex-col justify-between">
              <div>
                <span class="text-[10px] text-gray-500 block">显存频率</span>
                <span class="text-base font-bold text-white font-mono">8501 <span class="text-[9px] text-gray-500 font-bold">MHz</span></span>
              </div>
              <svg class="w-full h-8 opacity-70 mt-1" viewBox="0 0 160 40" preserveAspectRatio="none">
                <path d="M0,25 Q20,15 40,25 T80,25 T120,20 T160,25" fill="none" stroke="#3B82F6" stroke-width="1.2" />
                <path d="M0,25 Q20,15 40,25 T80,25 T120,20 T160,25 L160,40 L0,40 Z" fill="url(#g-blue)" />
              </svg>
            </div>

            <!-- GPU 温度 -->
            <div class="bg-white/[0.02] border border-white/[0.04] p-3 rounded-lg flex flex-col justify-between">
              <div>
                <span class="text-[10px] text-gray-500 block">GPU 温度</span>
                <span class="text-base font-bold text-white font-mono">62 <span class="text-[10px] text-gray-500 font-bold">°C</span></span>
              </div>
              <svg class="w-full h-8 opacity-70 mt-1" viewBox="0 0 160 40" preserveAspectRatio="none">
                <defs>
                  <linearGradient id="g-green" x1="0" y1="0" x2="0" y2="1"><stop offset="0%" stop-color="#10B981" stop-opacity="0.3" /><stop offset="100%" stop-color="#10B981" stop-opacity="0" /></linearGradient>
                </defs>
                <path d="M0,32 C20,30 40,22 60,35 C80,20 100,32 120,22 C140,25 160,20 160,20" fill="none" stroke="#10B981" stroke-width="1.2" />
                <path d="M0,32 C20,30 40,22 60,35 C80,20 100,32 120,22 C140,25 160,20 160,20 L160,40 L0,40 Z" fill="url(#g-green)" />
              </svg>
            </div>

            <!-- 风扇转速 -->
            <div class="bg-white/[0.02] border border-white/[0.04] p-3 rounded-lg flex flex-col justify-between">
              <div>
                <span class="text-[10px] text-gray-500 block">风扇转速</span>
                <span class="text-base font-bold text-white font-mono">1360 <span class="text-[9px] text-gray-500 font-bold">RPM</span></span>
              </div>
              <svg class="w-full h-8 opacity-70 mt-1" viewBox="0 0 160 40" preserveAspectRatio="none">
                <path d="M0,30 C30,15 60,35 90,20 C120,25 140,15 160,30" fill="none" stroke="#3B82F6" stroke-width="1.2" />
                <path d="M0,30 C30,15 60,35 90,20 C120,25 140,15 160,30 L160,40 L0,40 Z" fill="url(#g-blue)" />
              </svg>
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
/* 隐藏滚动条 */
.no-scrollbar::-webkit-scrollbar {
  display: none;
}
.no-scrollbar {
  -ms-overflow-style: none;
  scrollbar-width: none;
}

/* 重塑 Arco Slider 轨道样式（由蓝色变为极具赛博霓虹感的渐变色） */
:deep(.arco-slider-bar) {
  background: linear-gradient(90deg, #6366f1 0%, #8A2BE2 100%) !important;
  height: 5px !important;
  border-radius: 99px;
}
:deep(.arco-slider-track) {
  background-color: rgba(255, 255, 255, 0.04) !important;
  height: 5px !important;
  border-radius: 99px;
}
:deep(.arco-slider-button) {
  background-color: #ffffff !important;
  border: 2.5px solid #8A2BE2 !important;
  width: 13px !important;
  height: 13px !important;
  box-shadow: 0 0 10px rgba(138, 43, 226, 0.7) !important;
}

/* 重塑深色模式下拉选择菜单 */
:deep(.select-dark .arco-select-view-single) {
  background-color: #17192a !important;
  border: 1px solid rgba(255, 255, 255, 0.05) !important;
  color: #ffffff !important;
  border-radius: 8px !important;
  height: 32px !important;
}

:deep(.arco-switch-checked) {
  background-color: #8A2BE2 !important;
}
</style>