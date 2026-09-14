<script setup lang="ts">
import {computed, onMounted, onUnmounted, ref, type Ref, watch} from 'vue'
import {Message} from '@arco-design/web-vue'
import {type GpuCurveCapabilitiesInfo, type GpuCurveStatusInfo, NvidiaGpu,} from '@/utils/bridge'
import {buildSparkline} from '@/utils/chart'
import {useConfigStore} from '@/stores/config'
import {useSystemInfoStore} from '@/stores/systemInfo'
import {storeToRefs} from 'pinia'

const configStore = useConfigStore()
const systemInfoStore = useSystemInfoStore()
const {
  gpuName,
  gpuDriverVersion,
  gpuDriverDate,
  gpuMemoryTotal,
  gpuBusWidth,
  gpuUtilization,
  gpuMemoryUtilization,
  gpuCoreClock,
  gpuMemoryClock,
  gpuTemp,
  gpuFanSpeed,
} = storeToRefs(systemInfoStore)
const loading = ref(false)
const showAdvanced = ref(false)

if (!configStore.config) {
  await configStore.fetchConfig()
}

// --- Sparkline Chart History ---
const historyLength = 20
const utilHistory = ref<number[]>(Array(historyLength).fill(0))
const memUtilHistory = ref<number[]>(Array(historyLength).fill(0))
const coreClockHistory = ref<number[]>(Array(historyLength).fill(0))
const memClockHistory = ref<number[]>(Array(historyLength).fill(0))
const tempHistory = ref<number[]>(Array(historyLength).fill(0))
const fanSpeedHistory = ref<number[]>(Array(historyLength).fill(0))

const updateHistory = (history: Ref<number[]>, value: number, divisor = 1) => {
  history.value.push(value / divisor)
  if (history.value.length > historyLength) {
    history.value.shift()
  }
}

let unwatch: (() => void) | null = null

onMounted(() => {
  updateHistory(utilHistory, gpuUtilization.value)
  updateHistory(memUtilHistory, gpuMemoryUtilization.value)
  updateHistory(coreClockHistory, gpuCoreClock.value, 100)
  updateHistory(memClockHistory, gpuMemoryClock.value, 100)
  updateHistory(tempHistory, gpuTemp.value)
  updateHistory(fanSpeedHistory, gpuFanSpeed.value, 100)

  const stopWatchers = [
    watch(gpuUtilization, (v) => updateHistory(utilHistory, v)),
    watch(gpuMemoryUtilization, (v) => updateHistory(memUtilHistory, v)),
    watch(gpuCoreClock, (v) => updateHistory(coreClockHistory, v, 100)),
    watch(gpuMemoryClock, (v) => updateHistory(memClockHistory, v, 100)),
    watch(gpuTemp, (v) => updateHistory(tempHistory, v)),
    watch(gpuFanSpeed, (v) => updateHistory(fanSpeedHistory, v, 100)),
  ]
  unwatch = () => stopWatchers.forEach((fn) => fn())
})

onUnmounted(() => {
  if (unwatch) unwatch()
})

// --- Chart Generation ---
function generateSvgPath(history: number[], yMax: number, smooth = true) {
  return buildSparkline(history, { width: 160, height: 40, max: yMax, smooth, area: true })
}

const utilChart = computed(() => generateSvgPath(utilHistory.value, 100))
const memUtilChart = computed(() => generateSvgPath(memUtilHistory.value, 100))
const coreClockChart = computed(() => generateSvgPath(coreClockHistory.value, 30)) // Corresponds to 3000 MHz
const memClockChart = computed(() => generateSvgPath(memClockHistory.value, 100)) // Corresponds to 10000 MHz
const tempChart = computed(() => generateSvgPath(tempHistory.value, 100))
const fanChart = computed(() => generateSvgPath(fanSpeedHistory.value, 40)) // Corresponds to 4000 RPM

