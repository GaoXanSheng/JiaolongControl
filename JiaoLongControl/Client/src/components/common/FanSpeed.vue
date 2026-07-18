<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { Fan, CPU, NvidiaGpu } from '@/utils/bridge.ts'

const MAX_POINTS = 10
const INTERVAL = 2000
const PADDING_X = 40
const PADDING_Y = 20

// 定义色板（与系统全局风格统一）
const COLOR_CPU_FAN = '#8A2BE2'   // CPU转速：科技紫 (实线)
const COLOR_GPU_FAN = '#3B82F6'   // GPU转速：科技蓝 (虚线)
const COLOR_CPU_TEMP = '#E11D48'  // CPU温度：玫瑰红 (实线)
const COLOR_GPU_TEMP = '#10B981'  // GPU温度：薄荷绿 (虚线)

const MAX_FAN_RPM = 7800
const MAX_TEMP_C = 110

// 响应式宽高
const container = ref<HTMLElement | null>(null)
const width = ref(600)
const height = ref(180)

// 初始化预填充 10 个 0
const cpuFan = ref<number[]>(Array(MAX_POINTS).fill(0))
const gpuFan = ref<number[]>(Array(MAX_POINTS).fill(0))
const cpuTemp = ref<number[]>(Array(MAX_POINTS).fill(0))
const gpuTemp = ref<number[]>(Array(MAX_POINTS).fill(0))

const hasPolled = ref(false)
const hoverIndex = ref<number | null>(null)
let timer: number | null = null
let running = true
let resizeObserver: ResizeObserver | null = null

function push(arr: number[], v: number) {
  if (arr.length >= MAX_POINTS) arr.shift()
  arr.push(v)
}

const chartW = computed(() => Math.max(0, width.value - PADDING_X * 2))
const chartH = computed(() => Math.max(0, height.value - PADDING_Y * 2))

const xStep = computed(() => {
  if (MAX_POINTS <= 1) return 0
  return chartW.value / (MAX_POINTS - 1)
})

const xs = computed(() => {
  const currentLen = cpuFan.value.length
  return Array.from({ length: currentLen }, (_, i) => {
    return PADDING_X + i * xStep.value
  })
})

// 核心修改：根据 target ('cpu' | 'gpu') 分配不同的垂直轨道区间
function getY(v: number, max: number, target: 'cpu' | 'gpu') {
  const val = isNaN(v) ? 0 : v

  // 单个轨道占总高度的 45%
  const bandHeight = chartH.value * 0.45
  const relativeY = (1 - val / max) * bandHeight

  if (target === 'cpu') {
    // CPU 处于上半区间 (0% 到 45%)
    return PADDING_Y + relativeY
  } else {
    // GPU 处于下半区间 (55% 到 100%)，留出 10% 的中部空隙
    return PADDING_Y + (chartH.value * 0.55) + relativeY
  }
}

const makePath = (data: number[], max: number, target: 'cpu' | 'gpu') => {
  if (data.length === 0) return ''
  return data.map((val, i) => `${xs.value[i]},${getY(val, max, target)}`).join(' ')
}

const cpuFanPath = computed(() => makePath(cpuFan.value, MAX_FAN_RPM, 'cpu'))
const gpuFanPath = computed(() => makePath(gpuFan.value, MAX_FAN_RPM, 'gpu'))
const cpuTempPath = computed(() => makePath(cpuTemp.value, MAX_TEMP_C, 'cpu'))
const gpuTempPath = computed(() => makePath(gpuTemp.value, MAX_TEMP_C, 'gpu'))

function onMouseMove(e: MouseEvent) {
  if (!container.value || cpuFan.value.length === 0) return

  const rect = container.value.getBoundingClientRect()
  const mouseX = e.clientX - rect.left
  const chartX = mouseX - PADDING_X
  let index = Math.round(chartX / xStep.value)

  if (index < 0) index = 0
  if (index >= cpuFan.value.length) index = cpuFan.value.length - 1

  hoverIndex.value = index
}

async function poll() {
  if (!running) return

  let tempCpuFan = 0
  let tempGpuFan = 0
  let tempCpuTemp = 0
  let tempGpuTemp = 0

  try {
    const fan = await Fan.GetFanSpeed()
    if (fan?.Data) {
      tempCpuFan = fan.Data.CPUFanSpeed ?? 0
      tempGpuFan = fan.Data.GPUFanSpeed ?? 0
    }
  } catch (e) {
    console.error('读取风扇失败:', e)
  }

  try {
    const hw = await CPU.GetCPUThermometer()
    if (hw?.Data !== undefined) {
      tempCpuTemp = hw.Data ?? 0
    }
  } catch (e) {
    console.error('读取CPU温度失败:', e)
  }

  try {
    const gpu = await NvidiaGpu.GetGpuTemperature()
    if (gpu?.Data !== undefined) {
      tempGpuTemp = Number(gpu.Data) ?? 0
    }
  } catch (e) {
    console.error('读取GPU温度失败:', e)
  }

  push(cpuFan.value, tempCpuFan)
  push(gpuFan.value, tempGpuFan)
  push(cpuTemp.value, tempCpuTemp)
  push(gpuTemp.value, tempGpuTemp)

  hasPolled.value = true
  timer = window.setTimeout(poll, INTERVAL)
}

