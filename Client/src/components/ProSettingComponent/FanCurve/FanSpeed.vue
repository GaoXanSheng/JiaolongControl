<script setup lang="ts">
import { onMounted, onUnmounted, ref } from 'vue'
import { Fan, Hardware } from '@/utils/bridge.ts'

const MAX_POINTS = 10
const INTERVAL = 3000

const canvasRef = ref<HTMLCanvasElement | null>(null)

let timer: number | null = null
let running = true
let hoverIndex: number | null = null

const cpuFan: number[] = []
const gpuFan: number[] = []
const cpuTemp: number[] = []
const gpuTemp: number[] = []

const MAX_Y = 5800
const TEMP_SCALE = 58
const PADDING = 40

/* ================= 工具 ================= */

function push(arr: number[], v: number) {
  if (arr.length >= MAX_POINTS) arr.shift()
  arr.push(v)
}

function getCpuTemp(info: any): number {
  const t = info?.Cpu?.Temperature ?? []
  return t.find((x: any) => x.Name.includes('Package'))?.Value ?? t[0]?.Value ?? 0
}

function getGpuTemp(info: any): number {
  if (info?.GpuNvidia?.Temperature?.length)
    return info.GpuNvidia.Temperature[0].Value
  if (info?.GpuAmd?.Temperature?.length)
    return info.GpuAmd.Temperature[0].Value
  return 0
}

/* ================= 绘制 ================= */

function drawGrid(ctx: CanvasRenderingContext2D, w: number, h: number) {
  const chartW = w - PADDING * 2
  const chartH = h - PADDING * 2

  ctx.strokeStyle = '#f2f3f5'
  ctx.lineWidth = 1

  for (let i = 0; i <= 4; i++) {
    const y = PADDING + (i / 4) * chartH
    ctx.beginPath()
    ctx.moveTo(PADDING, y)
    ctx.lineTo(w - PADDING, y)
    ctx.stroke()

    ctx.fillStyle = '#999'
    ctx.font = '11px sans-serif'
    ctx.fillText(
        Math.round(MAX_Y * (1 - i / 4)).toString(),
        6,
        y + 4
    )
  }

  for (let i = 0; i < MAX_POINTS; i++) {
    const x = PADDING + (i / (MAX_POINTS - 1)) * chartW
    ctx.beginPath()
    ctx.moveTo(x, PADDING)
    ctx.lineTo(x, h - PADDING)
    ctx.stroke()
  }
}

function drawLine(
    ctx: CanvasRenderingContext2D,
    data: number[],
    color: string
) {
  const w = ctx.canvas.width
  const h = ctx.canvas.height
  const chartW = w - PADDING * 2
  const chartH = h - PADDING * 2

  ctx.strokeStyle = color
  ctx.lineWidth = 2
  ctx.beginPath()

  data.forEach((v, i) => {
    const x = PADDING + (i / (MAX_POINTS - 1)) * chartW
    const y = PADDING + (1 - v / MAX_Y) * chartH
    i === 0 ? ctx.moveTo(x, y) : ctx.lineTo(x, y)
  })

  ctx.stroke()

  data.forEach((v, i) => {
    const x = PADDING + (i / (MAX_POINTS - 1)) * chartW
    const y = PADDING + (1 - v / MAX_Y) * chartH
    ctx.beginPath()
    ctx.fillStyle = color
    ctx.arc(x, y, i === hoverIndex ? 5 : 3, 0, Math.PI * 2)
    ctx.fill()
  })
}