// --- Settings and Presets Logic ---
const GPUData = computed(() => configStore.config?.Gpu)
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
      const min = core.Data.Min ?? 0
      const max = core.Data.Max ?? 500
      coreClockRange.value = { Min: min, Max: max }
      if (GPUData.value && (GPUData.value.GpuClock < min || GPUData.value.GpuClock > max)) {
        GPUData.value.GpuClock = max
      }
    }

    if (mem.Success && mem.Data) {
      const min = mem.Data.Min ?? 0
      const max = mem.Data.Max ?? 1500
      memClockRange.value = { Min: min, Max: max }
      if (GPUData.value && (GPUData.value.MemoryClock < min || GPUData.value.MemoryClock > max)) {
        GPUData.value.MemoryClock = max
      }
    }

    if (power.Success && power.Data) {
      const min = power.Data.Min ?? 50
      const max = power.Data.Max ?? 140
      powerLimitRange.value = { Min: min, Max: max }
      if (GPUData.value && (GPUData.value.PowerLimit < min || GPUData.value.PowerLimit > max)) {
        GPUData.value.PowerLimit = max
      }
    }
  } catch (err) {
    console.error('Failed to fetch GPU ranges', err)
  }
}

// --- 核心/显存频率偏移 (Afterburner 主滑条模型) ---
const curveCaps = ref<GpuCurveCapabilitiesInfo | null>(null)
const curveStatus = ref<GpuCurveStatusInfo | null>(null)
const coreOffset = ref(0)
const memOffset = ref(0)
const curveApplying = ref(false)
const memOffsetMax = computed(() => Math.max(500, curveStatus.value?.MemoryOffsetMaxMhz ?? 1500))
const coreOffsetMin = computed(
  () => curveStatus.value?.CoreOffsetMinMhz ?? curveCaps.value?.CoreOffsetMinMhz ?? -250,
)
const coreOffsetMax = computed(
  () => curveStatus.value?.CoreOffsetMaxMhz ?? curveCaps.value?.CoreOffsetMaxMhz ?? 250,
)

/// 把已保存配置映射到界面控件 (页面初始化与取消修改共用)
function initCurveUiFromConfig() {
  if (!GPUData.value) return
  coreOffset.value = Math.min(
    Math.max(GPUData.value.CoreClockOffsetMhz ?? 0, coreOffsetMin.value),
    coreOffsetMax.value,
  )
  if (curveStatus.value) {
    memOffset.value = Math.min(
      Math.max(GPUData.value.MemoryClockOffsetMhz ?? 0, 0),
      curveStatus.value.MemoryOffsetMaxMhz,
    )
  }
}

async function fetchGpuCurve() {
  try {
    const caps = await NvidiaGpu.GetGpuCurveCapabilities()
    if (caps.Success && caps.Data) curveCaps.value = caps.Data

    const status = await NvidiaGpu.GetGpuCurveStatus().catch(() => null)
    if (status?.Success && status.Data) curveStatus.value = status.Data

    initCurveUiFromConfig()
  } catch (err) {
    console.error('Failed to fetch GPU curve state', err)
  }
}

/// 常规设置: 只应用 GPU/显存锁频, 不动高级超频
async function handleApplyNormal() {
  if (!GPUData.value) return
  loading.value = true
  try {
    const clockRes = await NvidiaGpu.LockGpuClock(GPUData.value.GpuClock)
    if (!clockRes.Success) {
      Message.error(clockRes.Message || 'GPU 频率锁定失败')
      return
    }
    const memClockRes = await NvidiaGpu.LockMemoryClock(GPUData.value.MemoryClock)
    if (!memClockRes.Success) {
      Message.error(memClockRes.Message || '显存频率锁定失败')
      return
    }
    const saveRes = await configStore.saveConfig()
    if (saveRes?.Success) {
      Message.success('常规设置已应用并保存')
    } else {
      Message.error(saveRes?.Message || '设置保存失败')
    }
  } catch {
    Message.error('应用失败，请检查显卡驱动及桥接服务')
  } finally {
    loading.value = false
  }
}

