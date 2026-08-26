<script setup lang="ts">
import CpuDie from '@/components/common/CpuDie.vue'

import { reactive, ref, watch, computed, onMounted, onUnmounted } from 'vue'
import { Message } from '@arco-design/web-vue'
import { CPU, RyzenSmu, type CommandResult, type SmuTelemetry } from '@/utils/bridge'
import { useConfigStore } from '@/stores/config'
import type { SmuSectionType } from '@/types/config'
import { POLL_INTERVAL_SMU } from '@/constants'

interface ConfigGroupItem {
  label: string
  key: keyof SmuSectionType
  min: number
  max: number
  step?: number
  unit: string
  sliderClass: string
}
interface ConfigGroup {
  title: string
  items: ConfigGroupItem[]
}

const CONFIG_GROUPS: ConfigGroup[] = [
  {
    title: '功耗限制 (Power Limits)',
    items: [
      {
        label: 'STAPM 长期功耗上限',
        key: 'StapmLimit',
        min: 0,
        max: 200,
        unit: 'W',
        sliderClass: 'slider-purple',
      },
      {
        label: 'STAPM 时间窗口',
        key: 'StapmTime',
        min: 0,
        max: 3600,
        unit: 's',
        sliderClass: 'slider-purple',
      },
      {
        label: 'Fast 瞬时功耗上限',
        key: 'FastLimit',
        min: 0,
        max: 200,
        unit: 'W',
        sliderClass: 'slider-purple',
      },
      {
        label: 'Slow 持续功耗上限',
        key: 'SlowLimit',
        min: 0,
        max: 200,
        unit: 'W',
        sliderClass: 'slider-purple',
      },
      {
        label: 'Slow 功耗时间窗口',
        key: 'SlowTime',
        min: 0,
        max: 3600,
        unit: 's',
        sliderClass: 'slider-purple',
      },
      {
        label: 'PPT 功耗限制 (RSMU)',
        key: 'PptLimitRsmu',
        min: 0,
        max: 200,
        unit: 'W',
        sliderClass: 'slider-purple',
      },
    ],
  },
  {
    title: '电流限制 (Current Limits)',
    items: [
      {
        label: 'VRM 持续电流限制 (MP1)',
        key: 'VrmCurrentMp1',
        min: 0,
        max: 300000,
        step: 1000,
        unit: 'mA',
        sliderClass: 'slider-blue',
      },
      {
        label: 'VRM 持续电流限制 (RSMU)',
        key: 'VrmCurrentRsmu',
        min: 0,
        max: 300000,
        step: 1000,
        unit: 'mA',
        sliderClass: 'slider-blue',
      },
      {
        label: 'EDC 瞬间电流限制 (MP1)',
        key: 'EdcLimitMp1',
        min: 0,
        max: 300000,
        step: 1000,
        unit: 'mA',
        sliderClass: 'slider-blue',
      },
      {
        label: 'EDC 瞬间电流限制 (RSMU)',
        key: 'EdcLimitRsmu',
        min: 0,
        max: 300000,
        step: 1000,
        unit: 'mA',
        sliderClass: 'slider-blue',
      },
    ],
  },
  {
    title: '温度控制 (Thermal Control)',
    items: [
      {
        label: '温度墙限制 (MP1)',
        key: 'TempLimitMp1',
        min: 40,
        max: 115,
        unit: '℃',
        sliderClass: 'slider-red',
      },
      {
        label: '温度墙限制 (RSMU)',
        key: 'TempLimitRsmu',
        min: 40,
        max: 115,
        unit: '℃',
        sliderClass: 'slider-red',
      },
    ],
  },
  {
    title: '时钟与超频 (Clocks & OC)',
    items: [
      {
        label: 'PBO 倍率上限选择',
        key: 'PboScalar',
        min: 1,
        max: 100,
        unit: 'x',
        sliderClass: 'slider-purple',
      },
      {
        label: '超频核心频率偏移',
        key: 'OcClk',
        min: -500,
        max: 500,
        step: 25,
        unit: 'MHz',
        sliderClass: 'slider-purple',
      },
      {
        label: '超频核心电压设定',
        key: 'OcVolt',
        min: 0,
        max: 1550,
        step: 5,
        unit: 'mV',
        sliderClass: 'slider-purple',
      },
    ],
  },
]

