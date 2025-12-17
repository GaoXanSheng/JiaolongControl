<script setup lang="ts">
import type { HardwareMonitorInfo } from '@/stores/interfaces.ts'
import { ref } from 'vue'
import SparkLine from '@/components/ProSettingComponent/SparkLine.vue'

const props = defineProps<{
	data: HardwareMonitorInfo
}>()
const GpuData = ref({
	Temperature: {
		Core: [
			{
				period: 'GPU Core',
				value: 0
			}
		],
		HotSpot: [
			{
				period: 'GPU Hot Spot',
				value: 0
			}
		]
	},
	Clock: {
		Core: [
			{
				period: 'GPU Core',
				value: 0
			}
		],
		Memory: [
			{
				period: 'GPU Memory',
				value: 0
			}
		]
	},
	Load: {
		Core: [
			{
				period: 'GPU Core',
				value: 0
			}
		],
		MemoryController: [
			{
				period: 'GPU Memory Controller',
				value: 0
			}
		],
		GPUVideoEngine: [
			{
				period: 'GPU Video Engine',
				value: 0
			}
		],
		D3DVideoDecode: [
			{
				period: 'D3D Video Decode',
				value: 0
			}
		],
		D3DCopy: [
			{
				period: 'D3D Copy',
				value: 0
			}
		],
		GPUMemory: [
			{
				period: 'GPU Memory',
				value: 0
			}
		]
	},
	SmallData: {
		D3DDedicatedMemoryUsed: [
			{
				period: 'D3D Dedicated Memory Used',
				value: 0
			}
		],
		D3DSharedMemoryUsed: [
			{
				period: 'D3D Shared Memory Used',
				value: 0
			}
		],
		GPUMemoryTotal: [
			{
				period: 'GPU Memory Total',
				value: 0
			}
		],
		GPUMemoryFree: [
			{
				period: 'GPU Memory Free',
				value: 0
			}
		],
		GPUMemoryUsed: [
			{
				period: 'GPU Memory Used',
				value: 0
			}
		]
	},
	Power: [
		{
			period: 'GPU Package',
			value: 0
		}
	]
})
setInterval(() => {
	if (GpuData.value.Clock.Core.length > 20) {
		GpuData.value.Clock.Core.shift()
		GpuData.value.Clock.Memory.shift()
		GpuData.value.Temperature.Core.shift()
	}
	const nvidia = props.data.GpuNvidia
	nvidia.Clock.map((value) => {
		if (value.Name.includes('Core')) {
			GpuData.value.Clock.Core.push({
				period: 'GPU Core Clock',
				value: value.Value
			})
		}
		if (value.Name.includes('Memory')) {
			GpuData.value.Clock.Memory.push({
				period: 'GPU Memory Clock',
				value: value.Value
			})
		}
	})
	nvidia.Temperature.map((value) => {
		if (value.Name.includes('Core')) {
			GpuData.value.Temperature.Core.push({
				period: 'GPU Core Temperature',
				value: value.Value
			})
		}
		if (value.Name.includes('Hot Spot')) {
			GpuData.value.Temperature.HotSpot.push({
				period: 'GPU Hot Spot Temperature',
				value: value.Value
			})
		}
	})
	nvidia.Load.map((value) => {
		if (value.Name.includes('Core')) {
			GpuData.value.Load.Core.push({
				period: 'GPU Core Load',
				value: value.Value
			})
		}
		if (value.Name.includes('Memory Controller')) {
			GpuData.value.Load.MemoryController.push({
				period: 'GPU Memory Controller Load',
				value: value.Value
			})
		}
		if (value.Name.includes('GPU Video Engine')) {
			GpuData.value.Load.GPUVideoEngine.push({
				period: 'GPU Video Engine Load',
				value: value.Value
			})
		}
		if (value.Name.includes('D3D Video Decode')) {
			GpuData.value.Load.D3DVideoDecode.push({
				period: 'D3D Video Decode Load',
				value: value.Value
			})
		}
		if (value.Name.includes('D3D Copy')) {
			GpuData.value.Load.D3DCopy.push({
				period: 'D3D Copy Load',
				value: value.Value
			})
		}
		if (value.Name.includes('GPU Memory')) {
			GpuData.value.Load.GPUMemory.push({
				period: 'GPU Memory Load',
				value: value.Value
			})
		}
	})
	nvidia.SmallData.map((value) => {
		if (value.Name.includes('D3D Dedicated Memory Used')) {
			GpuData.value.SmallData.D3DDedicatedMemoryUsed.push({
				period: 'D3D Dedicated Memory Used',
				value: value.Value
			})
		}
		if (value.Name.includes('D3D Shared Memory Used')) {
			GpuData.value.SmallData.D3DSharedMemoryUsed.push({
				period: 'D3D Shared Memory Used',
				value: value.Value
			})
		}
		if (value.Name.includes('GPU Memory Total')) {
			GpuData.value.SmallData.GPUMemoryTotal.push({
				period: 'GPU Memory Total',
				value: value.Value
			})
		}
		if (value.Name.includes('GPU Memory Free')) {
			GpuData.value.SmallData.GPUMemoryFree.push({
				period: 'GPU Memory Free',
				value: value.Value
			})
		}
		if (value.Name.includes('GPU Memory Used')) {
			GpuData.value.SmallData.GPUMemoryUsed.push({
				period: 'GPU Memory Used',
				value: value.Value
			})
		}
	})
	GpuData.value.Power.push({
		period: 'GPU Package Power',
		value: nvidia.Power[0].Value
	})
}, 1000)
</script>

