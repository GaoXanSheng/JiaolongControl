<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, type Ref, watch } from 'vue'
import { Message } from '@arco-design/web-vue'
import {
  NvidiaGpu,
  type CommandResult,
  type OverclockCapabilities,
} from '@/utils/bridge'
import { buildSparkline } from '@/utils/chart'
import { useConfigStore } from '@/stores/config'
import { useSystemInfoStore } from '@/stores/systemInfo'
import { storeToRefs } from 'pinia'

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
const gpuClockOffset = ref(0)
const memClockOffset = ref(0)
const voltageBoostPercent = ref(0)
const tempWall = ref(87)
const coreClockRange = ref({ Min: 0, Max: 500 })
const memClockRange = ref({ Min: 0, Max: 1500 })
const powerLimitRange = ref({ Min: 50, Max: 140 })
const offsetRange = ref({ Core: { Min: -1000, Max: 1000 }, Memory: { Min: -1000, Max: 3000 } })
const thermalPolicy = ref({ CurrentTemp: 87, MinTemp: 65, DefaultTemp: 83, MaxTemp: 90 })
const ocCaps = ref<OverclockCapabilities>({
  CoreOffset: true,
  MemoryOffset: true,
  VoltageBoost: true,
  ThermalPolicy: true,
  PowerPolicy: true,
})

async function fetchGpuRanges() {
  try {
    const [core, mem, power, ocRange, ocOffsets, thermal, caps] = await Promise.all([
      NvidiaGpu.GetGpuCoreClockRange(),
      NvidiaGpu.GetGpuMemoryClockRange(),
      NvidiaGpu.GetGpuPowerLimitRange(),
      NvidiaGpu.GetClockOffsetRange(),
      NvidiaGpu.GetClockOffsets().catch(() => null),
      NvidiaGpu.GetGpuThermalPolicy().catch(() => null),
      NvidiaGpu.GetOverclockCapabilities().catch(() => null),
    ])

    if (caps && caps.Success && caps.Data) {
      ocCaps.value = caps.Data
    }

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

    if (ocRange.Success && ocRange.Data) {
      offsetRange.value = {
        Core: { Min: ocRange.Data.Core?.Min ?? -1000, Max: ocRange.Data.Core?.Max ?? 1000 },
        Memory: { Min: ocRange.Data.Memory?.Min ?? -1000, Max: ocRange.Data.Memory?.Max ?? 3000 },
      }
    }

    // 偏移量以驱动当前实际值为准, 读取失败时回落到配置持久化值
    if (ocOffsets && ocOffsets.Success && ocOffsets.Data) {
      gpuClockOffset.value = ocOffsets.Data.CoreMhz
      memClockOffset.value = ocOffsets.Data.MemoryMhz
    } else if (GPUData.value) {
      gpuClockOffset.value = GPUData.value.CoreClockOffset ?? 0
      memClockOffset.value = GPUData.value.MemoryClockOffset ?? 0
    }

    if (thermal && thermal.Success && thermal.Data) {
      thermalPolicy.value = thermal.Data
      tempWall.value = thermal.Data.CurrentTemp
    }
  } catch (err) {
    console.error('Failed to fetch GPU ranges', err)
  }
}

await fetchGpuRanges()

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

async function handleApplyAdvanced() {
  if (!GPUData.value) return
  loading.value = true
  try {
    if (ocCaps.value.CoreOffset) {
      const coreRes = await NvidiaGpu.SetCoreClockOffset(gpuClockOffset.value)
      if (!coreRes.Success) {
        Message.error(coreRes.Message || '核心频率偏移失败')
        return
      }
    }
    if (ocCaps.value.MemoryOffset) {
      const memRes = await NvidiaGpu.SetMemoryClockOffset(memClockOffset.value)
      if (!memRes.Success) {
        Message.error(memRes.Message || '显存频率偏移失败')
        return
      }
    }
    if (ocCaps.value.VoltageBoost) {
      const voltRes = await NvidiaGpu.SetVoltageBoostPercent(voltageBoostPercent.value)
      if (!voltRes.Success) {
        Message.error(voltRes.Message || '核心电压提升设置失败')
        return
      }
    }
    if (ocCaps.value.ThermalPolicy && tempWall.value !== thermalPolicy.value.CurrentTemp) {
      const tempRes = await NvidiaGpu.SetGpuThermalPolicy(tempWall.value)
      if (!tempRes.Success) {
        Message.error(tempRes.Message || '温度墙设置失败')
        return
      }
      thermalPolicy.value.CurrentTemp = tempWall.value
    }
    GPUData.value.CoreClockOffset = gpuClockOffset.value
    GPUData.value.MemoryClockOffset = memClockOffset.value
    GPUData.value.VoltageBoostPercent = voltageBoostPercent.value
    const saveRes = await configStore.saveConfig()
    if (saveRes?.Success) {
      Message.success('高级超频已应用并保存')
    } else {
      Message.error(saveRes?.Message || '设置保存失败')
    }
  } catch {
    Message.error('应用失败，请检查显卡驱动及桥接服务')
  } finally {
    loading.value = false
  }
}

