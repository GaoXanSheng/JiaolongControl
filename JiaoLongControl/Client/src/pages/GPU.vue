<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, type Ref, watch } from "vue";
import { Message } from "@arco-design/web-vue";
import { NvidiaGpu } from "@/utils/bridge.ts";
import { useConfigStore } from '@/stores/config'
import { useSystemInfoStore } from "@/stores/systemInfo";
import { storeToRefs } from "pinia";

const configStore = useConfigStore()
const systemInfoStore = useSystemInfoStore()
const {
  gpuName, gpuDriverVersion, gpuMemoryTotal, gpuBusWidth,
  gpuUtilization, gpuMemoryUtilization, gpuCoreClock, gpuMemoryClock,
  gpuTemp, gpuFanSpeed
} = storeToRefs(systemInfoStore)
const loading = ref(false)
const showAdvanced = ref(false)

if (!configStore.config) {
  await configStore.fetchConfig()
}

// --- Sparkline Chart History ---
const historyLength = 20;
const utilHistory = ref<number[]>(Array(historyLength).fill(0));
const memUtilHistory = ref<number[]>(Array(historyLength).fill(0));
const coreClockHistory = ref<number[]>(Array(historyLength).fill(0));
const memClockHistory = ref<number[]>(Array(historyLength).fill(0));
const tempHistory = ref<number[]>(Array(historyLength).fill(0));
const fanSpeedHistory = ref<number[]>(Array(historyLength).fill(0));

const updateHistory = (history: Ref<number[]>, value: number, divisor = 1) => {
  history.value.push(value / divisor);
  if (history.value.length > historyLength) {
    history.value.shift();
  }
};

let unwatch: (() => void) | null = null;

onMounted(() => {
  updateHistory(utilHistory, gpuUtilization.value);
  updateHistory(memUtilHistory, gpuMemoryUtilization.value);
  updateHistory(coreClockHistory, gpuCoreClock.value, 100);
  updateHistory(memClockHistory, gpuMemoryClock.value, 100);
  updateHistory(tempHistory, gpuTemp.value);
  updateHistory(fanSpeedHistory, gpuFanSpeed.value, 100);

  const stopWatchers = [
    watch(gpuUtilization, v => updateHistory(utilHistory, v)),
    watch(gpuMemoryUtilization, v => updateHistory(memUtilHistory, v)),
    watch(gpuCoreClock, v => updateHistory(coreClockHistory, v, 100)),
    watch(gpuMemoryClock, v => updateHistory(memClockHistory, v, 100)),
    watch(gpuTemp, v => updateHistory(tempHistory, v)),
    watch(gpuFanSpeed, v => updateHistory(fanSpeedHistory, v, 100)),
  ];
  unwatch = () => stopWatchers.forEach(fn => fn());
});

onUnmounted(() => {
  if (unwatch) unwatch();
});


// --- Chart Generation ---
function generateSvgPath(history: number[], yMax: number, smooth = true) {
  if (history.length < 2) return {line: 'M 0 40', area: 'M 0 40'};

  const width = 160;
  const height = 40;

  const points = history.map((value, index) => {
    const x = (index / (historyLength - 1)) * width;
    const y = height - (Math.max(0, Math.min(value, yMax)) / yMax) * height;
    return {x, y};
  });

  const linePath = points.map((p, i) => {
    if (i === 0) return `M ${p.x},${p.y}`;
    if (smooth) {
      const prev = points[i - 1];
      const cp1x = prev.x + (p.x - prev.x) / 2;
      const cp1y = prev.y;
      const cp2x = prev.x + (p.x - prev.x) / 2;
      const cp2y = p.y;
      return `C ${cp1x},${cp1y} ${cp2x},${cp2y} ${p.x},${p.y}`;
    }
    return `L ${p.x},${p.y}`;
  }).join(' ');

  const areaPath = `${linePath} L ${width},${height} L 0,${height} Z`;

  return {line: linePath, area: areaPath};
}

const utilChart = computed(() => generateSvgPath(utilHistory.value, 100));
const memUtilChart = computed(() => generateSvgPath(memUtilHistory.value, 100));
const coreClockChart = computed(() => generateSvgPath(coreClockHistory.value, 30)); // Corresponds to 3000 MHz
const memClockChart = computed(() => generateSvgPath(memClockHistory.value, 100)); // Corresponds to 10000 MHz
const tempChart = computed(() => generateSvgPath(tempHistory.value, 100));
const fanChart = computed(() => generateSvgPath(fanSpeedHistory.value, 40)); // Corresponds to 4000 RPM

