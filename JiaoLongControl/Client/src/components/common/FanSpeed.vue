<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue'
import {Fan, CPU, NvidiaGpu} from '@/utils/bridge.ts'
const MAX_POINTS = 10
const INTERVAL = 2000
const PADDING_X = 40
const PADDING_Y = 20

// 定义色板（与系统全局风格统一）
const COLOR_CPU_FAN = '#8A2BE2'   // CPU转速：科技紫
const COLOR_GPU_FAN = '#3B82F6'   // GPU转速：科技蓝
const COLOR_CPU_TEMP = '#E11D48'  // CPU温度：玫瑰红
const COLOR_GPU_TEMP = '#10B981'  // GPU温度：薄荷绿

const MAX_FAN_RPM = 7800
const MAX_TEMP_C = 110

// 响应式宽高
const container = ref<HTMLElement | null>(null)
const width = ref(600)
const height = ref(180) // 略微调高画布高度以获取更舒展的波形展示

const cpuFan = ref<number[]>([])
const gpuFan = ref<number[]>([])
const cpuTemp = ref<number[]>([])
const gpuTemp = ref<number[]>([])
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

function getY(v: number, max: number) {
  const val = isNaN(v) ? 0 : v
  return PADDING_Y + (1 - val / max) * chartH.value
}

const makePath = (data: number[], max: number) => {
  if (data.length === 0) return ''
  return data.map((val, i) => `${xs.value[i]},${getY(val, max)}`).join(' ')
}

const cpuFanPath = computed(() => makePath(cpuFan.value, MAX_FAN_RPM))
const gpuFanPath = computed(() => makePath(gpuFan.value, MAX_FAN_RPM))
const cpuTempPath = computed(() => makePath(cpuTemp.value, MAX_TEMP_C))
const gpuTempPath = computed(() => makePath(gpuTemp.value, MAX_TEMP_C))

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
  try {
    const fan = await Fan.GetFanSpeed()
    const hw = await CPU.GetCPUThermometer()
    const gpu = await NvidiaGpu.GetGpuTemperature()
    push(cpuFan.value, fan.Data.CPUFanSpeed)
    push(gpuFan.value, fan.Data.GPUFanSpeed)
    push(cpuTemp.value, hw.Data)
    push(gpuTemp.value, Number(gpu.Data))
  } catch (e) {
    console.error(e)
  }
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

    <div
        ref="container"
        class="chart-container"
        @mousemove="onMouseMove"
        @mouseleave="hoverIndex = null"
    >
      <svg :width="width" :height="height" v-if="width > 0">
        <!-- SVG 视觉滤镜定义 -->
        <defs>
          <!-- 荧光微弱发光滤镜 -->
          <filter id="neon-glow" x="-20%" y="-20%" width="140%" height="140%">
            <feGaussianBlur stdDeviation="1.5" result="blur" />
            <feComposite in="SourceGraphic" in2="blur" operator="over" />
          </filter>
        </defs>

        <!-- 1. 背景网格横线 -->
        <g stroke="rgba(255, 255, 255, 0.03)" stroke-width="1" stroke-dasharray="3">
          <line
              v-for="i in 5"
              :key="'h'+i"
              :x1="PADDING_X"
              :x2="width - PADDING_X"
              :y1="PADDING_Y + (i-1) * chartH / 4"
              :y2="PADDING_Y + (i-1) * chartH / 4"
          />
        </g>

        <!-- 2. 实数折线路径（配有发光滤镜） -->
        <polyline :points="cpuFanPath" fill="none" :stroke="COLOR_CPU_FAN" stroke-width="2" filter="url(#neon-glow)" />
        <polyline :points="gpuFanPath" fill="none" :stroke="COLOR_GPU_FAN" stroke-width="2" filter="url(#neon-glow)" />
        <polyline :points="cpuTempPath" fill="none" :stroke="COLOR_CPU_TEMP" stroke-width="2" filter="url(#neon-glow)" />
        <polyline :points="gpuTempPath" fill="none" :stroke="COLOR_GPU_TEMP" stroke-width="2" filter="url(#neon-glow)" />

        <!-- 3. 数据拐点微圆点 -->
        <g v-for="(x, i) in xs" :key="'nodes-'+i">
          <circle :cx="x" :cy="getY(cpuFan[i]!, MAX_FAN_RPM)" r="2.5" :fill="COLOR_CPU_FAN"/>
          <circle :cx="x" :cy="getY(gpuFan[i]!, MAX_FAN_RPM)" r="2.5" :fill="COLOR_GPU_FAN"/>
          <circle :cx="x" :cy="getY(cpuTemp[i]!, MAX_TEMP_C)" r="2.5" :fill="COLOR_CPU_TEMP"/>
          <circle :cx="x" :cy="getY(gpuTemp[i]!, MAX_TEMP_C)" r="2.5" :fill="COLOR_GPU_TEMP"/>
        </g>

        <!-- 4. 交互式悬浮垂直标线及 Tooltip 卡片 -->
        <template v-if="hoverIndex !== null && xs[hoverIndex] !== undefined">
          <!-- 悬浮轴标虚线 -->
          <line
              :x1="xs[hoverIndex]"
              :x2="xs[hoverIndex]"
              :y1="PADDING_Y"
              :y2="height - PADDING_Y"
              stroke="rgba(255, 255, 255, 0.15)"
              stroke-width="1"
              stroke-dasharray="3"
          />
          <!-- 悬浮高亮圆圈 -->
          <g>
            <circle :cx="xs[hoverIndex]" :cy="getY(cpuFan[hoverIndex]!, MAX_FAN_RPM)" r="4" fill="#fff" :stroke="COLOR_CPU_FAN" stroke-width="2"/>
            <circle :cx="xs[hoverIndex]" :cy="getY(gpuFan[hoverIndex]!, MAX_FAN_RPM)" r="4" fill="#fff" :stroke="COLOR_GPU_FAN" stroke-width="2"/>
            <circle :cx="xs[hoverIndex]" :cy="getY(cpuTemp[hoverIndex]!, MAX_TEMP_C)" r="4" fill="#fff" :stroke="COLOR_CPU_TEMP" stroke-width="2"/>
            <circle :cx="xs[hoverIndex]" :cy="getY(gpuTemp[hoverIndex]!, MAX_TEMP_C)" r="4" fill="#fff" :stroke="COLOR_GPU_TEMP" stroke-width="2"/>
          </g>

          <!-- 悬浮数据指示卡（高透暗色玻璃卡） -->
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