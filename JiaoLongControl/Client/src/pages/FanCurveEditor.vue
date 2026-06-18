<template>
  <div class="h-full overflow-y-auto text-white p-6 no-scrollbar">
    <div class="max-w-[1300px] mx-auto space-y-6">

      <!-- 页面标题 -->
      <div>
        <h1 class="text-2xl font-bold tracking-wide">风扇曲线编辑器</h1>
        <p class="text-[13px] text-gray-500 mt-1">可视化拖动调整不同核心温度下的风扇转速，支持 CPU/GPU 独立配置。</p>
      </div>

      <!-- 主编辑器卡片 -->
      <a-card class="fan-curve-card" :bordered="false" @click="closeMenu">

        <!-- 头部控制栏 -->
        <div class="header-info">
          <!-- 左侧：CPU/GPU 切换及移除设置 -->
          <div class="info-section">
            <a-space size="medium">
              <a-radio-group
                  v-model="activeTab"
                  type="button"
                  @change="onTabChange"
                  :class="['radio-group-dark', activeTab === 'GPU' ? 'radio-gpu' : '']"
              >
                <a-radio value="CPU">CPU 曲线</a-radio>
                <a-radio value="GPU">GPU 曲线</a-radio>
              </a-radio-group>
              <button
                  @click="handleRemoveFanClick"
                  class="text-xs font-semibold text-rose-400 border border-rose-500/20 bg-rose-500/10 hover:bg-rose-500 hover:text-white px-4 py-1.5 rounded-lg transition-all"
              >
                移除转速设置
              </button>
            </a-space>
          </div>

          <!-- 中间：后台自动风扇控制服务开关 -->
          <div class="control-section">
            <a-space size="medium">
              <a-tag :color="isServiceRunning ? 'green' : 'gray'" bordered class="status-tag">
                <template #icon>
                  <div :class="['status-dot', { active: isServiceRunning }]"></div>
                </template>
                {{ isServiceRunning ? '运行中' : '已停止' }}
              </a-tag>

              <a-switch
                  v-model="isServiceRunning"
                  :loading="serviceLoading"
                  :before-change="handleServiceToggle"
                  class="switch-purple"
              />
            </a-space>
          </div>

          <!-- 右侧：操作提示 -->
          <div class="help-section sub-info">
            拖拽节点调整 / 右键管理节点
          </div>
        </div>

        <!-- 曲线网格主画布 -->
        <div class="svg-container" ref="containerRef">
          <svg
              v-if="isValidRender"
              :width="width"
              :height="height"
              @mousemove="onSvgMouseMove"
              @mouseup="onDragEnd"
              @mouseleave="onDragEnd"
          >
            <!-- 图表发光及投影滤镜 -->
            <defs>
              <filter id="shadow" x="-50%" y="-50%" width="200%" height="200%">
                <feDropShadow dx="0" dy="0" stdDeviation="3" :flood-color="activeTab === 'CPU' ? '#8A2BE2' : '#10B981'" flood-opacity="0.8"/>
              </filter>
              <!-- CPU 渐变区 -->
              <linearGradient id="cpu-glow" x1="0" y1="0" x2="0" y2="1">
                <stop offset="0%" stop-color="#8A2BE2" stop-opacity="0.25" />
                <stop offset="100%" stop-color="#8A2BE2" stop-opacity="0.0" />
              </linearGradient>
              <!-- GPU 渐变区 -->
              <linearGradient id="gpu-glow" x1="0" y1="0" x2="0" y2="1">
                <stop offset="0%" stop-color="#10B981" stop-opacity="0.25" />
                <stop offset="100%" stop-color="#10B981" stop-opacity="0.0" />
              </linearGradient>
            </defs>

            <!-- 1. 背景虚线网格线 -->
            <g class="grid">
              <line
                  v-for="i in 11"
                  :key="'v-'+i"
                  :x1="safeMapX(currentTempRange[0]! + (i - 1) * (currentTempRange[1]! - currentTempRange[0]!) / 10)"
                  :y1="padding.top"
                  :x2="safeMapX(currentTempRange[0]! + (i - 1) * (currentTempRange[1]! - currentTempRange[0]!) / 10)"
                  :y2="height - padding.bottom"
                  stroke="rgba(255, 255, 255, 0.03)"
                  stroke-dasharray="3"
              />
              <line
                  v-for="i in 11"
                  :key="'h-'+i"
                  :x1="padding.left"
                  :y1="safeMapY(speedRange[0]! + (i - 1) * (speedRange[1]! - speedRange[0]!) / 10)"
                  :x2="width - padding.right"
                  :y2="safeMapY(speedRange[0]! + (i - 1) * (speedRange[1]! - speedRange[0]!) / 10)"
                  stroke="rgba(255, 255, 255, 0.03)"
                  stroke-dasharray="3"
              />
            </g>

            <!-- 2. 轴坐标文字轴标 -->
            <g class="labels" style="user-select: none; pointer-events: none;">
              <text
                  v-for="i in 6"
                  :key="'xl-'+i"
                  :x="safeMapX(currentTempRange[0]! + (i - 1) * (currentTempRange[1]! - currentTempRange[0]!) / 5)"
                  :y="height - 12"
                  text-anchor="middle"
                  fill="rgba(255, 255, 255, 0.3)"
                  font-size="9"
                  font-family="monospace"
              >
                {{ Math.round(currentTempRange[0]! + (i - 1) * (currentTempRange[1]! - currentTempRange[0]!) / 5) }}°C
              </text>
              <text
                  v-for="i in 6"
                  :key="'yl-'+i"
                  :x="padding.left - 12"
                  :y="safeMapY(speedRange[0]! + (i - 1) * (speedRange[1]! - speedRange[0]!) / 5) + 3"
                  text-anchor="end"
                  fill="rgba(255, 255, 255, 0.3)"
                  font-size="9"
                  font-family="monospace"
              >
                {{ Math.round(speedRange[0]! + (i - 1) * (speedRange[1]! - speedRange[0]!) / 5) }}
              </text>
            </g>

            <!-- 3. 曲线底部高光渐变面积填充 -->
            <polygon
                :points="polygonPoints"
                :fill="activeTab === 'CPU' ? 'url(#cpu-glow)' : 'url(#gpu-glow)'"
            />

            <!-- 4. 主骨架曲线 -->
            <polyline
                :points="polylinePoints"
                fill="none"
                :stroke="activeTab === 'CPU' ? '#8A2BE2' : '#10B981'"
                stroke-width="2.5"
                stroke-linejoin="round"
                stroke-linecap="round"
            />

            <!-- 5. 交互控制锚点 -->
            <g v-for="(p, index) in currentPoints" :key="index">
              <!-- 用于加大鼠标捕捉面积的隐形圆圈 -->
              <circle
                  :cx="safeMapX(p.temp)"
                  :cy="safeMapY(p.speed)"
                  r="14"
                  fill="transparent"
                  cursor="pointer"
                  @mousedown.stop="onDragStart(index, $event)"
                  @contextmenu.prevent.stop="openContextMenu(index, $event)"
              />
              <!-- 真实渲染的微发光圆点 -->
              <circle
                  :cx="safeMapX(p.temp)"
                  :cy="safeMapY(p.speed)"
                  r="5"
                  :fill="activeTab === 'CPU' ? '#8A2BE2' : '#10B981'"
                  stroke="#fff"
                  stroke-width="1.5"
                  style="filter: url(#shadow); pointer-events: none;"
              />
              <!-- 拖动时的实时高亮数值气泡 -->
              <text
                  v-if="draggingIndex === index"
                  :x="safeMapX(p.temp)"
                  :y="safeMapY(p.speed) - 15"
                  text-anchor="middle"
                  fill="#ffffff"
                  font-size="10"
                  font-weight="bold"
                  font-family="monospace"
                  style="pointer-events: none; text-shadow: 0 0 4px rgba(0,0,0,0.8);"
              >
                {{ p.temp }}°C / {{ p.speed }} RPM
              </text>
            </g>
          </svg>

          <div v-else class="loading-state">
            <a-spin :size="20" />
            <span class="ml-2 text-xs text-gray-500">{{ width === 0 ? '初始化编辑器视图...' : '数据失效' }}</span>
          </div>
        </div>

        <!-- 节点右键操作菜单 -->
        <div
            v-if="menuVisible"
            class="context-menu"
            :style="menuStyle"
            @click.stop
            @contextmenu.prevent
        >
          <div class="menu-item" @click="onAddNode">在此处右侧添加节点</div>
          <div class="menu-item border-t border-white/[0.03]" :class="{ disabled: !canDelete }" @click="onRemoveNode">删除当前节点</div>
          <div class="menu-item border-t border-white/[0.03]" @click="openEditModal">编辑精确数值</div>
        </div>

        <!-- 手动精确编辑弹窗 -->
        <a-modal v-model:visible="showEdit" title="编辑转速节点" :mask-closable="false" @ok="onEditConfirm">
          <a-space v-if="selectedIndex !== null" direction="vertical" size="large" style="width: 100%">
            <a-input-number v-model="editForm.temp" :min="getMinTemp(selectedIndex)" :max="getMaxTemp(selectedIndex)"
                            style="width: 100%">
              <template #prepend>温度 (°C)</template>
            </a-input-number>
            <a-input-number v-model="editForm.speed" :min="speedRange[0]" :max="speedRange[1]" style="width: 100%">
              <template #prepend>转速 (RPM)</template>
            </a-input-number>
          </a-space>
        </a-modal>
      </a-card>

      <!-- 实时风扇统计反馈小组件 -->
      <FanSpeed></FanSpeed>
    </div>
  </div>