function drawTooltip(
    ctx: CanvasRenderingContext2D,
    x: number,
    y: number,
    i: number
) {
  const boxW = 180
  const boxH = 112
  let tx = x + 12
  let ty = y - boxH / 2

  if (tx + boxW > ctx.canvas.width) tx = x - boxW - 12
  if (ty < 10) ty = 10

  ctx.save()

  ctx.shadowColor = 'rgba(0,0,0,0.12)'
  ctx.shadowBlur = 12

  ctx.fillStyle = '#ffffff'
  ctx.strokeStyle = '#e5e6eb'
  ctx.lineWidth = 1
  ctx.beginPath()
  ctx.roundRect(tx, ty, boxW, boxH, 8)
  ctx.fill()
  ctx.stroke()

  ctx.shadowBlur = 0

  ctx.font = '13px -apple-system, BlinkMacSystemFont, Segoe UI, Roboto'
  ctx.textBaseline = 'middle'

  const rows = [
    ['CPU Fan', `${cpuFan[i]} RPM`, '#1f77b4'],
    ['GPU Fan', `${gpuFan[i]} RPM`, '#ff7f0e'],
    ['CPU Temp', `${cpuTemp[i]} °C`, '#2ca02c'],
    ['GPU Temp', `${gpuTemp[i]} °C`, '#d62728']
  ]

  rows.forEach((r, idx) => {
    const ry = ty + 22 + idx * 22

    ctx.fillStyle = r[2]
    ctx.beginPath()
    ctx.arc(tx + 12, ry, 4, 0, Math.PI * 2)
    ctx.fill()

    ctx.fillStyle = '#333'
    ctx.fillText(r[0], tx + 24, ry)
    ctx.fillText(r[1], tx + 110, ry)
  })

  ctx.restore()
}

function draw() {
  const canvas = canvasRef.value
  if (!canvas) return
  const ctx = canvas.getContext('2d')!
  const w = canvas.width
  const h = canvas.height

  ctx.clearRect(0, 0, w, h)
  ctx.fillStyle = '#fff'
  ctx.fillRect(0, 0, w, h)

  drawGrid(ctx, w, h)

  drawLine(ctx, cpuFan, '#1f77b4')
  drawLine(ctx, gpuFan, '#ff7f0e')
  drawLine(ctx, cpuTemp.map(v => v * TEMP_SCALE), '#2ca02c')
  drawLine(ctx, gpuTemp.map(v => v * TEMP_SCALE), '#d62728')

  if (hoverIndex !== null && cpuFan[hoverIndex] !== undefined) {
    const chartW = w - PADDING * 2
    const chartH = h - PADDING * 2
    const x = PADDING + (hoverIndex / (MAX_POINTS - 1)) * chartW
    const y =
        PADDING +
        (1 - cpuFan[hoverIndex] / MAX_Y) * chartH

    drawTooltip(ctx, x, y, hoverIndex)
  }
}

/* ================= 鼠标 ================= */

function onMouseMove(e: MouseEvent) {
  const canvas = canvasRef.value
  if (!canvas) return

  const rect = canvas.getBoundingClientRect()
  const x = e.clientX - rect.left

  const chartW = canvas.width - PADDING * 2
  let idx: number | null = null
  let min = Infinity

  for (let i = 0; i < cpuFan.length; i++) {
    const px = PADDING + (i / (MAX_POINTS - 1)) * chartW
    const d = Math.abs(px - x)
    if (d < min && d < 12) {
      min = d
      idx = i
    }
  }

  hoverIndex = idx
  draw()
}

function onMouseLeave() {
  hoverIndex = null
  draw()
}

/* ================= 轮询 ================= */

async function poll() {
  if (!running) return

  try {
    const fan = await Fan.GetFanSpeed()
    const hw = await Hardware.GetHardwareMonitorInfo()

    push(cpuFan, fan.CPUFanSpeed)
    push(gpuFan, fan.GPUFanSpeed)
    push(cpuTemp, getCpuTemp(hw))
    push(gpuTemp, getGpuTemp(hw))

    draw()
  } catch (e) {
    console.error(e)
  }

  timer = window.setTimeout(poll, INTERVAL)
}

onMounted(() => {
  const canvas = canvasRef.value!
  canvas.width = canvas.clientWidth
  canvas.height = canvas.clientHeight

  canvas.addEventListener('mousemove', onMouseMove)
  canvas.addEventListener('mouseleave', onMouseLeave)

  poll()
})

onUnmounted(() => {
  running = false
  if (timer) clearTimeout(timer)
})
</script>

<template>
  <div style="width: 100%; height: 250px">
    <canvas ref="canvasRef" style="width: 100%; height: 100%" />
  </div>
</template>

<style scoped>
canvas {
  display: block;
  cursor: crosshair;
}
</style>
