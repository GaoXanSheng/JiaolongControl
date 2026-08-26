<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import VChart from 'vue-echarts'
import { use } from 'echarts/core'
import { CanvasRenderer } from 'echarts/renderers'
import { LineChart } from 'echarts/charts'
import { GridComponent, LegendComponent, TooltipComponent } from 'echarts/components'
import { PerformanceMode, SystemInfo, SystemPerMode } from '@/utils/bridge'
import { useSystemInfoStore } from '@/stores/systemInfo'
import { storeToRefs } from 'pinia'
import imgCPU from '@/assets/icon/iconCPU.png'
import imgGPU from '@/assets/icon/gpu2.png'
import imgFan from '@/assets/icon/iconFan.png'
import iconQuiet from '@/assets/icon/iconQuiet.png'
import iconBalanced from '@/assets/icon/iconBalanced.png'
import iconPerformance from '@/assets/icon/iconPerformance.png'
import PerformanceModeComp from './Home/PerformanceMode.vue'
import CoreMonitoringComp from './Home/CoreMonitoring.vue'
import WelcomeBannerComp from './Home/WelcomeBanner.vue'

use([CanvasRenderer, LineChart, GridComponent, TooltipComponent, LegendComponent])

const systemInfoStore = useSystemInfoStore()
const { cpuTemp, gpuTemp, fanSpeed, gpuStats } = storeToRefs(systemInfoStore)

// 枚举↔中文映射以后端为准: 0=Performance(高性能) 1=Quiet(静音) 2=Balance(平衡)
const performanceModes = ref([
  { id: SystemPerMode.PerformanceMode, name: '高性能', icon: iconPerformance, active: false },
  { id: SystemPerMode.QuietMode, name: '静音', icon: iconQuiet, active: false },
  { id: SystemPerMode.BalanceMode, name: '平衡', icon: iconBalanced, active: false },
])

const activeMode = computed(
  () => performanceModes.value.find((m) => m.active) || { name: '高性能', icon: iconPerformance },
)

async function fetchPerformanceMode() {
  try {
    const res = await PerformanceMode.Get()
    if (res.Success) {
      performanceModes.value.forEach((e) => {
        e.active = e.id === res.Data
      })
    }
  } catch (e) {
    console.error(e)
  }
}

function setMode(id: SystemPerMode) {
  performanceModes.value.forEach((m) => {
    m.active = m.id === id
    if (id !== SystemPerMode.CustomMode) {
      PerformanceMode.Set(id)
    }
  })
}

const cpuUsage = computed(() => systemInfoStore.cpuStats?.Usage ?? 0)
const gpuUsage = computed(() => parseInt(gpuStats.value?.GpuUtilization || '0', 10))
// 无噪音传感器: 以风扇转速推算 (标定: 约 3000 RPM ≈ 25 dBA, 静止底噪 20 dBA)
const noiseLevel = computed(() => {
  const rpm = Math.max(fanSpeed.value.CPUFanSpeed, fanSpeed.value.GPUFanSpeed)
  return Math.round(20 + (rpm / 3000) * 5)
})

const sysCpuName = ref('Loading...')
const sysGpuName = ref('Loading...')
const sysMemory = ref('Loading...')
const sysOs = ref('Loading...')

// 模拟历史数据
const tempHistory = ref<{ cpu: number | null; gpu: number | null }[]>(
  Array(10).fill({ cpu: null, gpu: null }),
)

// 心电图数据与生成逻辑
const ecgData = ref<number[]>(Array(50).fill(30))
let ecgIndex = 0
const ecgPattern = [0, 0, 0, 0, -2, 2, -25, 12, -2, -6, 0, 0, 0, 0, 0, 0]

function tickEcg() {
  const offset = ecgPattern[ecgIndex] ?? 0
  ecgIndex = (ecgIndex + 1) % ecgPattern.length
  const noise = (Math.random() - 0.5) * 1.5
  const nextY = 30 + offset + noise
  ecgData.value.push(nextY)
  if (ecgData.value.length > 50) {
    ecgData.value.shift()
  }
}

const ecgPointsString = computed(() => {
  return ecgData.value
    .map((y, i) => {
      const x = (i / (ecgData.value.length - 1)) * 300
      return `${x.toFixed(1)},${y.toFixed(1)}`
    })
    .join(' ')
})

async function fetchStaticInfo() {
  try {
    const res = await SystemInfo.GetSystemOverview()
    if (res.Success && res.Data) {
      sysCpuName.value = res.Data.CpuName
      sysGpuName.value = res.Data.GpuName
      sysMemory.value = res.Data.MemoryInfo
      sysOs.value = res.Data.OsVersion
    }
  } catch (e) {
    console.error('Failed to fetch system info', e)
  }
}