const loadingMap = reactive<Record<string, boolean>>({})
const configStore = useConfigStore()
if (!configStore.config) {
  await configStore.fetchConfig()
}
const smuData = computed(() => configStore.config?.Smu)

// Physical core count fetched from backend (excludes hyperthreading)
const coreCount = ref(0)
const cpuName = ref('AMD Ryzen')
const cpuCoreInfo = ref('')

const perCoreCurve = reactive<number[]>([])
const perCoreOcClk = reactive<number[]>([])

watch(
  coreCount,
  (newCount) => {
    if (newCount <= 0) return
    const currentLen = perCoreCurve.length
    if (newCount > currentLen) {
      for (let i = currentLen; i < newCount; i++) {
        perCoreCurve.push(0)
        perCoreOcClk.push(0)
      }
    } else if (newCount < currentLen) {
      perCoreCurve.splice(newCount)
      perCoreOcClk.splice(newCount)
    }
  },
  { immediate: true },
)

const applySetting = async (methodName: keyof typeof RyzenSmu, ...args: number[]) => {
  loadingMap[methodName] = true
  try {
    const fn = RyzenSmu[methodName] as unknown as (
      ...methodArgs: number[]
    ) => Promise<CommandResult>
    const res = await fn(...args)

    if (res.Success) {
      Message.success(res.Message || '应用成功')
    } else {
      Message.error(res.Message || '应用失败')
    }
    configStore.debouncedSave()
  } catch (e) {
    Message.error('应用执行失败')
    console.error(e)
  } finally {
    loadingMap[methodName] = false
  }
}

// ====== Real-time SMU Telemetry ======
const HISTORY_LEN = 24
const telemetry = ref<SmuTelemetry>({ Ppt: 0, Tdc: null, Edc: null, Temp: 0, FreqMhz: 0, Usage: 0 })
const pptHistory = ref<number[]>(Array(HISTORY_LEN).fill(0))
const tdcHistory = ref<number[]>(Array(HISTORY_LEN).fill(0))
const edcHistory = ref<number[]>(Array(HISTORY_LEN).fill(0))
const tempHistory = ref<number[]>(Array(HISTORY_LEN).fill(0))

function pushHistory(arr: number[], value: number) {
  arr.push(value)
  if (arr.length > HISTORY_LEN) arr.shift()
}

function sparkline(history: number[], yMax: number): { line: string; area: string } {
  if (history.length < 2) return { line: 'M 0 40', area: 'M 0 40 L 160 40 L 0 40 Z' }
  const W = 160,
    H = 40
  const points = history.map((v, i) => ({
    x: (i / (HISTORY_LEN - 1)) * W,
    y: H - (Math.max(0, Math.min(v, yMax)) / yMax) * H,
  }))
  const line = points
    .map((p, i) => {
      if (i === 0) return `M ${p.x},${p.y}`
      const prev = points[i - 1]
      const cpx = (prev!.x + p.x) / 2
      return `C ${cpx},${prev!.y} ${cpx},${p.y} ${p.x},${p.y}`
    })
    .join(' ')
  const area = `${line} L ${W},${H} L 0,${H} Z`
  return { line, area }
}

const pptChart = computed(() => sparkline(pptHistory.value, 150))
const tdcChart = computed(() => sparkline(tdcHistory.value, 300))
const edcChart = computed(() => sparkline(edcHistory.value, 400))
const tempChart = computed(() => sparkline(tempHistory.value, 110))

let pollingTimer: ReturnType<typeof setInterval> | null = null

async function fetchTelemetry() {
  try {
    const res = await RyzenSmu.GetSmuTelemetry()
    if (res.Success && res.Data) {
      telemetry.value = res.Data
      pushHistory(pptHistory.value, res.Data.Ppt)
      pushHistory(tdcHistory.value, res.Data.Tdc ?? 0)
      pushHistory(edcHistory.value, res.Data.Edc ?? 0)
      pushHistory(tempHistory.value, res.Data.Temp)
    }
  } catch {
    // silent fail — telemetry is best-effort
  }
}