</template>

<script lang="ts" setup>
import {computed, onMounted, onUnmounted, reactive, ref, watch} from 'vue'
import {Message} from '@arco-design/web-vue'
import {AutoFanControl, Config, Fan} from '@/utils/bridge.ts'
import FanSpeed from "@/components/common/FanSpeed.vue";

interface Point {
  temp: number
  speed: number
}
const config = (await Config.GetConfig()).Data
const activeTab = ref<'CPU' | 'GPU'>('CPU')
const cpuPoints = ref<Point[]>([
  {temp: 60, speed: 1500},
  {temp: 80, speed: 3000},
  {temp: 100, speed: 5800}
])

const gpuPoints = ref<Point[]>([
  {temp: 60, speed: 1500},
  {temp: 75, speed: 3000},
  {temp: 87, speed: 5800}
])
const currentPoints = computed(() => activeTab.value === 'CPU' ? cpuPoints.value : gpuPoints.value)
const currentTempRange = computed(() => activeTab.value === 'CPU' ? [60, 100] : [60, 87])

const speedRange = [1500, 6800]
const padding = {top: 40, right: 60, bottom: 40, left: 60}

const containerRef = ref<HTMLDivElement | null>(null)
const width = ref(0)
const height = ref(0)
let resizeObserver: ResizeObserver | null = null