let ecgTimer: ReturnType<typeof setInterval> | null = null
let historyTimer: ReturnType<typeof setInterval> | null = null

function startTimers() {
  stopTimers()
  ecgTimer = setInterval(tickEcg, 100)
  historyTimer = setInterval(() => {
    tempHistory.value.push({ cpu: cpuTemp.value, gpu: gpuTemp.value })
    if (tempHistory.value.length > 10) tempHistory.value.shift()
  }, 2000)
}

function stopTimers() {
  if (ecgTimer) {
    clearInterval(ecgTimer)
    ecgTimer = null
  }
  if (historyTimer) {
    clearInterval(historyTimer)
    historyTimer = null
  }
}

function handleVisibilityChange() {
  if (document.hidden) {
    stopTimers()
  } else {
    startTimers()
  }
}

onMounted(() => {
  fetchStaticInfo()
  fetchPerformanceMode()
  startTimers()
  document.addEventListener('visibilitychange', handleVisibilityChange)
})

onUnmounted(() => {
  stopTimers()
  document.removeEventListener('visibilitychange', handleVisibilityChange)
})

// 2. 温度曲线 - 折线图
const lineChartOption = computed(() => ({
  grid: { top: 30, bottom: 20, left: 35, right: 10 },
  legend: {
    data: ['CPU', 'GPU'],
    icon: 'roundRect',
    itemWidth: 12,
    itemHeight: 4,
    textStyle: { color: '#A0AEC0', fontSize: 10 },
    top: 0,
  },
  xAxis: {
    type: 'category',
    data: Array(10).fill(''),
    axisLine: { show: false },
    axisTick: { show: false },
    axisLabel: { color: '#6B7280', fontSize: 10, margin: 12 },
  },
  yAxis: {
    type: 'value',
    min: 0,
    max: 100,
    interval: 25,
    splitLine: { lineStyle: { color: 'rgba(255, 255, 255, 0.05)' } },
    axisLabel: { color: '#6B7280', fontSize: 10, formatter: '{value}°C' },
  },
  series: [
    {
      name: 'CPU',
      data: tempHistory.value.map((i) => i.cpu),
      type: 'line',
      smooth: true,
      symbol: 'circle',
      symbolSize: 6,
      lineStyle: { color: '#3B82F6', width: 3 },
      itemStyle: { color: '#3B82F6' },
    },
    {
      name: 'GPU',
      data: tempHistory.value.map((i) => i.gpu),
      type: 'line',
      smooth: true,
      symbol: 'circle',
      symbolSize: 6,
      lineStyle: { color: '#10B981', width: 3 },
      itemStyle: { color: '#10B981' },
    },
  ],
}))
</script>