/// 常规设置: 只重置锁频到睿频上限, 不动高级超频
async function handleResetNormal() {
  loading.value = true
  try {
    const clockRes = await NvidiaGpu.ResetGpuClock()
    if (!clockRes.Success) {
      Message.error(clockRes.Message || 'GPU 频率重置失败')
      return
    }
    const memClockRes = await NvidiaGpu.ResetMemoryClock()
    if (!memClockRes.Success) {
      Message.error(memClockRes.Message || '显存频率重置失败')
      return
    }
    if (GPUData.value) {
      GPUData.value.GpuClock = coreClockRange.value.Max
      GPUData.value.MemoryClock = memClockRange.value.Max
      GPUData.value.PowerLimit = powerLimitRange.value.Max
    }
    const saveRes = await configStore.saveConfig()
    if (saveRes?.Success) {
      Message.info('常规设置已恢复默认')
    } else {
      Message.error(saveRes?.Message || '重置值保存失败')
    }
  } catch {
    Message.error('重置失败，请检查显卡驱动及桥接服务')
  } finally {
    loading.value = false
  }
}

/// 高级超频: 只应用核心/显存频率偏移, 不动常规锁频
async function handleApplyCurve() {
  if (!GPUData.value) return
  curveApplying.value = true
  try {
    const curveRes = await NvidiaGpu.SetGpuOffsets(coreOffset.value, memOffset.value)
    if (!curveRes.Success) {
      Message.error(curveRes.Message || '频率偏移设置失败')
      return
    }
    GPUData.value.CoreClockOffsetMhz = coreOffset.value
    GPUData.value.MemoryClockOffsetMhz = memOffset.value
    const saveRes = await configStore.saveConfig()
    if (!saveRes?.Success) {
      Message.error(saveRes?.Message || '设置已应用但保存失败')
      return
    }
    Message.success(curveRes.Message || '高级超频已应用')
  } catch {
    Message.error('应用失败，请检查显卡驱动及桥接服务')
  } finally {
    curveApplying.value = false
  }
}

/// 高级超频: 只清零核心/显存偏移, 不动常规锁频
async function handleResetCurve() {
  curveApplying.value = true
  try {
    const curveRes = await NvidiaGpu.ResetGpuCurve()
    if (!curveRes.Success) {
      Message.error(curveRes.Message || '偏移重置失败')
      return
    }
    coreOffset.value = 0
    memOffset.value = 0
    if (GPUData.value) {
      GPUData.value.CoreClockOffsetMhz = 0
      GPUData.value.MemoryClockOffsetMhz = 0
    }
    const saveRes = await configStore.saveConfig()
    if (saveRes?.Success) {
      Message.info('高级超频已重置为默认')
    } else {
      Message.error(saveRes?.Message || '重置值保存失败')
    }
  } catch {
    Message.error('重置失败，请检查显卡驱动及桥接服务')
  } finally {
    curveApplying.value = false
  }
}

// --- 名词解释 ---
const showGlossary = ref(false)
const glossary = [
  {
    term: 'V/F 曲线',
    desc: '电压与频率的对应关系：电压越高，GPU 能稳定运行的频率越高。降压的本质是让曲线在更低电压处达到目标频率。',
  },
  {
    term: '睿频 (Boost)',
    desc: '负载下 GPU 自动提升到的高频状态，实际频率受功耗、温度和电压共同约束。',
  },
  {
    term: '核心频率偏移',
    desc: '把整条 V/F 曲线统一平移：+100 MHz 即全频段超频 100 MHz，-50 MHz 即全频段降压 50 MHz。与 Afterburner 的核心时钟偏移 (Core Clock Offset) 相同。',
  },
  {
    term: '显存频率偏移',
    desc: '在出厂显存频率基础上的增量 (MHz)，提升显存带宽，高分辨率游戏受益明显。设置后重启会自动恢复。',
  },
  {
    term: '锁频 (常规设置)',
    desc: '把核心/显存频率固定在指定值，不随负载浮动。把频率拉到睿频上限可以作为超频被锁时的替代方案。',
  },
  {
    term: '工作点',
    desc: '负载下睿频实际停留的电压/频率位置，可在高级超频面板的提示中查看。',
  },
]

await fetchGpuRanges()
await fetchGpuCurve()
</script>

