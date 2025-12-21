<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { Fan, Hardware } from '@/utils/bridge.ts'

const MAX_POINTS = 20
const INTERVAL = 3000
const PADDING_X = 40
const PADDING_Y = 20

const MAX_FAN_RPM = 5800
const MAX_TEMP_C = 100

// 响应式宽高，初始值设为一个合理的大小
const container = ref<HTMLElement | null>(null)
const width = ref(600)
const height = ref(250)

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
function round2(v: number): number {
  return Math.round(v * 100) / 100
}

function getCpuTemp(info: any): number {
  const t = info?.Cpu?.Temperature ?? []
  const v =
      t.find((x: any) => x.Name.includes('Package'))?.Value ??
      t[0]?.Value ??
      0
  return round2(v)
}

function getGpuTemp(info: any): number {
  let v = 0
  if (info?.GpuNvidia?.Temperature?.length)
    v = info.GpuNvidia.Temperature[0].Value
  else if (info?.GpuAmd?.Temperature?.length)
    v = info.GpuAmd.Temperature[0].Value
  return round2(v)
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
    const hw = await Hardware.GetHardwareMonitorInfo()
    push(cpuFan.value, fan.CPUFanSpeed)
    push(gpuFan.value, fan.GPUFanSpeed)
    push(cpuTemp.value, getCpuTemp(hw))
    push(gpuTemp.value, getGpuTemp(hw))
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
  <div
      ref="container"
      class="chart-container"
      @mousemove="onMouseMove"
      @mouseleave="hoverIndex = null"
  >
    <svg :width="width" :height="height">
      <g stroke="#f2f3f5" stroke-width="1" stroke-dasharray="4 2">
        <line
            v-for="i in 5"
            :key="'h'+i"
            :x1="PADDING_X"
            :x2="width - PADDING_X"
            :y1="PADDING_Y + (i-1) * chartH / 4"
            :y2="PADDING_Y + (i-1) * chartH / 4"
        />
      </g>

      <polyline :points="cpuFanPath" fill="none" stroke="#1f77b4" stroke-width="2" />
      <polyline :points="gpuFanPath" fill="none" stroke="#ff7f0e" stroke-width="2" />
      <polyline :points="cpuTempPath" fill="none" stroke="#2ca02c" stroke-width="2" />
      <polyline :points="gpuTempPath" fill="none" stroke="#d62728" stroke-width="2" />

      <g v-for="(x, i) in xs" :key="'nodes-'+i">
        <circle :cx="x" :cy="getY(cpuFan[i], MAX_FAN_RPM)" r="3" fill="#1f77b4"/>
        <circle :cx="x" :cy="getY(gpuFan[i], MAX_FAN_RPM)" r="3" fill="#ff7f0e"/>
        <circle :cx="x" :cy="getY(cpuTemp[i], MAX_TEMP_C)" r="3" fill="#2ca02c"/>
        <circle :cx="x" :cy="getY(gpuTemp[i], MAX_TEMP_C)" r="3" fill="#d62728"/>
      </g>

      <line
          v-if="hoverIndex !== null"
          :x1="xs[hoverIndex]"
          :x2="xs[hoverIndex]"
          :y1="PADDING_Y"
          :y2="height - PADDING_Y"
          stroke="#999"
          stroke-width="1"
          stroke-dasharray="4"
      />

      <g v-if="hoverIndex !== null">
        <circle :cx="xs[hoverIndex]" :cy="getY(cpuFan[hoverIndex], MAX_FAN_RPM)" r="5" fill="#fff" stroke="#1f77b4" stroke-width="2"/>
        <circle :cx="xs[hoverIndex]" :cy="getY(gpuFan[hoverIndex], MAX_FAN_RPM)" r="5" fill="#fff" stroke="#ff7f0e" stroke-width="2"/>
        <circle :cx="xs[hoverIndex]" :cy="getY(cpuTemp[hoverIndex], MAX_TEMP_C)" r="5" fill="#fff" stroke="#2ca02c" stroke-width="2"/>
        <circle :cx="xs[hoverIndex]" :cy="getY(gpuTemp[hoverIndex], MAX_TEMP_C)" r="5" fill="#fff" stroke="#d62728" stroke-width="2"/>
      </g>

      <g v-if="hoverIndex !== null" style="pointer-events: none">
        <g :transform="`translate(${
            xs[hoverIndex] + (xs[hoverIndex] > width / 2 ? -210 : 20)
          }, ${ PADDING_Y + 10 })`">

          <rect
              width="180"
              height="120"
              rx="6"
              fill="rgba(255, 255, 255, 0.95)"
              stroke="#ccc"
              filter="drop-shadow(0 4px 6px rgba(0,0,0,0.15))"
          />
          <text x="15" y="25" font-size="12" font-weight="bold" fill="#333">Point Index: {{ hoverIndex }}</text>

          <g transform="translate(15, 45)">
            <circle r="4" fill="#1f77b4" cy="-4" />
            <text x="15" font-size="12" fill="#333">CPU Fan: {{ cpuFan[hoverIndex] }} RPM</text>
          </g>
          <g transform="translate(15, 65)">
            <circle r="4" fill="#ff7f0e" cy="-4" />
            <text x="15" font-size="12" fill="#333">GPU Fan: {{ gpuFan[hoverIndex] }} RPM</text>
          </g>
          <g transform="translate(15, 85)">
            <circle r="4" fill="#2ca02c" cy="-4" />
            <text x="15" font-size="12" fill="#333">CPU Temp: {{ cpuTemp[hoverIndex] }} °C</text>
          </g>
          <g transform="translate(15, 105)">
            <circle r="4" fill="#d62728" cy="-4" />
            <text x="15" font-size="12" fill="#333">GPU Temp: {{ gpuTemp[hoverIndex] }} °C</text>
          </g>
        </g>
      </g>
    </svg>
  </div>
</template>

<style scoped>
.chart-container {
  width: 100%;
  height: 250px;
  position: relative;
  overflow: hidden;
  cursor: crosshair;
}

svg {
  display: block;
}

text {
  user-select: none;
  font-family: sans-serif;
}
</style>