onMounted(async () => {
  // Fetch real physical core count (no hyperthreading)
  try {
    const coreRes = await CPU.GetPhysicalCoreCount()
    if (coreRes.Success && coreRes.Data > 0) {
      coreCount.value = coreRes.Data
    } else {
      coreCount.value = 8 // safe fallback
    }
  } catch {
    coreCount.value = 8
  }

  // Fetch CPU name for display
  try {
    const infoRes = await CPU.GetCpuInfo()
    if (infoRes.Success && infoRes.Data) {
      cpuName.value = infoRes.Data.Name || 'AMD Ryzen'
      cpuCoreInfo.value = `${infoRes.Data.Cores} 核心 / ${infoRes.Data.Threads} 线程`
    }
  } catch {
    /* ignore */
  }

  fetchTelemetry()
  pollingTimer = setInterval(fetchTelemetry, POLL_INTERVAL_SMU)
})

onUnmounted(() => {
  if (pollingTimer) clearInterval(pollingTimer)
})
</script>

<template>
  <div v-if="smuData" class="h-full overflow-y-auto text-white p-6 no-scrollbar">
    <div class="max-w-[1300px] mx-auto flex flex-col lg:flex-row gap-6">
      <!-- ==================== 左/中：高级电源、频率微调区 ==================== -->
      <div class="flex-1 space-y-6">
        <!-- 头部标题 -->
        <div>
          <h1 class="text-2xl font-bold tracking-wide">Ryzen SMU</h1>
          <p class="text-[13px] text-gray-500 mt-1">
            高级电源、电流及频率限制调整 (AMD Ryzen 平台专用)
          </p>
        </div>

        <div class="grid grid-cols-1 md:grid-cols-2 gap-5">
          <!-- 动态生成的配置卡片（分成左右两个大组排布更整齐） -->
          <div
            v-for="group in CONFIG_GROUPS"
            :key="group.title"
            class="bg-[#121320]/60 backdrop-blur-md border border-white/[0.05] rounded-xl p-5 shadow-lg flex flex-col justify-between"
          >
            <div>
              <h3
                class="text-xs font-black text-purple-400 uppercase tracking-widest mb-5 border-l-4 border-purple-600 pl-2.5"
              >
                {{ group.title }}
              </h3>

              <div class="space-y-5">
                <div v-for="item in group.items" :key="item.key" class="space-y-1.5">
                  <div class="flex justify-between items-center text-[11px]">
                    <span class="text-gray-400">{{ item.label }}</span>
                    <span class="text-white font-mono font-medium"
                      >{{ smuData[item.key] }} {{ item.unit }}</span
                    >
                  </div>
                  <div class="flex items-center gap-4">
                    <a-slider
                      v-model="smuData[item.key]"
                      :min="item.min"
                      :max="item.max"
                      :step="item.step || 1"
                      class="flex-1"
                      :class="item.sliderClass"
                    />
                    <a-button
                      type="primary"
                      size="small"
                      class="!bg-purple-600/10 !text-purple-400 !border-purple-500/20 hover:!bg-purple-600 hover:!text-white rounded-md px-3 font-semibold transition"
                      :loading="loadingMap[item.key]"
                      @click="
                        applySetting(('Set' + item.key) as keyof typeof RyzenSmu, smuData[item.key])
                      "
                      >应用</a-button
                    >
                  </div>
                </div>
              </div>
            </div>

            <!-- 仅在时钟与超频面板底部显示 OC 开关 -->
            <div
              v-if="group.title.includes('Clocks')"
              class="mt-6 flex gap-3 pt-5 border-t border-white/[0.03]"
            >
              <a-button
                type="primary"
                class="flex-1 !rounded-lg font-bold !bg-emerald-600/20 !text-emerald-400 !border-emerald-500/20 hover:!bg-emerald-600 hover:!text-white"
                :loading="loadingMap['EnableOc']"
                @click="applySetting('EnableOc')"
                >启用超频</a-button
              >
              <a-button
                type="primary"
                class="flex-1 !rounded-lg font-bold !bg-rose-600/20 !text-rose-400 !border-rose-500/20 hover:!bg-rose-600 hover:!text-white"
                :loading="loadingMap['DisableOc']"
                @click="applySetting('DisableOc')"
                >禁用超频</a-button
              >
            </div>
          </div>
        </div>

        <!-- 下部分割栏（Curve Optimizer 与 单核超频） -->
        <div class="grid grid-cols-1 md:grid-cols-2 gap-5">
          <!-- Curve Optimizer 面板 -->
          <div
            class="bg-[#121320]/60 backdrop-blur-md border border-white/[0.05] rounded-xl p-5 shadow-lg flex flex-col justify-between"
          >
            <div>
              <div class="flex justify-between items-center mb-4">
                <h3
                  class="text-xs font-black text-orange-400 uppercase tracking-widest border-l-4 border-orange-500 pl-2.5"
                >
                  Curve Optimizer 曲线优化
                </h3>
                <div class="flex items-center gap-2">
                  <span class="text-[9px] font-bold text-gray-500 uppercase">Cores</span>
                  <a-input-number
                    v-model="coreCount"
                    :min="1"
                    :max="64"
                    size="mini"
                    class="!w-12 !bg-white/5 !border-white/10 !text-white rounded-md"
                    hide-button
                  />
                </div>
              </div>

              <!-- 全核偏移调节块 -->
              <div class="bg-white/[0.02] border border-white/[0.04] p-3.5 rounded-lg mb-4">
                <div class="flex justify-between items-center mb-1 text-[11px]">
                  <span class="font-bold text-gray-300">All Core Offset (全核心偏移量)</span>
                  <span class="font-mono text-orange-400 font-semibold">{{
                    smuData.CurveOptimizerAll
                  }}</span>
                </div>
                <div class="flex items-center gap-4">
                  <a-slider
                    v-model="smuData.CurveOptimizerAll"
                    :min="-100"
                    :max="100"
                    class="flex-1 slider-orange"
                  />
                  <a-button
                    type="primary"
                    size="small"
                    class="!bg-orange-600/10 !text-orange-400 !border-orange-500/25 hover:!bg-orange-600 hover:!text-white rounded-md px-3 font-semibold transition"
                    :loading="loadingMap['SetCurveOptimizerAll']"
                    @click="applySetting('SetCurveOptimizerAll', smuData.CurveOptimizerAll)"
                    >应用</a-button
                  >
                </div>
              </div>

              <!-- 单核优化矩阵 -->
              <div class="grid grid-cols-2 gap-2 max-h-[160px] overflow-y-auto no-scrollbar">
                <div
                  v-for="(_, index) in perCoreCurve"
                  :key="index"
                  class="bg-white/[0.02] p-2.5 rounded-lg border border-white/[0.03] flex items-center justify-between"
                >
                  <span class="text-[9px] font-bold text-gray-500 uppercase">CORE {{ index }}</span>
                  <div class="flex items-center gap-1.5">
                    <a-input-number
                      v-model="perCoreCurve[index]"
                      :min="-50"
                      :max="50"
                      size="mini"
                      class="!w-10 !bg-transparent !border-none !text-white p-0 text-center font-mono"
                      hide-button
                    />
                    <button
                      class="w-5 h-5 bg-orange-600/10 text-orange-400 hover:bg-orange-600 hover:text-white transition-colors border border-orange-500/20 rounded flex items-center justify-center text-[10px]"
                      @click="
                        applySetting('SetCurveOptimizerPerCore', index, perCoreCurve[index] ?? 0)
                      "
                    >
                      ✓
                    </button>
                  </div>
                </div>
              </div>
            </div>
          </div>

          <!-- Per Core OC Clocks 面板 -->
          <div
            class="bg-[#121320]/60 backdrop-blur-md border border-white/[0.05] rounded-xl p-5 shadow-lg flex flex-col justify-between"
          >
            <div>
              <h3
                class="text-xs font-black text-blue-400 uppercase tracking-widest mb-4 border-l-4 border-blue-500 pl-2.5"
              >
                Per Core OC Clocks (单核超频限制)
              </h3>

              <div class="grid grid-cols-2 gap-2 max-h-[240px] overflow-y-auto no-scrollbar">
                <div
                  v-for="(_, index) in perCoreOcClk"
                  :key="index"
                  class="bg-white/[0.02] p-2.5 rounded-lg border border-white/[0.03] flex items-center justify-between"
                >
                  <span class="text-[9px] font-bold text-gray-500 uppercase">CORE {{ index }}</span>
                  <div class="flex items-center gap-1.5">
                    <a-input-number
                      v-model="perCoreOcClk[index]"
                      :min="0"
                      :max="1000"
                      :step="25"
                      size="mini"
                      class="!w-12 !bg-transparent !border-none !text-white p-0 text-center font-mono"
                      hide-button
                    />
                    <button
                      class="w-5 h-5 bg-blue-600/10 text-blue-400 hover:bg-blue-600 hover:text-white transition-colors border border-blue-500/20 rounded flex items-center justify-center text-[10px]"
                      @click="applySetting('SetPerCoreOcClk', index, perCoreOcClk[index] ?? 0)"
                    >
                      ✓
                    </button>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- ==================== 右侧：处理器信息与电源遥测栏 ==================== -->
      <div class="w-full lg:w-[360px] shrink-0 space-y-6 lg:pt-[115px]">
        <!-- 1. AMD Ryzen 处理器芯片详情 -->
        <div
          class="bg-[#121320]/60 backdrop-blur-md border border-white/[0.05] rounded-xl p-5 shadow-lg"
        >
          <h2 class="text-[13px] font-semibold text-gray-300 mb-4">Ryzen 芯片架构</h2>
          <div class="flex items-center gap-4">
            <!-- AM5 Socket 异形芯片 SVG 绘制 -->
            <div
              class="w-16 h-16 bg-white/[0.02] border border-white/[0.05] rounded-xl flex items-center justify-center relative shrink-0"
            >
                            <CpuDie />
            </div>

            <div class="space-y-1 text-[11px] text-gray-400">
              <div class="text-[13px] font-bold text-white">
                <span v-if="cpuName">{{ cpuName }}</span>
                <span v-else class="text-gray-600 animate-pulse">检测中...</span>
              </div>
              <div>AMD Ryzen 架构 / AM5 接口</div>
              <div>
                <span v-if="cpuCoreInfo">{{ cpuCoreInfo }}</span>
                <span v-else class="text-gray-600 animate-pulse">{{
                  coreCount > 0 ? `${coreCount} 物理核心` : '检测中...'
                }}</span>
              </div>
              <div>Curve Optimizer 已加载 {{ coreCount }} 核</div>
              <div>支持 PBO2 曲线优化</div>
            </div>
          </div>
        </div>

        <!-- 2. 电源实时监视器（遥测 PPT / TDC / EDC 波形图） -->
        <div
          class="bg-[#121320]/60 backdrop-blur-md border border-white/[0.05] rounded-xl p-5 shadow-lg space-y-4"
        >
          <div class="flex items-center justify-between">
            <h2 class="text-[13px] font-semibold text-gray-300">SMU 电源遥测</h2>
            <span
              class="text-[10px] text-gray-600 bg-white/[0.03] border border-white/[0.05] px-2 py-0.5 rounded-full"
              >{{ telemetry.FreqMhz }} MHz · {{ telemetry.Usage }}% 负载</span
            >
          </div>

          <div class="grid grid-cols-2 gap-3">
            <!-- PPT 功耗 -->
            <div
              class="bg-white/[0.02] border border-white/[0.04] p-3 rounded-lg flex flex-col justify-between"
            >
              <div>
                <span class="text-[10px] text-gray-500 block">PPT 封装功耗</span>
                <span class="text-base font-bold text-white font-mono"
                  >{{ telemetry.Ppt.toFixed(1) }}
                  <span class="text-[10px] text-gray-500 font-bold">W</span></span
                >
              </div>
              <svg
                class="w-full h-8 opacity-80 mt-1"
                viewBox="0 0 160 40"
                preserveAspectRatio="none"
              >
                <defs>
                  <linearGradient id="smu-g-purple" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="0%" stop-color="#8A2BE2" stop-opacity="0.35" />
                    <stop offset="100%" stop-color="#8A2BE2" stop-opacity="0" />
                  </linearGradient>
                </defs>
                <path
                  :d="pptChart.line"
                  fill="none"
                  stroke="#8A2BE2"
                  stroke-width="1.5"
                  stroke-linecap="round"
                />
                <path :d="pptChart.area" fill="url(#smu-g-purple)" />
              </svg>
            </div>

            <!-- TDC 长期电流 -->
            <div
              class="bg-white/[0.02] border border-white/[0.04] p-3 rounded-lg flex flex-col justify-between"
            >
              <div>
                <span class="text-[10px] text-gray-500 block">TDC 供电电流</span>
                <span class="text-base font-bold text-white font-mono"
                  >{{ (telemetry.Tdc ?? 0).toFixed(1) }}
                  <span class="text-[10px] text-gray-500 font-bold">A</span></span
                >
              </div>
              <svg
                class="w-full h-8 opacity-80 mt-1"
                viewBox="0 0 160 40"
                preserveAspectRatio="none"
              >
                <defs>
                  <linearGradient id="smu-g-blue" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="0%" stop-color="#3B82F6" stop-opacity="0.35" />
                    <stop offset="100%" stop-color="#3B82F6" stop-opacity="0" />
                  </linearGradient>
                </defs>
                <path
                  :d="tdcChart.line"
                  fill="none"
                  stroke="#3B82F6"
                  stroke-width="1.5"
                  stroke-linecap="round"
                />
                <path :d="tdcChart.area" fill="url(#smu-g-blue)" />
              </svg>
            </div>

            <!-- EDC 瞬间电流 -->
            <div
              class="bg-white/[0.02] border border-white/[0.04] p-3 rounded-lg flex flex-col justify-between"
            >
              <div>
                <span class="text-[10px] text-gray-500 block">EDC 峰值电流</span>
                <span class="text-base font-bold text-white font-mono"
                  >{{ (telemetry.Edc ?? 0).toFixed(1) }}
                  <span class="text-[10px] text-gray-500 font-bold">A</span></span
                >
              </div>
              <svg
                class="w-full h-8 opacity-80 mt-1"
                viewBox="0 0 160 40"
                preserveAspectRatio="none"
              >
                <defs>
                  <linearGradient id="smu-g-orange" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="0%" stop-color="#FF7D00" stop-opacity="0.35" />
                    <stop offset="100%" stop-color="#FF7D00" stop-opacity="0" />
                  </linearGradient>
                </defs>
                <path
                  :d="edcChart.line"
                  fill="none"
                  stroke="#FF7D00"
                  stroke-width="1.5"
                  stroke-linecap="round"
                />
                <path :d="edcChart.area" fill="url(#smu-g-orange)" />
              </svg>
            </div>

            <!-- 核心温度 -->
            <div
              class="bg-white/[0.02] border border-white/[0.04] p-3 rounded-lg flex flex-col justify-between"
            >
              <div>
                <span class="text-[10px] text-gray-500 block">核心温度</span>
                <span
                  class="text-base font-bold font-mono"
                  :class="
                    telemetry.Temp > 90
                      ? 'text-red-400'
                      : telemetry.Temp > 75
                        ? 'text-orange-400'
                        : 'text-white'
                  "
                  >{{ telemetry.Temp.toFixed(1) }}
                  <span class="text-[10px] text-gray-500 font-bold">°C</span></span
                >
              </div>
              <svg
                class="w-full h-8 opacity-80 mt-1"
                viewBox="0 0 160 40"
                preserveAspectRatio="none"
              >
                <defs>
                  <linearGradient id="smu-g-red" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="0%" stop-color="#EF4444" stop-opacity="0.35" />
                    <stop offset="100%" stop-color="#EF4444" stop-opacity="0" />
                  </linearGradient>
                </defs>
                <path
                  :d="tempChart.line"
                  fill="none"
                  stroke="#EF4444"
                  stroke-width="1.5"
                  stroke-linecap="round"
                />
                <path :d="tempChart.area" fill="url(#smu-g-red)" />
              </svg>
            </div>
          </div>
        </div>

        <!-- 3. 技术名释说明 -->
        <div
          class="bg-[#121320]/60 backdrop-blur-md border border-white/[0.05] rounded-xl p-5 shadow-lg space-y-2.5"
        >
          <h2 class="text-[13px] font-semibold text-gray-300">名词解释</h2>
          <div class="text-[11px] text-gray-500 leading-relaxed space-y-2">
            <p>
              <strong>STAPM</strong>: 根据设备表面温度自适应调整 CPU
              功耗分配（在移动端设备和掌机上尤为明显）。
            </p>
            <p>
              <strong>Curve Optimizer (PBO2)</strong>:
              通过调校不同内核的电压频率曲线（降压超频），能实现在更低温度下达到更高运行频率的目标。
            </p>
            <p>
              <strong>RSMU / MP1</strong>:
              芯片内部不同模块的系统级微处理器，两者的限制参数相互协调限制。
            </p>
          </div>
          <a
            target="_blank"
            href="https://www.amd.com/zh-cn/developer/browse-by-resource-type/documentation.html"
            class="text-[11px] text-blue-400 hover:text-blue-300 cursor-pointer pt-1 flex items-center gap-0.5 font-medium transition-colors"
          >
            参考 AMD PBO 手册
          </a>
        </div>
      </div>
    </div>
  </div>
  <div v-else class="flex items-center justify-center h-full">
    <a-spin dot />
  </div>
