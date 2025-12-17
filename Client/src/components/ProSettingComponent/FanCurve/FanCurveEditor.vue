<template>
	<a-card class="fan-curve-card" :bordered="false" @mouseup="onMouseUp" @click="onBackgroundClick">
		<div ref="chartWrapperRef" class="chart-wrapper">
			<svg ref="svgRef" @mousemove="onMouseMove">
				<g class="grid">
					<line
						v-for="i in 17"
						:key="'h' + i"
						:x1="margin"
						:x2="width - margin"
						:y1="i * gridY + margin"
						:y2="i * gridY + margin"
					/>
					<line
						v-for="i in 17"
						:key="'v' + i"
						:y1="margin"
						:y2="height - margin"
						:x1="i * gridX + margin"
						:x2="i * gridX + margin"
					/>
				</g>

				<polyline :points="linePoints" fill="none" stroke="#165DFF" stroke-width="2" />

				<g v-for="(point, index) in points" :key="index">
					<circle
						:cx="tempToX(point.temp)"
						:cy="speedToY(point.speed)"
						r="8"
						class="point"
						@mousedown.prevent="onMouseDown(index)"
						@contextmenu.prevent="onPointClick(index, $event)"
					/>
					<text
						:x="tempToX(point.temp) + 10"
						:y="speedToY(point.speed) - 10"
						font-size="12"
						fill="#444"
					>
						{{ point.temp }}°C / {{ point.speed }} RPM
					</text>
				</g>
			</svg>

			<div v-if="selectedIndex !== null" class="toolbar" :style="toolbarStyle" @mousedown.stop>
				<a-button size="small" @click="onAddNode">添加节点</a-button>
				<a-button size="small" :disabled="points.length <= 2" @click="onRemoveNode"
					>删除节点</a-button
				>
				<a-button size="small" @click.stop="showEdit = true">编辑节点</a-button>
			</div>
		</div>

		<a-modal v-model:visible="showEdit" title="编辑节点" :mask-closable="false" @ok="onEditOk">
			<a-space
				v-if="selectedIndex !== null && points[selectedIndex]"
				direction="vertical"
				size="large"
			>
				<a-input-number
					v-model="points[selectedIndex].temp"
					:min="tempRange[0]"
					:max="tempRange[1]"
				>
					<template #prepend>温度</template>
				</a-input-number>
				<a-input-number
					v-model="points[selectedIndex].speed"
					:min="speedRange[0]"
					:max="speedRange[1]"
				>
					<template #prepend>转速</template>
				</a-input-number>
			</a-space>
		</a-modal>
	</a-card>
</template>

<script lang="ts" setup>
import { computed, ref, reactive, CSSProperties } from 'vue'
import { useFanCurve } from './useFanCurve'
import { useCoordinateTransform } from './useCoordinateTransform'

const width = 740
const height = 600
const margin = 40
const tempRange: [number, number] = [60, 100]
const speedRange: [number, number] = [1500, 5800]
const gridX = (width - 2 * margin) / 10
const gridY = (height - 2 * margin) / 10

const points = useFanCurve()
const { tempToX, speedToY, xToTemp, yToSpeed } = useCoordinateTransform(
	width,
	height,
	margin,
	tempRange,
	speedRange
)

const svgRef = ref<SVGSVGElement | null>(null)
const chartWrapperRef = ref<HTMLDivElement | null>(null)
const selectedIndex = ref<number | null>(null)
const showEdit = ref(false)

const dragging = reactive({ index: null as number | null })

const linePoints = computed(() =>
	points.map((p) => `${tempToX(p.temp)},${speedToY(p.speed)}`).join(' ')
)

const toolbarStyle = computed<CSSProperties>(() => {
	if (selectedIndex.value === null) return {}
	const point = points[selectedIndex.value]
	const x = tempToX(point.temp)
	const y = speedToY(point.speed)

	const rect = chartWrapperRef.value?.getBoundingClientRect()
	const svgRect = svgRef.value?.getBoundingClientRect()
	if (!rect || !svgRect) return {}

	const offsetX = svgRect.left - rect.left
	const offsetY = svgRect.top - rect.top

	return {
		position: 'absolute',
		left: `${x + offsetX + 10}px`,
		top: `${y + offsetY - 30}px`,
		zIndex: 10,
		backgroundColor: 'rgba(255,255,255,0.95)',
		padding: '6px',
		borderRadius: '6px',
		boxShadow: '0 2px 8px rgba(0,0,0,0.15)',
		display: 'flex',
		gap: '6px'
	}
})

function onMouseDown(index: number) {
	dragging.index = index
	selectedIndex.value = null
}

function onMouseMove(event: MouseEvent) {
	if (dragging.index === null || !svgRef.value) return
	const rect = svgRef.value.getBoundingClientRect()
	const x = Math.max(margin, Math.min(event.clientX - rect.left, width - margin))
	const y = Math.max(margin, Math.min(event.clientY - rect.top, height - margin))

	const idx = dragging.index
	const newTemp = Math.round(xToTemp(x))
	const newSpeed = Math.round(yToSpeed(y))

	if (idx > 0 && newTemp <= points[idx - 1].temp) return
	if (idx < points.length - 1 && newTemp >= points[idx + 1].temp) return

	points[idx].temp = newTemp
	points[idx].speed = Math.max(speedRange[0], Math.min(newSpeed, speedRange[1]))
}

function onMouseUp() {
	dragging.index = null
}

function onPointClick(index: number, event: MouseEvent) {
	event.preventDefault()
	selectedIndex.value = index
}

function onBackgroundClick(event: MouseEvent) {
	const el = event.target as HTMLElement
	if (!el.classList.contains('point')) {
		selectedIndex.value = null
		showEdit.value = false
	}
}

function onAddNode() {
	if (selectedIndex.value === null) return
	const idx = selectedIndex.value
	const curr = points[idx]
	const next = points[idx + 1] || { temp: tempRange[1], speed: curr.speed }

	const newTemp = Math.min(tempRange[1] - 1, Math.round((curr.temp + next.temp) / 2))
	const newSpeed = Math.round((curr.speed + next.speed) / 2)

	points.splice(idx + 1, 0, { temp: newTemp, speed: newSpeed })
	selectedIndex.value = idx + 1
	showEdit.value = false
}

function onRemoveNode() {
	if (selectedIndex.value === null || points.length <= 2) return
	points.splice(selectedIndex.value, 1)
	selectedIndex.value = null
	showEdit.value = false
}

function onEditOk() {
	showEdit.value = false
}
</script>

<style lang="scss" scoped>
.fan-curve-card {
	height: 100%;
	margin: 0 auto;
	border-radius: 12px;
	box-shadow: 0 6px 16px rgba(0, 0, 0, 0.05);

	:deep(.arco-card-body) {
		height: 100%;
		display: flex;
		flex-direction: column;
	}
}

.chart-wrapper {
	position: relative;
	flex: 1;
	background: #f8f9fa;
	padding: 10px;
	border-radius: 8px;
	border: 1px solid #e5e6eb;

	svg {
		width: 100%;
		height: 100%;

		.grid line {
			stroke: #e5e6eb;
			stroke-dasharray: 4;
		}

		.point {
			fill: #165dff;
			stroke: #fff;
			stroke-width: 2;
			cursor: pointer;

			&:hover {
				fill: #3a7afe;
			}
		}
	}

	.toolbar {
		user-select: none;

		a-button {
			min-width: 70px;
		}
	}
}
</style>