async function handleResetAdvanced() {
  loading.value = true
  try {
    if (ocCaps.value.CoreOffset || ocCaps.value.MemoryOffset) {
      const res = await NvidiaGpu.ResetClockOffsets()
      if (!res.Success) {
        Message.error(res.Message || '超频重置失败')
        return
      }
    }
    if (ocCaps.value.VoltageBoost) {
      const voltRes = await NvidiaGpu.SetVoltageBoostPercent(0)
      if (!voltRes.Success) {
        Message.error(voltRes.Message || '电压提升重置失败')
        return
      }
    }
    if (
      ocCaps.value.ThermalPolicy &&
      tempWall.value !== thermalPolicy.value.DefaultTemp
    ) {
      // 温度墙恢复默认失败不阻塞整体重置
      const tempRes = await NvidiaGpu.SetGpuThermalPolicy(thermalPolicy.value.DefaultTemp)
      if (tempRes.Success) thermalPolicy.value.CurrentTemp = thermalPolicy.value.DefaultTemp
    }
    gpuClockOffset.value = 0
    memClockOffset.value = 0
    voltageBoostPercent.value = 0
    tempWall.value = thermalPolicy.value.DefaultTemp
    if (GPUData.value) {
      GPUData.value.CoreClockOffset = 0
      GPUData.value.MemoryClockOffset = 0
      GPUData.value.VoltageBoostPercent = 0
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
    loading.value = false
  }
}
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
<!--          <button-->
<!--            @click="showAdvanced = false"-->
<!--            :class="[-->
<!--              'flex-1 text-xs font-medium px-4 py-2.5 rounded-lg transition-all border',-->
<!--              !showAdvanced-->
<!--                ? 'bg-gradient-to-r from-purple-700 to-indigo-600 text-white border-transparent shadow-[0_0_12px_rgba(138,43,226,0.25)]'-->
<!--                : 'bg-ink/[0.02] text-gray-400 border-ink/10 hover:text-ink hover:border-ink/20',-->
<!--            ]"-->
<!--          >-->
<!--            常规设置-->
<!--          </button>-->
<!--          <button-->
<!--            @click="showAdvanced = true"-->
<!--            :class="[-->
<!--              'flex-1 text-xs font-medium px-4 py-2.5 rounded-lg transition-all border',-->
<!--              showAdvanced-->
<!--                ? 'bg-gradient-to-r from-purple-700 to-indigo-600 text-white border-transparent shadow-[0_0_12px_rgba(138,43,226,0.25)]'-->
<!--                : 'bg-ink/[0.02] text-gray-400 border-ink/10 hover:text-ink hover:border-ink/20',-->
<!--            ]"-->
<!--          >-->
<!--            高级超频-->
<!--          </button>-->
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
              class="text-xs font-medium text-ink bg-gradient-to-r from-purple-700 to-indigo-600 hover:from-purple-600 hover:to-indigo-500 disabled:opacity-50 px-6 py-2 rounded-lg transition-all shadow-[0_0_15px_rgba(138,43,226,0.3)]"
              @click="handleApplyNormal"
            >
              {{ loading ? '应用中...' : '应用' }}
            </button>
          </div>
        </div>

        <!-- 高级超频面板 -->
<!--        <div-->
<!--          v-if="showAdvanced"-->
<!--          class="bg-panel/60 backdrop-blur-md border border-ink/[0.05] rounded-xl p-5 shadow-lg space-y-5"-->
<!--        >-->
<!--          <div class="space-y-5">-->
<!--            <div-->
<!--              v-if="!ocCaps.CoreOffset || !ocCaps.MemoryOffset || !ocCaps.VoltageBoost"-->
<!--              class="text-[11px] text-amber-400/90 bg-amber-500/10 border border-amber-500/20 rounded-lg px-3 py-2"-->
<!--            >-->
<!--              本机驱动已锁定部分超频能力 (OEM 限制)，对应滑条已置灰。可用「常规设置」的锁频拉满睿频代替。-->
<!--            </div>-->

<!--            <div class="space-y-2">-->
<!--              <div class="flex justify-between items-center text-xs">-->
<!--                <span class="text-gray-300 flex items-center gap-1"-->
<!--                  >核心频率偏移-->
<!--                  <span-->
<!--                    v-if="!ocCaps.CoreOffset"-->
<!--                    class="text-[9px] text-rose-400/90 border border-rose-500/30 rounded px-1"-->
<!--                    >驱动已锁定</span-->
<!--                  >-->
<!--                  <span class="text-gray-500 cursor-pointer text-[10px]">ⓘ</span></span-->
<!--                >-->
<!--                <span class="text-purple-400 font-medium font-mono"-->
<!--                  >{{ gpuClockOffset > 0 ? '+' : '' }}{{ gpuClockOffset }} MHz</span-->
<!--                >-->
<!--              </div>-->
<!--              <a-slider-->
<!--                v-model="gpuClockOffset"-->
<!--                :min="offsetRange.Core.Min"-->
<!--                :max="offsetRange.Core.Max"-->
<!--                :disabled="!ocCaps.CoreOffset"-->
<!--                class="w-full"-->
<!--              />-->
<!--            </div>-->