<template>
  <div v-if="GPUData && gpuName" class="h-full overflow-y-auto text-ink p-6 no-scrollbar">
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
          class="bg-panel/60 backdrop-blur-md border border-ink/[0.05] rounded-xl p-5 shadow-lg flex flex-col md:flex-row justify-between gap-6"
        >
          <div class="space-y-3 md:w-1/2">
            <span class="text-[11px] text-gray-500 font-semibold block uppercase">当前 GPU</span>
            <span class="flex items-center gap-2">
              <span class="w-1.5 h-1.5 rounded-full bg-[#76B900] font-bold"></span>
              {{ gpuName }}
            </span>
          </div>

          <div
            class="grid grid-cols-2 gap-x-6 gap-y-3 text-[11px] text-gray-400 md:w-1/2 md:border-l md:border-ink/[0.05] md:pl-6 pt-1"
          >
            <div>
              <span class="text-gray-600 block mb-0.5">驱动版本</span>
              <span class="text-ink font-medium font-mono">{{ gpuDriverVersion }}</span>
            </div>
            <div>
              <span class="text-gray-600 block mb-0.5">显存容量</span>
              <span class="text-ink font-medium font-mono">{{ gpuMemoryTotal }}</span>
            </div>
            <div>
              <span class="text-gray-600 block mb-0.5">驱动日期</span>
              <span class="text-ink font-medium font-mono">{{ gpuDriverDate }}</span>
            </div>
            <div>
              <span class="text-gray-600 block mb-0.5">总线宽度</span>
              <span class="text-ink font-medium font-mono">{{ gpuBusWidth }}</span>
            </div>
          </div>
        </div>
        <!-- 3. 模式切换按钮 -->
        <div class="flex gap-2">
          <button
            :class="[
              'flex-1 text-xs font-medium px-4 py-2.5 rounded-lg transition-all border',
              !showAdvanced
                ? 'bg-gradient-to-r from-purple-700 to-indigo-600 text-white border-transparent shadow-[0_0_12px_rgba(138,43,226,0.25)]'
                : 'bg-ink/[0.02] text-gray-400 border-ink/10 hover:text-ink hover:border-ink/20',
            ]"
            @click="showAdvanced = false"
          >
            常规设置
          </button>
          <button
            :class="[
              'flex-1 text-xs font-medium px-4 py-2.5 rounded-lg transition-all border',
              showAdvanced
                ? 'bg-gradient-to-r from-purple-700 to-indigo-600 text-white border-transparent shadow-[0_0_12px_rgba(138,43,226,0.25)]'
                : 'bg-ink/[0.02] text-gray-400 border-ink/10 hover:text-ink hover:border-ink/20',
            ]"
            @click="showAdvanced = true"
          >
            高级超频
          </button>
        </div>

        <!-- 常规设置面板 -->
        <div
          v-if="!showAdvanced"
          class="bg-panel/60 backdrop-blur-md border border-ink/[0.05] rounded-xl p-5 shadow-lg space-y-5"
        >
          <div class="space-y-5">
            <div class="space-y-2">
              <div class="flex justify-between items-center text-xs">
                <span class="text-gray-300 flex items-center gap-1"
                  >GPU 频率
                  <span class="text-gray-500 cursor-pointer text-[10px]">ⓘ</span>
                </span>
                <span class="text-purple-400 font-medium font-mono"
                  >{{ GPUData.GpuClock }} MHz</span
                >
              </div>
              <a-slider
                v-model="GPUData.GpuClock"
                :min="coreClockRange.Min"
                :max="coreClockRange.Max"
                class="w-full"
              />
            </div>

            <div class="space-y-2">
              <div class="flex justify-between items-center text-xs">
                <span class="text-gray-300 flex items-center gap-1"
                  >显存频率 <span class="text-gray-500 cursor-pointer text-[10px]">ⓘ</span></span
                >
                <span class="text-purple-400 font-medium font-mono"
                  >{{ GPUData.MemoryClock }} MHz</span
                >
              </div>
              <a-slider
                v-model="GPUData.MemoryClock"
                :min="memClockRange.Min"
                :max="memClockRange.Max"
                class="w-full"
              />
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

          <div class="flex justify-between items-center pt-2 border-t border-ink/[0.04]">
            <button
              class="flex items-center gap-2 text-xs text-gray-400 hover:text-ink border border-ink/10 hover:border-ink/20 bg-ink/[0.02] hover:bg-ink/[0.05] px-4 py-2 rounded-lg transition-colors"
              @click="handleResetNormal"
            >
              重置
            </button>
            <button
              :disabled="loading"
              class="text-xs font-medium text-white bg-gradient-to-r from-purple-700 to-indigo-600 hover:from-purple-600 hover:to-indigo-500 disabled:opacity-50 px-6 py-2 rounded-lg transition-all shadow-[0_0_15px_rgba(138,43,226,0.3)]"
              @click="handleApplyNormal"
            >
              {{ loading ? '应用中...' : '应用' }}
            </button>
          </div>
        </div>

        <!-- 高级超频面板 -->
        <div
          v-if="showAdvanced"
          class="bg-panel/60 backdrop-blur-md border border-ink/[0.05] rounded-xl p-5 shadow-lg space-y-6"
        >
          <div
            v-if="curveCaps && !curveCaps.Supported"
            class="text-[11px] text-amber-400/90 bg-amber-500/10 border border-amber-500/20 rounded-lg px-3 py-2"
          >
            本机不支持核心频率偏移：{{ curveCaps.Reason }}。显存偏移仍可设置，超频可用「常规设置」的锁频代替。
          </div>

          <!-- 核心频率偏移 -->
          <div v-if="curveCaps?.Supported" class="space-y-2">
            <div class="flex justify-between items-center text-xs">
              <span class="text-gray-300 flex items-center gap-1"
                >核心频率偏移
                <span
                  class="text-gray-500 cursor-pointer text-[10px] hover:text-gray-300"
                  title="把整条 V/F 曲线统一平移：+100 MHz 即全频段超频 100 MHz，-50 MHz 即全频段降压（与 Afterburner 的核心时钟偏移相同）"
                  >ⓘ</span
                ></span
              >
              <span class="text-purple-400 font-medium font-mono">
                {{ coreOffset >= 0 ? '+' : '' }}{{ coreOffset }} MHz
                <span
                  v-if="coreOffset !== 0"
                  :class="coreOffset > 0 ? 'text-amber-400' : 'text-emerald-400'"
                  class="text-[10px] font-sans"
                  >{{ coreOffset > 0 ? '超频' : '降压' }}</span
                >
              </span>
            </div>
            <a-slider
              v-model="coreOffset"
              :max="coreOffsetMax"
              :min="coreOffsetMin"
              :step="5"
              class="w-full"
            />
          </div>

          <!-- 显存频率偏移 -->
          <div class="space-y-2">
            <div class="flex justify-between items-center text-xs">
              <span class="text-gray-300 flex items-center gap-1">显存频率偏移</span>
              <span class="text-purple-400 font-medium font-mono">+{{ memOffset }} MHz</span>
            </div>
            <a-slider
              v-model="memOffset"
              :max="memOffsetMax"
              :min="0"
              :step="50"
              class="w-full"
            />
          </div>

          <div class="flex justify-between items-center pt-2 border-t border-ink/[0.04]">
            <button
              class="flex items-center gap-2 text-xs text-gray-400 hover:text-ink border border-ink/10 hover:border-ink/20 bg-ink/[0.02] hover:bg-ink/[0.05] px-4 py-2 rounded-lg transition-colors"
              @click="handleResetCurve"
            >
              重置
            </button>
            <button
              :disabled="curveApplying"
              class="text-xs font-medium text-white bg-gradient-to-r from-purple-700 to-indigo-600 hover:from-purple-600 hover:to-indigo-500 disabled:opacity-50 px-6 py-2 rounded-lg transition-all shadow-[0_0_15px_rgba(138,43,226,0.3)]"
              @click="handleApplyCurve"
            >
              {{ curveApplying ? '应用中...' : '应用' }}
            </button>
          </div>
        </div>
      </div>

      <!-- ==================== 右侧：显卡信息与实时监控栏 ==================== -->
      <div class="w-full lg:w-[360px] shrink-0 space-y-6 lg:pt-[115px]">
        <!-- 2. 实时监控面板 -->
        <div
          class="bg-panel/60 backdrop-blur-md border border-ink/[0.05] rounded-xl p-5 shadow-lg space-y-4"
        >
          <div class="flex justify-between items-center">
            <h2 class="text-[13px] font-semibold text-gray-300">实时监控</h2>
          </div>
          <div class="grid grid-cols-2 gap-3">
            <!-- GPU 使用率 -->
            <div
              class="bg-ink/[0.02] border border-ink/[0.04] p-3 rounded-lg flex flex-col justify-between"
            >
              <div>
                <span class="text-[10px] text-gray-500 block">GPU 使用率</span>
                <span class="text-base font-bold text-ink font-mono"
                  >{{ gpuUtilization }}
                  <span class="text-[10px] text-gray-500 font-bold">%</span></span
                >
              </div>
              <svg
                class="w-full h-8 opacity-70 mt-1"
                viewBox="0 0 160 40"
                preserveAspectRatio="none"
              >
                <defs>
                  <linearGradient id="g-purple" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="0%" stop-color="#8A2BE2" stop-opacity="0.3" />
                    <stop offset="100%" stop-color="#8A2BE2" stop-opacity="0" />
                  </linearGradient>
                </defs>
                <path :d="utilChart.line" fill="none" stroke="#8A2BE2" stroke-width="1.2" />
                <path :d="utilChart.area" fill="url(#g-purple)" />
              </svg>
            </div>

            <!-- 显存使用率 -->
            <div
              class="bg-ink/[0.02] border border-ink/[0.04] p-3 rounded-lg flex flex-col justify-between"
            >
              <div>
                <span class="text-[10px] text-gray-500 block">显存使用率</span>
                <span class="text-base font-bold text-ink font-mono"
                  >{{ gpuMemoryUtilization }}
                  <span class="text-[10px] text-gray-500 font-bold">%</span></span
                >
              </div>
              <svg
                class="w-full h-8 opacity-70 mt-1"
                viewBox="0 0 160 40"
                preserveAspectRatio="none"
              >
                <defs>
                  <linearGradient id="g-blue" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="0%" stop-color="#3B82F6" stop-opacity="0.3" />
                    <stop offset="100%" stop-color="#3B82F6" stop-opacity="0" />
                  </linearGradient>
                </defs>
                <path :d="memUtilChart.line" fill="none" stroke="#3B82F6" stroke-width="1.2" />
                <path :d="memUtilChart.area" fill="url(#g-blue)" />
              </svg>
            </div>

            <!-- 核心频率 -->
            <div
              class="bg-ink/[0.02] border border-ink/[0.04] p-3 rounded-lg flex flex-col justify-between"
            >
              <div>
                <span class="text-[10px] text-gray-500 block">核心频率</span>
                <span class="text-base font-bold text-ink font-mono"
                  >{{ gpuCoreClock }}
                  <span class="text-[9px] text-gray-500 font-bold">MHz</span></span
                >
              </div>
              <svg
                class="w-full h-8 opacity-70 mt-1"
                viewBox="0 0 160 40"
                preserveAspectRatio="none"
              >
                <path :d="coreClockChart.line" fill="none" stroke="#8A2BE2" stroke-width="1.2" />
                <path :d="coreClockChart.area" fill="url(#g-purple)" />
              </svg>
            </div>

            <!-- 显存频率 -->
            <div
              class="bg-ink/[0.02] border border-ink/[0.04] p-3 rounded-lg flex flex-col justify-between"
            >
              <div>
                <span class="text-[10px] text-gray-500 block">显存频率</span>
                <span class="text-base font-bold text-ink font-mono"
                  >{{ gpuMemoryClock }}
                  <span class="text-[9px] text-gray-500 font-bold">MHz</span></span
                >
              </div>
              <svg
                class="w-full h-8 opacity-70 mt-1"
                viewBox="0 0 160 40"
                preserveAspectRatio="none"
              >
                <path :d="memClockChart.line" fill="none" stroke="#3B82F6" stroke-width="1.2" />
                <path :d="memClockChart.area" fill="url(#g-blue)" />
              </svg>
            </div>

            <!-- GPU 温度 -->
            <div
              class="bg-ink/[0.02] border border-ink/[0.04] p-3 rounded-lg flex flex-col justify-between"
            >
              <div>
                <span class="text-[10px] text-gray-500 block">GPU 温度</span>
                <span class="text-base font-bold text-ink font-mono"
                  >{{ gpuTemp }} <span class="text-[10px] text-gray-500 font-bold">°C</span></span
                >
              </div>
              <svg
                class="w-full h-8 opacity-70 mt-1"
                viewBox="0 0 160 40"
                preserveAspectRatio="none"
              >
                <defs>
                  <linearGradient id="g-green" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="0%" stop-color="#10B981" stop-opacity="0.3" />
                    <stop offset="100%" stop-color="#10B981" stop-opacity="0" />
                  </linearGradient>
                </defs>
                <path :d="tempChart.line" fill="none" stroke="#10B981" stroke-width="1.2" />
                <path :d="tempChart.area" fill="url(#g-green)" />
              </svg>
            </div>

            <!-- 风扇转速 -->
            <div
              class="bg-ink/[0.02] border border-ink/[0.04] p-3 rounded-lg flex flex-col justify-between"
            >
              <div>
                <span class="text-[10px] text-gray-500 block">风扇转速</span>
                <span class="text-base font-bold text-ink font-mono"
                  >{{ gpuFanSpeed }}
                  <span class="text-[9px] text-gray-500 font-bold">RPM</span></span
                >
              </div>
              <svg
                class="w-full h-8 opacity-70 mt-1"
                viewBox="0 0 160 40"
                preserveAspectRatio="none"
              >
                <path :d="fanChart.line" fill="none" stroke="#3B82F6" stroke-width="1.2" />
                <path :d="fanChart.area" fill="url(#g-blue)" />
              </svg>
            </div>
          </div>
        </div>

        <!-- 说明卡片 -->
        <div
          class="bg-panel/60 backdrop-blur-md border border-ink/[0.05] rounded-xl p-5 shadow-lg space-y-2.5"
        >
          <h2 class="text-[13px] font-semibold text-gray-300">说明</h2>
          <div class="text-[11px] text-gray-500 leading-relaxed space-y-2">
            <p>核心频率偏移把整条电压-频率曲线统一平移：正值超频，负值降压。</p>
            <p>显存频率偏移在出厂显存频率基础上提升带宽。</p>
            <p>修改设置后请点击"应用"以生效。</p>
          </div>
          <div
            class="text-[11px] text-blue-400 hover:text-blue-300 cursor-pointer pt-1 flex items-center gap-0.5 font-medium transition-colors"
            @click="showGlossary = !showGlossary"
          >
            {{ showGlossary ? '收起术语说明' : '了解更多' }} <span>&gt;</span>
          </div>
          <div v-if="showGlossary" class="space-y-2.5 pt-1">
            <div v-for="item in glossary" :key="item.term" class="text-[11px] leading-relaxed">
              <span class="text-gray-300 font-medium">{{ item.term }}</span>
              <span class="text-gray-500 ml-2">{{ item.desc }}</span>
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
:deep(.arco-select-view-value) {
  color: var(--color-text-main);
}

.no-scrollbar::-webkit-scrollbar {
  display: none;
}

.no-scrollbar {
  -ms-overflow-style: none;
  scrollbar-width: none;
}

:deep(.select-dark .arco-select-view-single) {
  background-color: var(--color-panel-elevated) !important;
  border: 1px solid var(--color-line-soft) !important;
  color: var(--color-text-main) !important;
  border-radius: 8px !important;
  height: 32px !important;
}

:deep(.arco-switch-checked) {
  background-color: var(--color-accent-purple) !important;
}
</style>