// --- Settings and Presets Logic ---
const GPUData = computed(() => configStore.config?.Gpu)
const gpuVoltageOffset = ref(0)
const gpuClockOffset = ref(120)
const memClockOffset = ref(500)
const coreClockRange = ref({ Min: 0, Max: 500 })
const memClockRange = ref({ Min: 0, Max: 1500 })
const powerLimitRange = ref({ Min: 50, Max: 140 })

async function fetchGpuRanges() {
  try {
    const [core, mem, power] = await Promise.all([
      NvidiaGpu.GetGpuCoreClockRange(),
      NvidiaGpu.GetGpuMemoryClockRange(),
      NvidiaGpu.GetGpuPowerLimitRange(),
    ])
    
    if (core.Success && core.Data) {
      const min = core.Data.Min ?? 0;
      const max = core.Data.Max ?? 500;
      coreClockRange.value = { Min: min, Max: max }
      if (GPUData.value && (GPUData.value.GpuClock < min || GPUData.value.GpuClock > max)) {
        GPUData.value.GpuClock = max;
      }
    }
    
    if (mem.Success && mem.Data) {
      const min = mem.Data.Min  ?? 0;
      const max = mem.Data.Max  ?? 1500;
      memClockRange.value = { Min: min, Max: max }
      if (GPUData.value && (GPUData.value.MemoryClock < min || GPUData.value.MemoryClock > max)) {
        GPUData.value.MemoryClock = max;
      }
    }
    
    if (power.Success && power.Data) {
      const min = power.Data.Min ?? 50;
      const max = power.Data.Max ?? 140;
      powerLimitRange.value = { Min: min, Max: max }
      if (GPUData.value && (GPUData.value.PowerLimit < min || GPUData.value.PowerLimit > max)) {
        GPUData.value.PowerLimit = max;
      }
    }
  } catch (err) {
    console.error('Failed to fetch GPU ranges', err)
  }
}

await fetchGpuRanges();

async function handleApplyNormal() {
  if (!GPUData.value) return;
  loading.value = true;
  try {
    await NvidiaGpu.LockGpuClock(GPUData.value.GpuClock);
    await NvidiaGpu.LockMemoryClock(GPUData.value.MemoryClock);
    // 功耗限制：笔记本 TGP 由固件/EC 管理，NvAPI 接口在笔记本上不可用，故不启用
    // await NvidiaGpu.SetPowerLimit(GPUData.value.PowerLimit);
    await configStore.saveConfig()
    Message.success('常规设置已应用并保存');
  } catch (error) {
    Message.error('应用失败，请检查显卡驱动及桥接服务');
  } finally {
    loading.value = false;
  }
}

async function handleResetNormal() {
  loading.value = true;
  try {
    await NvidiaGpu.ResetGpuClock();
    await NvidiaGpu.ResetMemoryClock();
    if (GPUData.value) {
      GPUData.value.GpuClock = coreClockRange.value.Max
      GPUData.value.MemoryClock = memClockRange.value.Max
      GPUData.value.PowerLimit = powerLimitRange.value.Max
    }
    Message.info('常规设置已恢复默认');
  } catch (error) {
    Message.error('重置失败');
  } finally {
    loading.value = false;
  }
}

async function handleApplyAdvanced() {
  loading.value = true;
  try {
    await configStore.saveConfig()
    Message.success('高级设置已保存');
  } catch (error) {
    Message.error('保存失败');
  } finally {
    loading.value = false;
  }
}

async function handleResetAdvanced() {
  gpuClockOffset.value = 0
  memClockOffset.value = 0
  gpuVoltageOffset.value = 0;
  Message.info('高级设置已恢复默认');
}
</script>