<!--            <div class="space-y-2">-->
<!--              <div class="flex justify-between items-center text-xs">-->
<!--                <span class="text-gray-300 flex items-center gap-1"-->
<!--                  >显存频率偏移-->
<!--                  <span-->
<!--                    v-if="!ocCaps.MemoryOffset"-->
<!--                    class="text-[9px] text-rose-400/90 border border-rose-500/30 rounded px-1"-->
<!--                    >不支持</span-->
<!--                  >-->
<!--                  <span class="text-gray-500 cursor-pointer text-[10px]">ⓘ</span></span-->
<!--                >-->
<!--                <span class="text-purple-400 font-medium font-mono"-->
<!--                  >{{ memClockOffset > 0 ? '+' : '' }}{{ memClockOffset }} MHz</span-->
<!--                >-->
<!--              </div>-->
<!--              <a-slider-->
<!--                v-model="memClockOffset"-->
<!--                :min="offsetRange.Memory.Min"-->
<!--                :max="offsetRange.Memory.Max"-->
<!--                :disabled="!ocCaps.MemoryOffset"-->
<!--                class="w-full"-->
<!--              />-->
<!--            </div>-->

<!--            <div class="space-y-2">-->
<!--              <div class="flex justify-between items-center text-xs">-->
<!--                <span class="text-gray-300 flex items-center gap-1"-->
<!--                  >核心电压提升-->
<!--                  <span-->
<!--                    v-if="!ocCaps.VoltageBoost"-->
<!--                    class="text-[9px] text-rose-400/90 border border-rose-500/30 rounded px-1"-->
<!--                    >驱动已锁定</span-->
<!--                  >-->
<!--                  <span class="text-gray-500 cursor-pointer text-[10px]">ⓘ</span></span-->
<!--                >-->
<!--                <span class="text-purple-400 font-medium font-mono"-->
<!--                  >+{{ voltageBoostPercent }} %</span-->
<!--                >-->
<!--              </div>-->
<!--              <a-slider-->
<!--                v-model="voltageBoostPercent"-->
<!--                :min="0"-->
<!--                :max="100"-->
<!--                :disabled="!ocCaps.VoltageBoost"-->
<!--                class="w-full"-->
<!--              />-->
<!--            </div>-->

<!--            <div class="space-y-2">-->
<!--              <div class="flex justify-between items-center text-xs">-->
<!--                <span class="text-gray-300 flex items-center gap-1"-->
<!--                  >温度墙上限-->
<!--                  <span-->
<!--                    v-if="!ocCaps.ThermalPolicy"-->
<!--                    class="text-[9px] text-rose-400/90 border border-rose-500/30 rounded px-1"-->
<!--                    >不支持</span-->
<!--                  >-->
<!--                  <span class="text-gray-500 cursor-pointer text-[10px]">ⓘ</span></span-->
<!--                >-->
<!--                <span class="text-purple-400 font-medium font-mono">{{ tempWall }} ℃</span>-->
<!--              </div>-->
<!--              <a-slider-->
<!--                v-model="tempWall"-->
<!--                :min="thermalPolicy.MinTemp"-->
<!--                :max="thermalPolicy.MaxTemp"-->
<!--                :disabled="!ocCaps.ThermalPolicy"-->
<!--                class="w-full"-->
<!--              />-->
<!--            </div>-->
<!--          </div>-->

<!--          <div class="flex justify-between items-center pt-2 border-t border-ink/[0.04]">-->
<!--            <button-->
<!--              class="flex items-center gap-2 text-xs text-gray-400 hover:text-ink border border-ink/10 hover:border-ink/20 bg-ink/[0.02] hover:bg-ink/[0.05] px-4 py-2 rounded-lg transition-colors"-->
<!--              @click="handleResetAdvanced"-->
<!--            >-->
<!--              重置-->
<!--            </button>-->
<!--            <button-->
<!--              :disabled="loading"-->
<!--              class="text-xs font-medium text-ink bg-gradient-to-r from-purple-700 to-indigo-600 hover:from-purple-600 hover:to-indigo-500 disabled:opacity-50 px-6 py-2 rounded-lg transition-all shadow-[0_0_15px_rgba(138,43,226,0.3)]"-->
<!--              @click="handleApplyAdvanced"-->
<!--            >-->
<!--              {{ loading ? '应用中...' : '应用' }}-->
<!--            </button>-->
<!--          </div>-->
<!--        </div>-->
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