const draggingIndex = ref<number | null>(null)
const menuVisible = ref(false)
const menuPos = reactive({x: 0, y: 0})
const selectedIndex = ref<number | null>(null)
const showEdit = ref(false)
const editForm = reactive({temp: 0, speed: 0})
const isServiceRunning = ref(false)
const serviceLoading = ref(false)

let autoSaveTimer: number | null = null

const canDelete = computed(() => selectedIndex.value !== null && currentPoints.value.length > 2)

const isValidRender = computed(() => {
  return width.value > 0 && height.value > 0 && currentPoints.value.every(p => !isNaN(p.temp) && !isNaN(p.speed))
})

function onTabChange() {
  closeMenu()
  draggingIndex.value = null
  selectedIndex.value = null
}

const checkServiceStatus = async () => {
  try {
    isServiceRunning.value = (await AutoFanControl.IsRunning()).Success
  } catch (e) {
    console.error('Failed to check fan control status:', e)
  }
}

const handleServiceToggle = async (newValue: any): Promise<boolean> => {
  serviceLoading.value = true
  try {
    if (newValue) {
      await AutoFanControl.Start()
      Message.success('自动风扇控制已启动')
    } else {
      await AutoFanControl.Stop()
      Message.info('自动风扇控制已停止')
    }
    isServiceRunning.value = (await AutoFanControl.IsRunning()).Success
    return true
  } catch (e) {
    Message.error('操作失败，请检查日志')
    isServiceRunning.value = (await AutoFanControl.IsRunning()).Success
    return false
  } finally {
    serviceLoading.value = false
  }
}
const autoSave = async () => {
  try {
    config.AdvancedFanControlSystemConfig = {
      CpuFan: cpuPoints.value,
      GpuFan: gpuPoints.value
    }
    await Config.SetConfig(config)
  } catch (e) {
    console.error('Save failed:', e)
  }
}
watch([cpuPoints, gpuPoints], () => {
  if (autoSaveTimer) {
    clearTimeout(autoSaveTimer)
  }
  autoSaveTimer = window.setTimeout(() => {
    autoSave()
  }, 500)
}, {deep: true})

