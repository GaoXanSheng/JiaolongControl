<script setup lang="ts">
import {computed, onMounted, onUnmounted, ref} from 'vue'
import VChart from 'vue-echarts'
import {use} from 'echarts/core'
import {CanvasRenderer} from 'echarts/renderers'
import {LineChart} from 'echarts/charts'
import {GridComponent, LegendComponent, TooltipComponent} from 'echarts/components'
import {CPU, Fan, NvidiaGpu, SystemInfo} from '@/utils/bridge'
import imgCPU from '@/assets/icon/iconCPU.png'
import imgGPU from '@/assets/icon/gpu2.png'
import imgFan from '@/assets/icon/iconFan.png'
import PerformanceModeComp from './HOME/PerformanceMode.vue'
import CoreMonitoringComp from './HOME/CoreMonitoring.vue'

use([CanvasRenderer, LineChart, GridComponent, TooltipComponent, LegendComponent])

const cpuTemp = ref(0)
const gpuTemp = ref(0)
const cpuUsage = ref(0)
const gpuUsage = ref(0)
const cpuFreq = ref('- GHz')
const gpuFreq = ref('- MHz')
const fanSpeed = ref(0)
const noiseLevel = ref(0)

const sysCpuName = ref('Loading...')
const sysGpuName = ref('Loading...')
const sysMemory = ref('Loading...')
const sysOs = ref('Loading...')

// 模拟历史数据
const tempHistory = ref([
  {cpu: 0, gpu: 0, mb: 0},
  {cpu: 0, gpu: 0, mb: 0},
  {cpu: 0, gpu: 0, mb: 0},
  {cpu: 0, gpu: 0, mb: 0},
  {cpu: 0, gpu: 0, mb: 0}
])

async function fetchStaticInfo() {
  try {
    const res = await SystemInfo.GetSystemOverview();
    if (res.Success && res.Data) {
      sysCpuName.value = res.Data.CpuName;
      sysGpuName.value = res.Data.GpuName;
      sysMemory.value = res.Data.MemoryInfo;
      sysOs.value = res.Data.OsVersion;
    }
  } catch (e) {
    console.error('Failed to fetch system info', e)
  }
}

async function updateHardwareInfo() {
  try {
    const [cTemp, fSpeed, gTemp] = await Promise.all([
      CPU.GetCPUThermometer(),
      Fan.GetFanSpeed(),
      NvidiaGpu.GetGpuTemperature()
    ])
    if (cTemp.Success) cpuTemp.value = cTemp.Data
    if (fSpeed.Success) fanSpeed.value = fSpeed.Data.CPUFanSpeed || 0
    if (gTemp.Success) gpuTemp.value = gTemp.Data
    cpuUsage.value = Math.floor(Math.random() * 30) + 10
    gpuUsage.value = Math.floor(Math.random() * 40) + 20
    noiseLevel.value = Math.floor(Math.random() * 10) + 30

    const now = new Date().toLocaleTimeString([], {
      hour12: false,
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit'
    })
    tempHistory.value.push({cpu: cpuTemp.value, gpu: gpuTemp.value, mb: cpuTemp.value - 5})
    if (tempHistory.value.length > 10) tempHistory.value.shift()
  } catch (e) {
    console.error(e)
  }
}

let timer: any = null
onMounted(() => {
  fetchStaticInfo();
  updateHardwareInfo();
  timer = setInterval(updateHardwareInfo, 2000)
})
onUnmounted(() => {
  if (timer) clearInterval(timer)
})

