<script setup lang="ts">
import Heatmap from '@/components/ProSettingComponent/Heatmap.vue'
import type { HardwareMonitorInfo } from '@/stores/interfaces.ts'
const props = defineProps<{
	data: HardwareMonitorInfo
}>()
function CpuHeatMapData() {
	const data = props.data.Cpu
	const cores = data.Load.filter((item) => item.Name.includes('CPU Core #')).map(
		(item) => item.Value
	)

	if (!cores.length) return []

	// 检测 CCD 数量
	const ccdCount = data.Temperature.filter((item) => item.Name.match(/^CCD\d+/i)).length || 1 // 如果没有 CCD 信息，默认 1

	const coresPerCCD = Math.ceil(cores.length / ccdCount) // 核心数均分到每个 CCD
	const CCDs: { name: string; values: number[] }[] = []

	for (let i = 0; i < cores.length; i += coresPerCCD) {
		CCDs.push({
			name: `CCD${CCDs.length + 1}`,
			values: cores.slice(i, i + coresPerCCD)
		})
	}

	return CCDs
}
</script>

<template>
	<div class="cpuMap">
		<heatmap :title="props.data.Cpu.Name" :data="CpuHeatMapData()"></heatmap>
	</div>
</template>

<style scoped>
.cpuMap {
	width: 100%;
	height: 100%;
	position: absolute;
}
</style>