</template>

<style scoped lang="scss">
/* 隐藏自定义滚动条 */
.no-scrollbar::-webkit-scrollbar {
  display: none;
}
.no-scrollbar {
  -ms-overflow-style: none;
  scrollbar-width: none;
}

/* 分色重写 Slider 轨道（紫 / 蓝 / 红 / 橘） */
:deep(.slider-purple .arco-slider-bar) {
  background: linear-gradient(90deg, #6366f1 0%, #8a2be2 100%) !important;
  height: 5px !important;
  border-radius: 99px;
}
:deep(.slider-purple .arco-slider-button) {
  border: 2px solid #8a2be2 !important;
  box-shadow: 0 0 8px rgba(138, 43, 226, 0.6) !important;
}

:deep(.slider-blue .arco-slider-bar) {
  background: linear-gradient(90deg, #3b82f6 0%, #1d4ed8 100%) !important;
  height: 5px !important;
  border-radius: 99px;
}
:deep(.slider-blue .arco-slider-button) {
  border: 2px solid #3b82f6 !important;
  box-shadow: 0 0 8px rgba(59, 130, 246, 0.6) !important;
}

:deep(.slider-red .arco-slider-bar) {
  background: linear-gradient(90deg, #f43f5e 0%, #e11d48 100%) !important;
  height: 5px !important;
  border-radius: 99px;
}
:deep(.slider-red .arco-slider-button) {
  border: 2px solid #e11d48 !important;
  box-shadow: 0 0 8px rgba(225, 29, 72, 0.6) !important;
}

:deep(.slider-orange .arco-slider-bar) {
  background: linear-gradient(90deg, #ff7d00 0%, #ff5000 100%) !important;
  height: 5px !important;
  border-radius: 99px;
}
:deep(.slider-orange .arco-slider-button) {
  border: 2px solid #ff7d00 !important;
  box-shadow: 0 0 8px rgba(255, 125, 0, 0.6) !important;
}

/* 深色模式下拉选择框 */
:deep(.arco-select-view-single) {
  background-color: #17192a !important;
  border: 1px solid rgba(255, 255, 255, 0.05) !important;
  color: #ffffff !important;
  border-radius: 6px !important;
  height: 28px !important;
}
</style>