// 2. 温度曲线 - 折线图
const lineChartOption = computed(() => ({
  grid: {top: 30, bottom: 20, left: 35, right: 10},
  legend: {
    data: ['CPU', 'GPU', '主板'],
    icon: 'roundRect',
    itemWidth: 12,
    itemHeight: 4,
    textStyle: {color: '#A0AEC0', fontSize: 10},
    top: 0
  },
  xAxis: {
    type: 'category',
    data: ['0%', '25%', '50%', '75%', '100%'],
    axisLine: {show: false},
    axisTick: {show: false},
    axisLabel: {color: '#6B7280', fontSize: 10, margin: 12}
  },
  yAxis: {
    type: 'value',
    min: 0, max: 100, interval: 25,
    splitLine: {lineStyle: {color: 'rgba(255, 255, 255, 0.05)'}},
    axisLabel: {color: '#6B7280', fontSize: 10, formatter: '{value}°C'}
  },
  series: [
    {
      name: 'CPU',
      data: tempHistory.value.map(i => i.cpu),
      type: 'line',
      smooth: true,
      symbol: 'circle',
      symbolSize: 6,
      lineStyle: {color: '#3B82F6', width: 3},
      itemStyle: {color: '#3B82F6'}
    },
    {
      name: 'GPU',
      data: tempHistory.value.map(i => i.gpu),
      type: 'line',
      smooth: true,
      symbol: 'circle',
      symbolSize: 6,
      lineStyle: {color: '#10B981', width: 3},
      itemStyle: {color: '#10B981'}
    },
    {
      name: '主板',
      data: tempHistory.value.map(i => i.mb),
      type: 'line',
      smooth: true,
      symbol: 'circle',
      symbolSize: 6,
      lineStyle: {color: '#8A2BE2', width: 3},
      itemStyle: {color: '#8A2BE2'}
    }
  ]
}))
</script>