function safeMapX(val: number): number {
  if (isNaN(val) || width.value <= 0) return 0
  const result = mapX(val)
  return isNaN(result) ? 0 : result
}

function safeMapY(val: number): number {
  if (isNaN(val) || height.value <= 0) return 0
  const result = mapY(val)
  return isNaN(result) ? 0 : result
}

function mapX(temp: number) {
  const innerWidth = width.value - padding.left - padding.right
  const range = currentTempRange.value
  const ratio = (temp - range[0]!) / (range[1]! - range[0]!)
  return padding.left + ratio * innerWidth
}

function mapY(speed: number) {
  const innerHeight = height.value - padding.top - padding.bottom
  const ratio = (speed - speedRange[0]!) / (speedRange[1]! - speedRange[0]!)
  return padding.top + (1 - ratio) * innerHeight
}

function unmapX(x: number) {
  const innerWidth = width.value - padding.left - padding.right
  const range = currentTempRange.value
  const ratio = (x - padding.left) / innerWidth
  return range[0]! + ratio * (range[1]! - range[0]!)
}

function unmapY(y: number) {
  const innerHeight = height.value - padding.top - padding.bottom
  const ratio = (y - padding.top) / innerHeight
  return speedRange[0]! + (1 - ratio) * (speedRange[1]! - speedRange[0]!)
}

const polylinePoints = computed(() => {
  return currentPoints.value.map(p => `${safeMapX(p.temp)},${safeMapY(p.speed)}`).join(' ')
})

// 计算面积渐变闭合多边形的坐标点
const polygonPoints = computed(() => {
  if (currentPoints.value.length === 0) return ''
  const pts = currentPoints.value.map(p => `${safeMapX(p.temp)},${safeMapY(p.speed)}`)
  // 投影右下角点与左下角点以闭合底部
  const lastPt = currentPoints.value[currentPoints.value.length - 1]
  const firstPt = currentPoints.value[0]
  pts.push(`${safeMapX(lastPt!.temp)},${safeMapY(speedRange[0]!)}`)
  pts.push(`${safeMapX(firstPt!.temp)},${safeMapY(speedRange[0]!)}`)
  return pts.join(' ')
})

function parseConfigPoints(rawData: any) {
  if (!rawData || !Array.isArray(rawData)) return null
  const cleanData = rawData.map((item: any) => {
    const t = Number(item.temp ?? item.Temp ?? item.Temperature ?? item.temperature ?? 0)
    const s = Number(item.speed ?? item.Speed ?? item.FanSpeed ?? item.rpm ?? 0)
    return {temp: t, speed: s}
  })
  const validData = cleanData.filter((p: Point) => !isNaN(p.temp) && !isNaN(p.speed) && p.temp > 0)
  return validData.length > 0 ? validData : null
}

onMounted(async () => {
  if (containerRef.value) {
    resizeObserver = new ResizeObserver((entries) => {
      const entry = entries[0]
      if (entry!.contentRect.width > 0 && entry!.contentRect.height > 0) {
        width.value = entry!.contentRect.width
        height.value = entry!.contentRect.height
      }
    })
    resizeObserver.observe(containerRef.value)
  }

  await checkServiceStatus()
  try {
    const advancedConfig = config.AdvancedFanControlSystemConfig
    const parsedCpu = parseConfigPoints(advancedConfig.CpuFan)
    if (parsedCpu) cpuPoints.value = parsedCpu
    const parsedGpu = parseConfigPoints(advancedConfig.GpuFan)
    if (parsedGpu) gpuPoints.value = parsedGpu
  } catch (e) {
    console.error(e)
  }
})

onUnmounted(() => {
  resizeObserver?.disconnect()
  if (autoSaveTimer) clearTimeout(autoSaveTimer)
})

function onDragStart(index: number, e: MouseEvent) {
  if (e.button !== 0) return
  draggingIndex.value = index
  menuVisible.value = false
}

function onSvgMouseMove(e: MouseEvent) {
  if (draggingIndex.value === null) return

  const rect = containerRef.value!.getBoundingClientRect()
  const mouseX = e.clientX - rect.left
  const mouseY = e.clientY - rect.top

  let newTemp = Math.round(unmapX(mouseX))
  let newSpeed = Math.round(unmapY(mouseY))

  newSpeed = Math.max(speedRange[0]!, Math.min(newSpeed, speedRange[1]!))

  const idx = draggingIndex.value
  const pointsRef = currentPoints.value
  const range = currentTempRange.value

  const minT = idx === 0 ? range[0] : pointsRef[idx - 1]!.temp + 1
  const maxT = idx === pointsRef.length - 1 ? range[1] : pointsRef[idx + 1]!.temp - 1
  newTemp = Math.max(minT!, Math.min(newTemp, maxT!))

  pointsRef[idx]!.temp = newTemp
  pointsRef[idx]!.speed = newSpeed
}