<template>
	<div class="title cell">{{ props.data.GpuNvidia.Name }}</div>
	<SparkLine
		class="cell"
		:title="`GPU Core Temperature`"
		:data="GpuData.Temperature.Core"
	></SparkLine>
	<SparkLine
		class="cell"
		:title="`GPU Hot Spot Temperature`"
		:data="GpuData.Temperature.HotSpot"
	></SparkLine>
	<SparkLine class="cell" :title="`Clock Core Load`" :data="GpuData.Load.Core"></SparkLine>
	<SparkLine class="cell" :title="`GPU Package Power`" :data="GpuData.Power"></SparkLine>
	<SparkLine
		class="cell"
		:title="`GPU Memory Used`"
		:data="GpuData.SmallData.GPUMemoryUsed"
	></SparkLine>
	<div class="container">
		<!--		<SparkLine class="cell" :title="`GPU Core Clock`" :data="GpuData.Clock.Core"></SparkLine>-->
		<!--		<SparkLine class="cell" :title="`GPU Memory Clock`" :data="GpuData.Clock.Memory"></SparkLine>-->
		<SparkLine class="cell" :title="`GPU Memory Load`" :data="GpuData.Load.GPUMemory"></SparkLine>
		<SparkLine
			class="cell"
			:title="`GPU Video Engine`"
			:data="GpuData.Load.GPUVideoEngine"
		></SparkLine>
		<SparkLine
			class="cell"
			:title="`Memory Controller`"
			:data="GpuData.Load.MemoryController"
		></SparkLine>
		<SparkLine
			class="cell"
			:title="`GPU Memory Free`"
			:data="GpuData.SmallData.GPUMemoryFree"
		></SparkLine>
		<SparkLine
			class="cell"
			:title="`Video Decode Load`"
			:data="GpuData.Load.D3DVideoDecode"
		></SparkLine>
		<SparkLine class="cell" :title="`Copy Load`" :data="GpuData.Load.D3DCopy"></SparkLine>
		<SparkLine
			class="cell"
			:title="`Dedicated Memory`"
			:data="GpuData.SmallData.D3DDedicatedMemoryUsed"
		></SparkLine>
		<SparkLine
			class="cell"
			:title="`Shared Memory`"
			:data="GpuData.SmallData.D3DSharedMemoryUsed"
		></SparkLine>
	</div>
</template>

<style lang="scss" scoped>
.container {
	display: grid;
	grid-template-columns: 50% 50%;
	width: 100%;
	.cell {
		padding: 8px;
		.vue-ui-sparkline-title {
			font-size: 12px;
		}
	}
}
.title {
	height: 5%;
	display: flex;
	align-items: center;
	justify-content: center;
	font-weight: bold;
	font-size: 1em;
}
</style>
