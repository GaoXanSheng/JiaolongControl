import { computed, onMounted, onUnmounted, reactive, ref, watch } from 'vue'
import { Message } from '@arco-design/web-vue'
import { AutoFanControl, Fan } from '@/utils/bridge'
import { useConfigStore } from '@/stores/config'

export interface FanCurvePoint {
  temp: number
  speed: number
}

/**
 * 风扇曲线编辑器逻辑: 节点数据/坐标映射/拖拽与右键菜单/编辑弹窗/自动保存/服务开关.
 * 模板所需的全部状态与动作由此统一暴露, 组件只负责渲染.
 */
export function useFanCurveEditor() {
  const configStore = useConfigStore()

  const activeTab = ref<'CPU' | 'GPU'>('CPU')
  const cpuPoints = ref<FanCurvePoint[]>([
    { temp: 60, speed: 1500 },
    { temp: 80, speed: 3000 },
    { temp: 100, speed: 5800 },
  ])

  const gpuPoints = ref<FanCurvePoint[]>([
    { temp: 60, speed: 1500 },
    { temp: 75, speed: 3000 },
    { temp: 87, speed: 5800 },
  ])
  const currentPoints = computed(() =>
    activeTab.value === 'CPU' ? cpuPoints.value : gpuPoints.value,
  )
  const currentTempRange = computed(() => (activeTab.value === 'CPU' ? [60, 100] : [60, 87]))

  const speedRange = [1500, 6800]
  const padding = { top: 40, right: 60, bottom: 40, left: 60 }

  const containerRef = ref<HTMLDivElement | null>(null)
  const width = ref(0)
  const height = ref(0)
  let resizeObserver: ResizeObserver | null = null

  const draggingIndex = ref<number | null>(null)
  const menuVisible = ref(false)
  const menuPos = reactive({ x: 0, y: 0 })
  const selectedIndex = ref<number | null>(null)
  const showEdit = ref(false)
  const editForm = reactive({ temp: 0, speed: 0 })
  const isServiceRunning = ref(false)
  const serviceLoading = ref(false)

  let autoSaveTimer: number | null = null

  const canDelete = computed(() => selectedIndex.value !== null && currentPoints.value.length > 2)

  const isValidRender = computed(() => {
    return (
      width.value > 0 &&
      height.value > 0 &&
      currentPoints.value.every((p) => !isNaN(p.temp) && !isNaN(p.speed))
    )
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

  const handleServiceToggle = async (
    newValue: string | number | boolean,
  ): Promise<boolean> => {
    serviceLoading.value = true
    try {
      if (newValue) {
        await AutoFanControl.Start()
        Message.success('自动风扇控制已启用')
      } else {
        await AutoFanControl.Stop()
        Message.info('自动风扇控制已停止')
      }
      isServiceRunning.value = (await AutoFanControl.IsRunning()).Success
      return true
    } catch {
      Message.error('操作失败，请检查日志')
      isServiceRunning.value = (await AutoFanControl.IsRunning()).Success
      return false
    } finally {
      serviceLoading.value = false
    }
  }
  const autoSave = async () => {
    try {
      if (configStore.config) {
        configStore.config.Fan.CpuFanCurve = cpuPoints.value
        configStore.config.Fan.GpuFanCurve = gpuPoints.value
        configStore.debouncedSave()
      }
    } catch (e) {
      console.error('Save failed:', e)
    }
  }
  watch(
    [cpuPoints, gpuPoints],
    () => {
      if (autoSaveTimer) {
        clearTimeout(autoSaveTimer)
      }
      autoSaveTimer = window.setTimeout(() => {
        autoSave()
      }, 500)
    },
    { deep: true },
  )

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
    return currentPoints.value
      .map((p) => `${safeMapX(p.temp)},${safeMapY(p.speed)}`)
      .join(' ')
  })

  // 计算面积渐变闭合多边形的坐标点
  const polygonPoints = computed(() => {
    if (currentPoints.value.length === 0) return ''
    const pts = currentPoints.value.map((p) => `${safeMapX(p.temp)},${safeMapY(p.speed)}`)
    // 投影右下角点与左下角点以闭合底部
    const lastPt = currentPoints.value[currentPoints.value.length - 1]
    const firstPt = currentPoints.value[0]
    pts.push(`${safeMapX(lastPt!.temp)},${safeMapY(speedRange[0]!)}`)
    pts.push(`${safeMapX(firstPt!.temp)},${safeMapY(speedRange[0]!)}`)
    return pts.join(' ')
  })

  function parseConfigPoints(rawData: unknown): FanCurvePoint[] | null {
    if (!rawData || !Array.isArray(rawData)) return null
    const cleanData = rawData.map((item) => {
      const rec = (item ?? {}) as Record<string, unknown>
      const t = Number(rec.temp ?? rec.Temp ?? rec.Temperature ?? rec.temperature ?? 0)
      const s = Number(rec.speed ?? rec.Speed ?? rec.FanSpeed ?? rec.rpm ?? 0)
      return { temp: t, speed: s }
    })
    const validData = cleanData.filter(
      (p: FanCurvePoint) => !isNaN(p.temp) && !isNaN(p.speed) && p.temp > 0,
    )
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
      if (!configStore.config) {
        await configStore.fetchConfig()
      }
      const advancedConfig = configStore.config?.Fan
      const parsedCpu = parseConfigPoints(advancedConfig?.CpuFanCurve)
      if (parsedCpu) cpuPoints.value = parsedCpu
      const parsedGpu = parseConfigPoints(advancedConfig?.GpuFanCurve)
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
    top: `${menuPos.y}px`,
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

    if (curr == undefined) return
    if (!next || next.temp <= curr.temp + 1) return

    pointsRef.splice(selectedIndex.value + 1, 0, {
      temp: Math.floor((curr.temp + next.temp) / 2),
      speed: Math.floor((curr.speed + next.speed) / 2),
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

  return {
    activeTab,
    cpuPoints,
    gpuPoints,
    currentPoints,
    currentTempRange,
    speedRange,
    padding,
    containerRef,
    width,
    height,
    draggingIndex,
    menuVisible,
    menuPos,
    selectedIndex,
    showEdit,
    editForm,
    isServiceRunning,
    serviceLoading,
    canDelete,
    isValidRender,
    onTabChange,
    checkServiceStatus,
    handleServiceToggle,
    handleRemoveFanClick,
    safeMapX,
    safeMapY,
    mapX,
    mapY,
    unmapX,
    unmapY,
    polylinePoints,
    polygonPoints,
    onDragStart,
    onSvgMouseMove,
    onDragEnd,
    menuStyle,
    openContextMenu,
    closeMenu,
    getMinTemp,
    getMaxTemp,
    onAddNode,
    onRemoveNode,
    openEditModal,
    onEditConfirm,
  }
}