function onDragEnd() {
  draggingIndex.value = null
}

const menuStyle = computed(() => ({
  left: `${menuPos.x}px`,
  top: `${menuPos.y}px`
}))

function openContextMenu(index: number, e: MouseEvent) {
  selectedIndex.value = index
  menuVisible.value = true

  const rect = containerRef.value!.getBoundingClientRect()
  menuPos.x = e.clientX - rect.left + 10
  menuPos.y = e.clientY - rect.top
}

function closeMenu() {
  menuVisible.value = false
}

function getMinTemp(index: number) {
  if (index === 0) return currentTempRange.value[0]
  return currentPoints.value[index - 1]!.temp + 1
}

function getMaxTemp(index: number) {
  if (index === currentPoints.value.length - 1) return currentTempRange.value[1]
  return currentPoints.value[index + 1]!.temp - 1
}

function onAddNode() {
  if (selectedIndex.value === null) return
  const pointsRef = currentPoints.value
  const curr = pointsRef[selectedIndex.value]
  const next = pointsRef[selectedIndex.value + 1]

  if (curr == undefined) return;
  if (!next || next.temp <= curr.temp + 1) return

  pointsRef.splice(selectedIndex.value + 1, 0, {
    temp: Math.floor((curr.temp + next.temp) / 2),
    speed: Math.floor((curr.speed + next.speed) / 2)
  })
  closeMenu()
}

function onRemoveNode() {
  if (!canDelete.value || selectedIndex.value === null) return
  currentPoints.value.splice(selectedIndex.value, 1)
  closeMenu()
  selectedIndex.value = null
}

function openEditModal() {
  if (selectedIndex.value === null) return
  const p = currentPoints.value[selectedIndex.value]
  editForm.temp = p!.temp
  editForm.speed = p!.speed
  showEdit.value = true
  closeMenu()
}

function onEditConfirm() {
  if (selectedIndex.value === null) return
  currentPoints.value[selectedIndex.value]!.temp = editForm.temp
  currentPoints.value[selectedIndex.value]!.speed = editForm.speed
  showEdit.value = false
}

async function handleRemoveFanClick() {
  if (await AutoFanControl.IsRunning()) {
    await AutoFanControl.Stop()
  }
  Message.success((await Fan.RemoveFanSpeed()).Message)
  isServiceRunning.value = false
}
</script>

<style lang="scss" scoped>
/* 隐藏滚动条 */
.no-scrollbar::-webkit-scrollbar {
  display: none;
}
.no-scrollbar {
  -ms-overflow-style: none;
  scrollbar-width: none;
}

.fan-curve-card {
  height: 400px;
  position: relative;
  user-select: none;
  background: rgba(18, 19, 32, 0.6) !important;
  backdrop-filter: blur(12px);
  border: 1px solid rgba(255, 255, 255, 0.05) !important;
  border-radius: 12px;
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.3);

  :deep(.arco-card-body) {
    height: 100%;
    padding: 0;
    display: flex;
    flex-direction: column;
  }
}

.header-info {
  padding: 12px 20px;
  border-bottom: 1px solid rgba(255, 255, 255, 0.05);
  display: flex;
  justify-content: space-between;
  align-items: center;
  font-size: 12px;
  color: rgba(255, 255, 255, 0.85);
  height: 54px;

  .info-section {
    flex: 1.5;
    display: flex;
    align-items: center;
  }

  .control-section {
    flex: 1;
    display: flex;
    justify-content: center;
    align-items: center;

    .status-tag {
      display: flex;
      align-items: center;
      background-color: rgba(255, 255, 255, 0.02) !important;
      border: 1px solid rgba(255, 255, 255, 0.05) !important;
      color: rgba(255, 255, 255, 0.6);
      height: 24px;
      padding: 0 8px;
    }

    .status-dot {
      width: 6px;
      height: 6px;
      border-radius: 50%;
      background-color: #86909c;
      margin-right: 6px;
      transition: all 0.3s;

      &.active {
        background-color: #00b42a;
        box-shadow: 0 0 6px #00b42a;
        animation: pulse 2s infinite;
      }
    }
  }

  .help-section {
    flex: 1;
    text-align: right;
  }

  .sub-info {
    color: rgba(255, 255, 255, 0.4);
  }
}