<template>
  <div class="p-6 h-full overflow-y-auto space-y-6 text-white no-scrollbar">

    <!-- Row 1: 顶部 Banner -->
    <div class="glass-card h-[280px] relative overflow-hidden flex flex-col justify-between p-10 group">
      <!-- 背景装饰层 -->
      <div
          class="absolute right-0 top-0 bottom-0 w-[53%] bg-gradient-to-l from-purple-900/20 to-transparent z-0 transition-transform duration-800">
        <!-- 右侧视频 -->
        <div
            class="w-[500px] h-[300px] rounded-2xl overflow-hidden shadow-2xl border border-white/5 bg-black/20 group/video -mt-2">
          <video
              src="@/assets/BackgroundVideo.mp4"
              autoplay
              loop
              muted
              playsinline
              class="w-full h-full object-cover opacity-90 group-hover/video:opacity-100 transition-opacity duration-500"
          ></video>
          <div class="absolute inset-0 bg-gradient-to-t from-black/40 to-transparent pointer-events-none"></div>
        </div>
        <div class="w-full h-full bg-[radial-gradient(circle_at_70%_50%,_rgba(138,43,226,0.15),transparent_60%)]"></div>
      </div>
      <div class="absolute inset-0 bg-gradient-to-r from-[#12141D] via-[#12141D]/90 to-transparent z-10"></div>

      <!-- 文字内容 & 视频 -->
      <div class="flex justify-between items-start relative z-20">
        <div class="space-y-2 max-w-xl">
          <p class="text-sm text-gray-400">欢迎回来</p>
          <h1 class="text-4xl font-semibold tracking-wide text-blue-400/90">JiaoLong <span class="text-purple-400/90">Control Console</span>
          </h1>
          <p class="text-sm text-gray-400 mt-2">为极致性能而生，掌控每一分潜能。</p>
        </div>
      </div>

      <!-- 底部指标 -->
      <div class="relative z-20 flex gap-12 mt-auto">
        <div class="flex items-center gap-3">
          <div class="w-10 h-10 rounded-full bg-blue-500/10 flex items-center justify-center">
            <img :src="imgCPU" class="w-6 h-6 object-contain"
                 style="filter: invert(48%) sepia(79%) saturate(2476%) hue-rotate(190deg) brightness(118%) contrast(119%);"/>
          </div>
          <div>
            <div class="text-xs text-gray-400">CPU</div>
            <div class="text-xl font-semibold">{{ cpuTemp }}°C</div>
          </div>
        </div>
        <div class="flex items-center gap-3">
          <div class="w-10 h-10 rounded-full bg-purple-500/10 flex items-center justify-center">
            <img :src="imgGPU" class="w-6 h-6 object-contain"
                 style="filter: invert(57%) sepia(52%) saturate(2859%) hue-rotate(120deg) brightness(100%) contrast(100%);"/>
          </div>
          <div>
            <div class="text-xs text-gray-400">GPU</div>
            <div class="text-xl font-semibold">{{ gpuTemp }}°C</div>
          </div>
        </div>
        <div class="flex items-center gap-3">
          <div class="w-10 h-10 rounded-full bg-white/5 flex items-center justify-center text-white">
            <icon-rocket class="text-xl"/>
          </div>
          <div>
            <div class="text-xs text-gray-400">模式</div>
            <div class="text-xl font-semibold">高性能</div>
          </div>
        </div>
      </div>
    </div>

    <!-- Row 2: 模式 & 监控 -->
    <div class="grid grid-cols-12 gap-3 h-[250px]">
      <PerformanceModeComp />
      <CoreMonitoringComp 
        :cpuUsage="cpuUsage" 
        :gpuUsage="gpuUsage" 
        :cpuTemp="cpuTemp" 
        :gpuTemp="gpuTemp" 
        :cpuFreq="cpuFreq" 
        :gpuFreq="gpuFreq" 
      />
    </div>

    <!-- Row 3: 系统概览 & 风扇 & 曲线 -->
    <div class="grid grid-cols-12 gap-6 h-[280px]">

      <!-- 系统概览 -->
      <div class="col-span-4 glass-card p-6 flex flex-col">
        <h2 class="text-[15px] font-medium text-white/90 mb-4">系统概览</h2>
        <div class="flex-1 flex flex-col justify-between">
          <div class="flex items-center gap-4">
            <div class="w-8 h-8 rounded-full bg-blue-900/30 flex items-center justify-center text-blue-500"><img
                :src="imgCPU" class="w-4 h-4" style="filter: invert(48%) sepia(79%) saturate(2476%)
      hue-rotate(190deg)"/></div>
            <div>
              <div class="text-xs text-white/90">CPU</div>
              <div class="text-xs text-gray-500 mt-0.5">{{ sysCpuName }}</div>
            </div>
          </div>
          <div class="flex items-center gap-4">
            <div class="w-8 h-8 rounded-full bg-green-900/30 flex items-center justify-center text-green-500"><img
                :src="imgGPU" class="w-4 h-4" style="filter: invert(57%) sepia(52%) saturate(2859%)
      hue-rotate(120deg)"/></div>
            <div>
              <div class="text-xs text-white/90">GPU</div>
              <div class="text-xs text-gray-500 mt-0.5">{{ sysGpuName }}</div>
            </div>
          </div>
          <div class="flex items-center gap-4">
            <div class="w-8 h-8 rounded-full bg-yellow-900/30 flex items-center justify-center text-yellow-500">
              <icon-storage/>
            </div>
            <div>
              <div class="text-xs text-white/90">内存</div>
              <div class="text-xs text-gray-500 mt-0.5">{{ sysMemory }}</div>
            </div>
          </div>
          <div class="flex items-center gap-4">
            <div class="w-8 h-8 rounded-full bg-red-900/30 flex items-center justify-center text-red-500">
              <icon-computer/>
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
          <div class="w-12 h-12 rounded-full bg-blue-600/20 flex items-center justify-center overflow-hidden">
            <img :src="imgFan" class="w-7 h-7 object-contain animate-spin"
                 style="animation-duration: 3s; filter: invert(48%) sepia(79%) saturate(2476%) hue-rotate(190deg) brightness(118%) contrast(119%);"/>
          </div>
          <div>
            <div class="flex items-baseline gap-1">
              <span class="text-3xl font-semibold">{{ fanSpeed }}</span>
              <span class="text-xs text-gray-400">RPM</span>
            </div>
            <div class="text-xs text-gray-500">风扇转速</div>
          </div>
        </div>

        <!-- 模拟声波图 -->
        <div class="h-16 flex items-center justify-center gap-1 mb-6">
          <div v-for="i in 40" :key="i"
               :style="{ height: (20 + Math.random() * 80) + '%' }"
               class="w-1 rounded-full bg-gradient-to-t from-purple-600 to-blue-500 opacity-60 transition-all duration-300"></div>
        </div>

        <div class="mt-auto">
          <div class="flex items-baseline gap-1">
            <span class="text-2xl font-semibold">{{ noiseLevel }}</span>
            <span class="text-xs text-gray-400">dBA</span>
          </div>
          <div class="text-xs text-gray-500">当前噪音</div>
        </div>
      </div>

      <!-- 温度曲线 -->
      <div class="col-span-4 glass-card p-6 flex flex-col">
        <h2 class="text-[15px] font-medium text-white/90 mb-2">温度曲线</h2>
        <div class="flex-1">
          <VChart :option="lineChartOption" autoresize/>
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