<template>
  <div class="p-6 h-full overflow-y-auto space-y-6 text-white no-scrollbar">
    <!-- Row 1: 顶部 Banner -->
    <WelcomeBannerComp
      :cpu-temp="cpuTemp"
      :gpu-temp="gpuTemp"
      :active-mode-name="activeMode.name"
      :active-mode-icon="activeMode.icon"
    />

    <!-- Row 2: 模式 & 监控 -->
    <div class="grid grid-cols-12 gap-3 h-[250px]">
      <PerformanceModeComp :modes="performanceModes" @change-mode="setMode" />
      <CoreMonitoringComp
        :cpu-usage="cpuUsage"
        :gpu-usage="gpuUsage"
        :cpu-temp="cpuTemp"
        :gpu-temp="gpuTemp"
      />
    </div>

    <!-- Row 3: 系统概览 & 风扇 & 曲线 -->
    <div class="grid grid-cols-12 gap-6 h-[280px]">
      <!-- 系统概览 -->
      <div class="col-span-4 glass-card p-6 flex flex-col">
        <h2 class="text-[15px] font-medium text-white/90 mb-4">系统概览</h2>
        <div class="flex-1 flex flex-col justify-between">
          <div class="flex items-center gap-4">
            <div
              class="w-8 h-8 rounded-full bg-blue-900/30 flex items-center justify-center text-blue-500"
            >
              <img
                :src="imgCPU"
                class="w-4 h-4"
                style="filter: invert(48%) sepia(79%) saturate(2476%) hue-rotate(190deg)"
              />
            </div>
            <div>
              <div class="text-xs text-white/90">CPU</div>
              <div class="text-xs text-gray-500 mt-0.5">{{ sysCpuName }}</div>
            </div>
          </div>
          <div class="flex items-center gap-4">
            <div
              class="w-8 h-8 rounded-full bg-green-900/30 flex items-center justify-center text-green-500"
            >
              <img
                :src="imgGPU"
                class="w-4 h-4"
                style="filter: invert(57%) sepia(52%) saturate(2859%) hue-rotate(120deg)"
              />
            </div>
            <div>
              <div class="text-xs text-white/90">GPU</div>
              <div class="text-xs text-gray-500 mt-0.5">{{ sysGpuName }}</div>
            </div>
          </div>
          <div class="flex items-center gap-4">
            <div
              class="w-8 h-8 rounded-full bg-yellow-900/30 flex items-center justify-center text-yellow-500"
            >
              <icon-storage />
            </div>
            <div>
              <div class="text-xs text-white/90">内存</div>
              <div class="text-xs text-gray-500 mt-0.5">{{ sysMemory }}</div>
            </div>
          </div>
          <div class="flex items-center gap-4">
            <div
              class="w-8 h-8 rounded-full bg-red-900/30 flex items-center justify-center text-red-500"
            >
              <icon-computer />
            </div>
            <div>
              <div class="text-xs text-white/90">系统</div>
              <div class="text-xs text-gray-500 mt-0.5">{{ sysOs }}</div>
            </div>
          </div>
        </div>
      </div>

      <!-- 风扇与噪音 -->
      <div class="col-span-4 glass-card p-6 flex flex-col">
        <h2 class="text-[15px] font-medium text-white/90 mb-4">风扇与噪音</h2>

        <div class="flex items-center gap-4 mb-6">
          <div
            class="w-12 h-12 rounded-full bg-blue-600/20 flex items-center justify-center overflow-hidden"
          >
            <img
              :src="imgFan"
              class="w-7 h-7 object-contain animate-spin"
              style="
                animation-duration: 3s;
                filter: invert(48%) sepia(79%) saturate(2476%) hue-rotate(190deg) brightness(118%)
                  contrast(119%);
              "
            />
          </div>
          <div>
            <div class="flex items-baseline gap-1">
              <span class="text-3xl font-semibold">{{
                Math.max(fanSpeed.CPUFanSpeed, fanSpeed.GPUFanSpeed)
              }}</span>
              <span class="text-xs text-gray-400">RPM</span>
            </div>
            <div class="text-xs text-gray-500">风扇转速</div>
          </div>
        </div>

        <!-- 模拟心电图 (ECG) -->
        <div class="h-16 flex items-center justify-center mb-6 overflow-hidden">
          <svg class="w-full h-full" viewBox="0 0 300 60" preserveAspectRatio="none">
            <defs>
              <!-- 水平方向渐变色，从紫色过渡到蓝色，再到浅绿 -->
              <linearGradient id="ecgGrad" x1="0%" y1="0%" x2="100%" y2="0%">
                <stop offset="0%" stop-color="#8A2BE2" stop-opacity="0.3" />
                <stop offset="50%" stop-color="#3B82F6" stop-opacity="0.8" />
                <stop offset="100%" stop-color="#10B981" stop-opacity="1" />
              </linearGradient>
              <!-- 霓虹发光滤镜 -->
              <filter id="glow" x="-20%" y="-20%" width="140%" height="140%">
                <feGaussianBlur stdDeviation="1.2" result="blur" />
                <feMerge>
                  <feMergeNode in="blur" />
                  <feMergeNode in="SourceGraphic" />
                </feMerge>
              </filter>
            </defs>
            <polyline
              fill="none"
              stroke="url(#ecgGrad)"
              stroke-width="2"
              stroke-linecap="round"
              stroke-linejoin="round"
              filter="url(#glow)"
              :points="ecgPointsString"
            />
          </svg>
        </div>

        <div class="mt-auto">
          <div class="flex items-baseline gap-1">
            <span class="text-2xl font-semibold">{{ noiseLevel }}</span>
            <span class="text-xs text-gray-400">dBA</span>
          </div>
          <div class="text-xs text-gray-500">当前噪音 (估算)</div>
        </div>
      </div>

      <!-- 温度曲线 -->
      <div class="col-span-4 glass-card p-6 flex flex-col">
        <h2 class="text-[15px] font-medium text-white/90 mb-2">温度曲线</h2>
        <div class="flex-1">
          <VChart :option="lineChartOption" autoresize />
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.echarts {
  width: 100%;
  height: 100%;
}
</style>