<template>
  <div class="h-full overflow-y-auto text-white p-6 no-scrollbar" v-if="GPUData && gpuName">
    <div class="max-w-[1300px] mx-auto flex flex-col lg:flex-row gap-6">

      <!-- ==================== 左/中：显卡主要设置区 ==================== -->
      <div class="flex-1 space-y-6">
        <!-- 头部标题 -->
        <div>
          <h1 class="text-2xl font-bold tracking-wide">GPU 设置</h1>
          <p class="text-[13px] text-gray-500 mt-1">调整 GPU 的性能参数，发挥显卡最佳性能。</p>
        </div>

        <!-- 1. 选择 GPU 与卡片详情 -->
        <div
            class="bg-[#121320]/60 backdrop-blur-md border border-white/[0.05] rounded-xl p-5 shadow-lg flex flex-col md:flex-row justify-between gap-6 ">
          <div class="space-y-3 md:w-1/2">
            <span class="text-[11px] text-gray-500 font-semibold block uppercase">当前 GPU</span>
            <span class="flex items-center gap-2">
                  <span class="w-1.5 h-1.5 rounded-full bg-[#76B900] font-bold"></span>
                  {{ gpuName }}
                </span>
          </div>

          <div
              class="grid grid-cols-2 gap-x-6 gap-y-3 text-[11px] text-gray-400 md:w-1/2 md:border-l md:border-white/[0.05] md:pl-6 pt-1">
            <div>
              <span class="text-gray-600 block mb-0.5">驱动版本</span>
              <span class="text-white font-medium font-mono">{{ gpuDriverVersion }}</span>
            </div>
            <div>
              <span class="text-gray-600 block mb-0.5">显存容量</span>
              <span class="text-white font-medium font-mono">{{ gpuMemoryTotal }}</span>
            </div>
            <div>
              <span class="text-gray-600 block mb-0.5">驱动日期</span>
              <span class="text-white font-medium font-mono">N/A</span>
            </div>
            <div>
              <span class="text-gray-600 block mb-0.5">总线宽度</span>
              <span class="text-white font-medium font-mono">{{ gpuBusWidth }}</span>
            </div>
          </div>
        </div>
        <!-- 3. 模式切换按钮 -->
        <div class="flex gap-2">
          <button @click="showAdvanced = false"
                  :class="[
                    'flex-1 text-xs font-medium px-4 py-2.5 rounded-lg transition-all border',
                    !showAdvanced
                      ? 'bg-gradient-to-r from-purple-700 to-indigo-600 text-white border-transparent shadow-[0_0_12px_rgba(138,43,226,0.25)]'
                      : 'bg-white/[0.02] text-gray-400 border-white/10 hover:text-white hover:border-white/20'
                  ]">
            常规设置
          </button>
          <button @click="showAdvanced = true"
                  :class="[
                    'flex-1 text-xs font-medium px-4 py-2.5 rounded-lg transition-all border',
                    showAdvanced
                      ? 'bg-gradient-to-r from-purple-700 to-indigo-600 text-white border-transparent shadow-[0_0_12px_rgba(138,43,226,0.25)]'
                      : 'bg-white/[0.02] text-gray-400 border-white/10 hover:text-white hover:border-white/20'
                  ]">
            高级超频
          </button>
        </div>

        <!-- 常规设置面板 -->
        <div v-if="!showAdvanced" class="bg-[#121320]/60 backdrop-blur-md border border-white/[0.05] rounded-xl p-5 shadow-lg space-y-5">
          <div class="space-y-5">
            <div class="space-y-2">
              <div class="flex justify-between items-center text-xs">
                <span class="text-gray-300 flex items-center gap-1">GPU 频率
                   <span class="text-gray-500 cursor-pointer text-[10px]">ⓘ</span>
                </span>
                <span class="text-purple-400 font-medium font-mono">{{ GPUData.GpuClock }} MHz</span>
              </div>
              <a-slider v-model="GPUData.GpuClock" :min="coreClockRange.Min" :max="coreClockRange.Max" class="w-full"/>
            </div>

            <div class="space-y-2">
              <div class="flex justify-between items-center text-xs">
                <span class="text-gray-300 flex items-center gap-1">显存频率 <span
                    class="text-gray-500 cursor-pointer text-[10px]">ⓘ</span></span>
                <span class="text-purple-400 font-medium font-mono">{{ GPUData.MemoryClock }} MHz</span>
              </div>
              <a-slider v-model="GPUData.MemoryClock" :min="memClockRange.Min" :max="memClockRange.Max" class="w-full"/>
            </div>

            <!-- 功耗限制：笔记本 TGP 由固件/EC 管理，驱动接口不可用，暂不提供 -->
            <!-- <div class="space-y-2">
              <div class="flex justify-between items-center text-xs">
                <span class="text-gray-300 flex items-center gap-1">功耗限制 <span
                    class="text-gray-500 cursor-pointer text-[10px]">ⓘ</span></span>
                <span class="text-purple-400 font-medium font-mono">{{ GPUData.PowerLimit }} W</span>
              </div>
              <a-slider v-model="GPUData.PowerLimit" :min="powerLimitRange.Min" :max="powerLimitRange.Max" class="w-full"/>
            </div> -->
          </div>

          <div class="flex justify-between items-center pt-2 border-t border-white/[0.04]">
            <button @click="handleResetNormal"
                    class="flex items-center gap-2 text-xs text-gray-400 hover:text-white border border-white/10 hover:border-white/20 bg-white/[0.02] hover:bg-white/[0.05] px-4 py-2 rounded-lg transition-colors">
              重置
            </button>
            <button @click="handleApplyNormal" :disabled="loading"
                    class="text-xs font-medium text-white bg-gradient-to-r from-purple-700 to-indigo-600 hover:from-purple-600 hover:to-indigo-500 disabled:opacity-50 px-6 py-2 rounded-lg transition-all shadow-[0_0_15px_rgba(138,43,226,0.3)]">
              {{ loading ? '应用中...' : '应用' }}
            </button>
          </div>
        </div>

        <!-- 高级超频面板 -->
        <div v-if="showAdvanced" class="bg-[#121320]/60 backdrop-blur-md border border-white/[0.05] rounded-xl p-5 shadow-lg space-y-5">
          <div class="space-y-5">
            <div class="space-y-2">
              <div class="flex justify-between items-center text-xs">
                <span class="text-gray-300 flex items-center gap-1">核心频率偏移 <span
                    class="text-gray-500 cursor-pointer text-[10px]">ⓘ</span></span>
                <span class="text-purple-400 font-medium font-mono">+{{ gpuClockOffset }} MHz</span>
              </div>
              <a-slider v-model="gpuClockOffset" :min="0" :max="500" class="w-full"/>
            </div>

            <div class="space-y-2">
              <div class="flex justify-between items-center text-xs">
                <span class="text-gray-300 flex items-center gap-1">显存频率偏移 <span
                    class="text-gray-500 cursor-pointer text-[10px]">ⓘ</span></span>
                <span class="text-purple-400 font-medium font-mono">+{{ memClockOffset }} MHz</span>
              </div>
              <a-slider v-model="memClockOffset" :min="0" :max="1500" class="w-full"/>
            </div>

            <div class="space-y-2">
              <div class="flex justify-between items-center text-xs">
                <span class="text-gray-300 flex items-center gap-1">核心电压偏移 <span
                    class="text-gray-500 cursor-pointer text-[10px]">ⓘ</span></span>
                <span class="text-purple-400 font-medium font-mono">{{ gpuVoltageOffset }} mV</span>
              </div>
              <a-slider v-model="gpuVoltageOffset" :min="-100" :max="100" class="w-full"/>
            </div>
          </div>

          <div class="flex justify-between items-center pt-2 border-t border-white/[0.04]">
            <button @click="handleResetAdvanced"
                    class="flex items-center gap-2 text-xs text-gray-400 hover:text-white border border-white/10 hover:border-white/20 bg-white/[0.02] hover:bg-white/[0.05] px-4 py-2 rounded-lg transition-colors">
              重置
            </button>
            <button @click="handleApplyAdvanced" :disabled="loading"
                    class="text-xs font-medium text-white bg-gradient-to-r from-purple-700 to-indigo-600 hover:from-purple-600 hover:to-indigo-500 disabled:opacity-50 px-6 py-2 rounded-lg transition-all shadow-[0_0_15px_rgba(138,43,226,0.3)]">
              {{ loading ? '应用中...' : '应用' }}
            </button>
          </div>
        </div>


      </div>

      <!-- ==================== 右侧：显卡信息与实时监控栏 ==================== -->
      <div class="w-full lg:w-[360px] shrink-0 space-y-6 lg:pt-[115px]">
        <!-- 2. 实时监控面板 -->
        <div class="bg-[#121320]/60 backdrop-blur-md border border-white/[0.05] rounded-xl p-5 shadow-lg space-y-4">
          <div class="flex justify-between items-center"><h2 class="text-[13px] font-semibold text-gray-300">
            实时监控</h2></div>
          <div class="grid grid-cols-2 gap-3">

            <!-- GPU 使用率 -->
            <div class="bg-white/[0.02] border border-white/[0.04] p-3 rounded-lg flex flex-col justify-between">
              <div>
                <span class="text-[10px] text-gray-500 block">GPU 使用率</span>
                <span class="text-base font-bold text-white font-mono">{{ gpuUtilization }} <span
                    class="text-[10px] text-gray-500 font-bold">%</span></span>
              </div>
              <svg class="w-full h-8 opacity-70 mt-1" viewBox="0 0 160 40" preserveAspectRatio="none">
                <defs>
                  <linearGradient id="g-purple" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="0%" stop-color="#8A2BE2" stop-opacity="0.3"/>
                    <stop offset="100%" stop-color="#8A2BE2" stop-opacity="0"/>
                  </linearGradient>
                </defs>
                <path :d="utilChart.line" fill="none" stroke="#8A2BE2" stroke-width="1.2"/>
                <path :d="utilChart.area" fill="url(#g-purple)"/>
              </svg>
            </div>

            <!-- 显存使用率 -->
            <div class="bg-white/[0.02] border border-white/[0.04] p-3 rounded-lg flex flex-col justify-between">
              <div>
                <span class="text-[10px] text-gray-500 block">显存使用率</span>
                <span class="text-base font-bold text-white font-mono">{{ gpuMemoryUtilization }} <span
                    class="text-[10px] text-gray-500 font-bold">%</span></span>
              </div>
              <svg class="w-full h-8 opacity-70 mt-1" viewBox="0 0 160 40" preserveAspectRatio="none">
                <defs>
                  <linearGradient id="g-blue" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="0%" stop-color="#3B82F6" stop-opacity="0.3"/>
                    <stop offset="100%" stop-color="#3B82F6" stop-opacity="0"/>
                  </linearGradient>
                </defs>
                <path :d="memUtilChart.line" fill="none" stroke="#3B82F6" stroke-width="1.2"/>
                <path :d="memUtilChart.area" fill="url(#g-blue)"/>
              </svg>
            </div>

            <!-- 核心频率 -->
            <div class="bg-white/[0.02] border border-white/[0.04] p-3 rounded-lg flex flex-col justify-between">
              <div>
                <span class="text-[10px] text-gray-500 block">核心频率</span>
                <span class="text-base font-bold text-white font-mono">{{ gpuCoreClock }} <span
                    class="text-[9px] text-gray-500 font-bold">MHz</span></span>
              </div>
              <svg class="w-full h-8 opacity-70 mt-1" viewBox="0 0 160 40" preserveAspectRatio="none">
                <path :d="coreClockChart.line" fill="none" stroke="#8A2BE2" stroke-width="1.2"/>
                <path :d="coreClockChart.area" fill="url(#g-purple)"/>
              </svg>
            </div>

            <!-- 显存频率 -->
            <div class="bg-white/[0.02] border border-white/[0.04] p-3 rounded-lg flex flex-col justify-between">
              <div>
                <span class="text-[10px] text-gray-500 block">显存频率</span>
                <span class="text-base font-bold text-white font-mono">{{ gpuMemoryClock }} <span
                    class="text-[9px] text-gray-500 font-bold">MHz</span></span>
              </div>
              <svg class="w-full h-8 opacity-70 mt-1" viewBox="0 0 160 40" preserveAspectRatio="none">
                <path :d="memClockChart.line" fill="none" stroke="#3B82F6" stroke-width="1.2"/>
                <path :d="memClockChart.area" fill="url(#g-blue)"/>
              </svg>
            </div>

            <!-- GPU 温度 -->
            <div class="bg-white/[0.02] border border-white/[0.04] p-3 rounded-lg flex flex-col justify-between">
              <div>
                <span class="text-[10px] text-gray-500 block">GPU 温度</span>
                <span class="text-base font-bold text-white font-mono">{{ gpuTemp }} <span
                    class="text-[10px] text-gray-500 font-bold">°C</span></span>
              </div>
              <svg class="w-full h-8 opacity-70 mt-1" viewBox="0 0 160 40" preserveAspectRatio="none">
                <defs>
                  <linearGradient id="g-green" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="0%" stop-color="#10B981" stop-opacity="0.3"/>
                    <stop offset="100%" stop-color="#10B981" stop-opacity="0"/>
                  </linearGradient>
                </defs>
                <path :d="tempChart.line" fill="none" stroke="#10B981" stroke-width="1.2"/>
                <path :d="tempChart.area" fill="url(#g-green)"/>
              </svg>
            </div>

            <!-- 风扇转速 -->
            <div class="bg-white/[0.02] border border-white/[0.04] p-3 rounded-lg flex flex-col justify-between">
              <div>
                <span class="text-[10px] text-gray-500 block">风扇转速</span>
                <span class="text-base font-bold text-white font-mono">{{ gpuFanSpeed }} <span
                    class="text-[9px] text-gray-500 font-bold">RPM</span></span>
              </div>
              <svg class="w-full h-8 opacity-70 mt-1" viewBox="0 0 160 40" preserveAspectRatio="none">
                <path :d="fanChart.line" fill="none" stroke="#3B82F6" stroke-width="1.2"/>
                <path :d="fanChart.area" fill="url(#g-blue)"/>
              </svg>
            </div>

          </div>
        </div>
      </div>
    </div>
  </div>
  <div v-else class="flex items-center justify-center h-full">
    <a-spin dot/>
  </div>
</template>

<style lang="scss" scoped>
:deep(.arco-select-view-value) {
  color: white;
}

.no-scrollbar::-webkit-scrollbar {
  display: none;
}

.no-scrollbar {
  -ms-overflow-style: none;
  scrollbar-width: none;
}

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