onMounted(() => {
  poll()
  if (container.value) {
    resizeObserver = new ResizeObserver((entries) => {
      for (const entry of entries) {
        width.value = entry.contentRect.width
        height.value = entry.contentRect.height
      }
    })
    resizeObserver.observe(container.value)
  }
})

onUnmounted(() => {
  running = false
  if (timer) clearTimeout(timer)
  if (resizeObserver) resizeObserver.disconnect()
})
</script>

<template>
  <div class="bg-[#121320]/60 backdrop-blur-md border border-white/[0.05] rounded-xl p-5 shadow-lg space-y-4">
    <!-- 图表顶栏标题 -->
    <div class="flex justify-between items-center select-none">
      <h2 class="text-[13px] font-semibold text-gray-300 flex items-center gap-1.5">
        <span class="w-1.5 h-1.5 rounded-full bg-purple-500 animate-pulse"></span>
        实时运行状态遥测
      </h2>
      <span class="text-[10px] text-gray-500 font-mono">{{ INTERVAL / 1000 }}s 采样间隔</span>
    </div>
    <!-- 图表区域 -->
    <div
        ref="container"
        class="chart-container"
        @mousemove="onMouseMove"
        @mouseleave="hoverIndex = null"
    >
      <svg :width="width" :height="height" v-if="width > 0">
        <defs>
          <!-- 荧光微弱发光滤镜 -->
          <filter id="neon-glow" x="-20%" y="-20%" width="140%" height="140%">
            <feGaussianBlur stdDeviation="1.5" result="blur" />
            <feComposite in="SourceGraphic" in2="blur" operator="over" />
          </filter>
        </defs>

        <!-- 1. 背景网格横线 (CPU 区间) -->
        <g stroke="rgba(255, 255, 255, 0.02)" stroke-width="1">
          <line :x1="PADDING_X" :x2="width - PADDING_X" :y1="PADDING_Y" :y2="PADDING_Y" />
          <line :x1="PADDING_X" :x2="width - PADDING_X" :y1="PADDING_Y + chartH * 0.22" :y2="PADDING_Y + chartH * 0.22" />
          <line :x1="PADDING_X" :x2="width - PADDING_X" :y1="PADDING_Y + chartH * 0.45" :y2="PADDING_Y + chartH * 0.45" />
        </g>

        <!-- 2. 中部 CPU / GPU 软隔离线 -->
        <line
            :x1="PADDING_X"
            :x2="width - PADDING_X"
            :y1="PADDING_Y + chartH * 0.5"
            :y2="PADDING_Y + chartH * 0.5"
            stroke="rgba(255, 255, 255, 0.08)"
            stroke-width="1"
            stroke-dasharray="4,4"
        />

        <!-- 3. 背景网格横线 (GPU 区间) -->
        <g stroke="rgba(255, 255, 255, 0.02)" stroke-width="1">
          <line :x1="PADDING_X" :x2="width - PADDING_X" :y1="PADDING_Y + chartH * 0.55" :y2="PADDING_Y + chartH * 0.55" />
          <line :x1="PADDING_X" :x2="width - PADDING_X" :y1="PADDING_Y + chartH * 0.77" :y2="PADDING_Y + chartH * 0.77" />
          <line :x1="PADDING_X" :x2="width - PADDING_X" :y1="PADDING_Y + chartH" :y2="PADDING_Y + chartH" />
        </g>

        <!-- 4. CPU/GPU 侧边标识文本 -->
        <text :x="PADDING_X - 12" :y="PADDING_Y + chartH * 0.25" font-size="9" fill="rgba(255,255,255,0.25)" text-anchor="end" font-weight="bold">CPU</text>
        <text :x="PADDING_X - 12" :y="PADDING_Y + chartH * 0.8" font-size="9" fill="rgba(255,255,255,0.25)" text-anchor="end" font-weight="bold">GPU</text>

        <!-- 5. 独立轨道折线路径 -->
        <polyline :points="cpuFanPath" fill="none" :stroke="COLOR_CPU_FAN" stroke-width="2" filter="url(#neon-glow)" />
        <polyline :points="gpuFanPath" fill="none" :stroke="COLOR_GPU_FAN" stroke-width="2" stroke-dasharray="6,4" filter="url(#neon-glow)" />
        <polyline :points="cpuTempPath" fill="none" :stroke="COLOR_CPU_TEMP" stroke-width="2" filter="url(#neon-glow)" />
        <polyline :points="gpuTempPath" fill="none" :stroke="COLOR_GPU_TEMP" stroke-width="2" stroke-dasharray="6,4" filter="url(#neon-glow)" />

        <!-- 6. 数据点拐点微圆点 -->
        <g v-for="(x, i) in xs" :key="'nodes-'+i">
          <circle v-if="cpuFan[i] !== undefined" :cx="x" :cy="getY(cpuFan[i]!, MAX_FAN_RPM, 'cpu')" r="2" :fill="COLOR_CPU_FAN"/>
          <circle v-if="gpuFan[i] !== undefined" :cx="x" :cy="getY(gpuFan[i]!, MAX_FAN_RPM, 'gpu')" r="2" :fill="COLOR_GPU_FAN"/>
          <circle v-if="cpuTemp[i] !== undefined" :cx="x" :cy="getY(cpuTemp[i]!, MAX_TEMP_C, 'cpu')" r="2" :fill="COLOR_CPU_TEMP"/>
          <circle v-if="gpuTemp[i] !== undefined" :cx="x" :cy="getY(gpuTemp[i]!, MAX_TEMP_C, 'gpu')" r="2" :fill="COLOR_GPU_TEMP"/>
        </g>

        <!-- 7. 交互式悬浮垂直标线及 Tooltip -->
        <template v-if="hoverIndex !== null && xs[hoverIndex] !== undefined">
          <!-- 悬浮轴标虚线 -->
          <line
              :x1="xs[hoverIndex]"
              :x2="xs[hoverIndex]"
              :y1="PADDING_Y"
              :y2="height - PADDING_Y"
              stroke="rgba(255, 255, 255, 0.12)"
              stroke-width="1"
              stroke-dasharray="3"
          />
          <!-- 悬浮高亮圆圈 -->
          <g>
            <circle v-if="cpuFan[hoverIndex] !== undefined" :cx="xs[hoverIndex]" :cy="getY(cpuFan[hoverIndex]!, MAX_FAN_RPM, 'cpu')" r="3.5" fill="#fff" :stroke="COLOR_CPU_FAN" stroke-width="2"/>
            <circle v-if="gpuFan[hoverIndex] !== undefined" :cx="xs[hoverIndex]" :cy="getY(gpuFan[hoverIndex]!, MAX_FAN_RPM, 'gpu')" r="3.5" fill="#fff" :stroke="COLOR_GPU_FAN" stroke-width="2"/>
            <circle v-if="cpuTemp[hoverIndex] !== undefined" :cx="xs[hoverIndex]" :cy="getY(cpuTemp[hoverIndex]!, MAX_TEMP_C, 'cpu')" r="3.5" fill="#fff" :stroke="COLOR_CPU_TEMP" stroke-width="2"/>
            <circle v-if="gpuTemp[hoverIndex] !== undefined" :cx="xs[hoverIndex]" :cy="getY(gpuTemp[hoverIndex]!, MAX_TEMP_C, 'gpu')" r="3.5" fill="#fff" :stroke="COLOR_GPU_TEMP" stroke-width="2"/>
          </g>

          <!-- 悬浮数据指示卡 -->
          <g :transform="`translate(${
              xs[hoverIndex]! + (xs[hoverIndex]! > width / 2 ? -200 : 20)
            }, ${ PADDING_Y - 5 })`" style="pointer-events: none">
            <rect
                width="180"
                height="124"
                rx="8"
                fill="rgba(18, 19, 32, 0.95)"
                stroke="rgba(255, 255, 255, 0.08)"
                stroke-width="1"
            />
            <text x="15" y="24" font-size="11" font-weight="bold" fill="#ffffff">时间切片: {{ hoverIndex + 1 }} / 10</text>
            <g transform="translate(15, 46)">
              <circle r="3.5" :fill="COLOR_CPU_FAN" cy="-3.5" />
              <text x="14" font-size="11" fill="rgba(255,255,255,0.7)">CPU风扇: <tspan font-weight="bold" fill="#ffffff" font-family="monospace">{{ cpuFan[hoverIndex] }}</tspan> RPM</text>
            </g>
            <g transform="translate(15, 66)">
              <circle r="3.5" :fill="COLOR_GPU_FAN" cy="-3.5" />
              <text x="14" font-size="11" fill="rgba(255,255,255,0.7)">GPU风扇: <tspan font-weight="bold" fill="#ffffff" font-family="monospace">{{ gpuFan[hoverIndex] }}</tspan> RPM</text>
            </g>
            <g transform="translate(15, 86)">
              <circle r="3.5" :fill="COLOR_CPU_TEMP" cy="-3.5" />
              <text x="14" font-size="11" fill="rgba(255,255,255,0.7)">CPU温度: <tspan font-weight="bold" fill="#ffffff" font-family="monospace">{{ cpuTemp[hoverIndex] }}</tspan> °C</text>
            </g>
            <g transform="translate(15, 106)">
              <circle r="3.5" :fill="COLOR_GPU_TEMP" cy="-3.5" />
              <text x="14" font-size="11" fill="rgba(255,255,255,0.7)">GPU温度: <tspan font-weight="bold" fill="#ffffff" font-family="monospace">{{ gpuTemp[hoverIndex] }}</tspan> °C</text>
            </g>
          </g>
        </template>
      </svg>
    </div>
  </div>
</template>

<style scoped>
.chart-container {
  width: 100%;
  position: relative;
  overflow: hidden;
}

svg {
  display: block;
}

text {
  user-select: none;
  font-family: system-ui, -apple-system, sans-serif;
}
</style>