@keyframes pulse {
  0% {
    box-shadow: 0 0 0 0 rgba(0, 180, 42, 0.4);
  }
  70% {
    box-shadow: 0 0 0 4px rgba(0, 180, 42, 0);
  }
  100% {
    box-shadow: 0 0 0 0 rgba(0, 180, 42, 0);
  }
}

.svg-container {
  flex: 1;
  width: 100%;
  position: relative;
  overflow: hidden;
  background: transparent;

  .loading-state {
    width: 100%;
    height: 100%;
    display: flex;
    align-items: center;
    justify-content: center;
    color: rgba(255, 255, 255, 0.4);
    font-size: 13px;
  }
}

.context-menu {
  position: absolute;
  z-index: 999;
  background: rgba(26, 27, 43, 0.95);
  backdrop-filter: blur(12px);
  border-radius: 6px;
  box-shadow: 0 8px 24px rgba(0, 0, 0, 0.5);
  border: 1px solid rgba(255, 255, 255, 0.08);
  min-width: 140px;
  padding: 4px 0;

  .menu-item {
    padding: 8px 16px;
    cursor: pointer;
    font-size: 12px;
    color: rgba(255, 255, 255, 0.8);
    transition: all 0.2s;

    &:hover {
      background: rgba(138, 43, 226, 0.15);
      color: #a855f7;
    }

    &.disabled {
      color: rgba(255, 255, 255, 0.25);
      cursor: not-allowed;
      background: transparent !important;
    }
  }
}

/* 覆盖 Arco 选项卡为暗色玻璃样式 */
:deep(.radio-group-dark.arco-radio-group-button) {
  background-color: rgba(255, 255, 255, 0.03) !important;
  border: 1px solid rgba(255, 255, 255, 0.05) !important;
  border-radius: 8px !important;
  padding: 2px !important;

  .arco-radio-button {
    background-color: transparent !important;
    border: none !important;
    color: rgba(255, 255, 255, 0.5) !important;
    border-radius: 6px !important;
    font-weight: 500 !important;
    font-size: 11px !important;
    transition: all 0.3s !important;
    padding: 0 10px !important;
    height: 24px !important;
    line-height: 24px !important;

    &:not(.arco-radio-button-checked):hover {
      background-color: rgba(255, 255, 255, 0.05) !important;
      color: rgba(255, 255, 255, 0.8) !important;
    }

    &.arco-radio-button-checked {
      background-color: #8A2BE2 !important;
      color: #ffffff !important;
      box-shadow: 0 0 10px rgba(138, 43, 226, 0.3) !important;
    }
  }
}

:deep(.radio-gpu.arco-radio-group-button) {
  .arco-radio-button.arco-radio-button-checked {
    background-color: #10B981 !important;
    box-shadow: 0 0 10px rgba(16, 185, 129, 0.3) !important;
  }
}

/* 覆盖 Switch 默认背景色 */
:deep(.switch-purple.arco-switch-checked) {
  background-color: #8A2BE2 !important;
}

/* 覆盖 Arco Modal 的暗色磨砂样式 */
:deep(.arco-modal) {
  background-color: #121320 !important;
  border: 1px solid rgba(255, 255, 255, 0.08) !important;
  border-radius: 12px !important;
  box-shadow: 0 12px 36px rgba(0, 0, 0, 0.6) !important;

  .arco-modal-header {
    border-bottom: 1px solid rgba(255, 255, 255, 0.05) !important;
    .arco-modal-title {
      color: #ffffff !important;
      font-size: 13px !important;
    }
  }

  .arco-input-number {
    background-color: #17192a !important;
    border: 1px solid rgba(255, 255, 255, 0.05) !important;
    color: #ffffff !important;
    border-radius: 8px !important;
    overflow: hidden;

    .arco-input-number-prepend {
      background-color: #121320 !important;
      border-right: 1px solid rgba(255, 255, 255, 0.05) !important;
      color: rgba(255, 255, 255, 0.5) !important;
      font-size: 11px !important;
    }
  }

  .arco-modal-footer {
    border-t: 1px solid rgba(255, 255, 255, 0.05) !important;
  }
}
</style>