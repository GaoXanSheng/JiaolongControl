<template>
  <a-card class="fan-curve-card" :bordered="false" @click="closeMenu">
    <div class="header-info">
      <!-- 左侧：基础统计信息 & CPU/GPU切换 -->
      <div class="info-section">
        <a-space>
          <a-radio-group v-model="activeTab" type="button" @change="onTabChange">
            <a-radio value="CPU">CPU曲线</a-radio>
            <a-radio value="GPU">GPU曲线</a-radio>
          </a-radio-group>
          <a-button type="primary" @click="handleRemoveFanClick">移除转速设置</a-button>
        </a-space>
      </div>
      <!-- 中间：服务控制开关 -->
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
              checked-text="ON"
              unchecked-text="OFF"
          />
        </a-space>
      </div>

      <!-- 右侧：操作提示 -->
      <div class="help-section sub-info">
        拖拽节点调整 / 右键管理节点
      </div>
    </div>

    <div class="svg-container" ref="containerRef">
      <svg
          v-if="isValidRender"
          :width="width"
          :height="height"
          @mousemove="onSvgMouseMove"
          @mouseup="onDragEnd"
          @mouseleave="onDragEnd"
      >
        <defs>
          <filter id="shadow" x="-50%" y="-50%" width="200%" height="200%">
            <feDropShadow dx="1" dy="1" stdDeviation="2" flood-opacity="0.3"/>
          </filter>
        </defs>

        <g class="grid">
          <line
              v-for="i in 11"
              :key="'v-'+i"
              :x1="safeMapX(currentTempRange[0]! + (i - 1) * (currentTempRange[1]! - currentTempRange[0]!) / 10)"
              :y1="padding.top"
              :x2="safeMapX(currentTempRange[0]! + (i - 1) * (currentTempRange[1]! - currentTempRange[0]!) / 10)"
              :y2="height - padding.bottom"
              stroke="#eee"
              stroke-dasharray="4"
          />
          <line
              v-for="i in 11"
              :key="'h-'+i"
              :x1="padding.left"
              :y1="safeMapY(speedRange[0]! + (i - 1) * (speedRange[1]! - speedRange[0]!) / 10)"
              :x2="width - padding.right"
              :y2="safeMapY(speedRange[0]! + (i - 1) * (speedRange[1]! - speedRange[0]!) / 10)"
              stroke="#eee"
              stroke-dasharray="4"
          />
        </g>

        <g class="labels" style="user-select: none; pointer-events: none;">
          <text
              v-for="i in 6"
              :key="'xl-'+i"
              :x="safeMapX(currentTempRange[0]! + (i - 1) * (currentTempRange[1]! - currentTempRange[0]!) / 5)"
              :y="height - 10"
              text-anchor="middle"
              fill="#999"
              font-size="10"
          >
            {{ Math.round(currentTempRange[0]! + (i - 1) * (currentTempRange[1]! - currentTempRange[0]!) / 5) }}°C
          </text>
          <text
              v-for="i in 6"
              :key="'yl-'+i"
              :x="padding.left - 10"
              :y="safeMapY(speedRange[0]! + (i - 1) * (speedRange[1]! - speedRange[0]!) / 5) + 4"
              text-anchor="end"
              fill="#999"
              font-size="10"
          >
            {{ Math.round(speedRange[0]! + (i - 1) * (speedRange[1]! - speedRange[0]!) / 5) }}
          </text>
        </g>

        <polyline
            :points="polylinePoints"
            fill="none"
            :stroke="activeTab === 'CPU' ? '#165DFF' : '#00B42A'"
            stroke-width="2"
            stroke-linejoin="round"
            stroke-linecap="round"
        />

        <g v-for="(p, index) in currentPoints" :key="index">
          <circle
              :cx="safeMapX(p.temp)"
              :cy="safeMapY(p.speed)"
              r="15"
              fill="transparent"
              cursor="pointer"
              @mousedown.stop="onDragStart(index, $event)"
              @contextmenu.prevent.stop="openContextMenu(index, $event)"
          />
          <circle
              :cx="safeMapX(p.temp)"
              :cy="safeMapY(p.speed)"
              r="6"
              :fill="activeTab === 'CPU' ? '#165DFF' : '#00B42A'"
              stroke="#fff"
              stroke-width="2"
              style="filter: url(#shadow); pointer-events: none;"
          />
          <text
              v-if="draggingIndex === index"
              :x="safeMapX(p.temp)"
              :y="safeMapY(p.speed) - 15"
              text-anchor="middle"
              fill="#333"
              font-size="12"
              font-weight="bold"
              style="pointer-events: none; text-shadow: 0 1px 2px white;"
          >
            {{ p.temp }}°C / {{ p.speed }}
          </text>
        </g>
      </svg>

      <div v-else class="loading-state">
        {{ width === 0 ? 'Initializing View...' : 'Invalid Data' }}
      </div>
    </div>

    <div
        v-if="menuVisible"
        class="context-menu"
        :style="menuStyle"
        @click.stop
        @contextmenu.prevent
    >
      <div class="menu-item" @click="onAddNode">在此处右侧添加节点</div>
      <div class="menu-item" :class="{ disabled: !canDelete }" @click="onRemoveNode">删除当前节点</div>
      <div class="menu-item" @click="openEditModal">编辑精确数值</div>
    </div>

    <a-modal v-model:visible="showEdit" title="编辑节点" :mask-closable="false" @ok="onEditConfirm">
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
  <FanSpeed></FanSpeed>
</template>

<script lang="ts" setup>
import {computed, onMounted, onUnmounted, reactive, ref, watch} from 'vue'
import {Message} from '@arco-design/web-vue'
import {AutoFanControl, Config, Fan} from '@/utils/bridge.ts'
import FanSpeed from "@/components/ProSettingComponent/FanCurve/FanSpeed.vue";

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
    console.log(config)
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
.fan-curve-card {
  height: 400px;
  position: relative;
  user-select: none;

  :deep(.arco-card-body) {
    height: 100%;
    padding: 0;
    display: flex;
    flex-direction: column;
  }
}

.header-info {
  padding: 10px 20px;
  border-bottom: 1px solid #f0f0f0;
  display: flex;
  justify-content: space-between;
  align-items: center;
  font-size: 12px;
  color: #333;
  height: 50px;

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
        box-shadow: 0 0 4px #00b42a;
        animation: pulse 2s infinite;
      }
    }
  }

  .help-section {
    flex: 1;
    text-align: right;
  }

  .sub-info {
    color: #999;
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
  background: #fff;

  .loading-state {
    width: 100%;
    height: 100%;
    display: flex;
    align-items: center;
    justify-content: center;
    color: #999;
    font-size: 14px;
  }
}

.context-menu {
  position: absolute;
  z-index: 999;
  background: #fff;
  border-radius: 4px;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
  border: 1px solid #e5e6eb;
  min-width: 140px;
  padding: 4px 0;

  .menu-item {
    padding: 8px 16px;
    cursor: pointer;
    font-size: 13px;
    color: #333;

    &:hover {
      background: #f2f3f5;
      color: #165DFF;
    }

    &.disabled {
      color: #c9cdd4;
      cursor: not-allowed;
    }
  }
}
</style>