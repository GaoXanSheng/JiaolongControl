<script setup lang="ts">
import type { HardwareMonitorInfo } from '@/stores/interfaces.ts'
import SparkLine from '@/components/ProSettingComponent/SparkLine.vue'
import { ref } from 'vue'

const props = defineProps<{
	data: HardwareMonitorInfo
}>()
const GpuData = ref({
	Clock: {
		Core: [
			{
				period: 'GPU Core Clock',
				value: 0
			}
		],
		SoC: [
			{
				period: 'GPU SoC Clock',
				value: 0
			}
		],
		Memory: [
			{
				period: 'GPU Memory Clock',
				value: 0
			}
		]
	},
	Temperature: [
		{
			period: 'GPU Temperature',
			value: 0
		}
	],
	Voltage: {
		Core: [
			{
				period: 'GPU Core Voltage',
				value: 0
			}
		],
		SoC: [
			{
				period: 'GPU SoC Voltage',
				value: 0
			}
		]
	},
	Power: {
		Core: [
			{
				period: 'GPU Core Power',
				value: 0
			}
		],
		SoC: [
			{
				period: 'GPU SoC Power',
				value: 0
			}
		]
	}
})
setInterval(() => {
	const amd = props.data.GpuAmd
	if (GpuData.value.Clock.Core.length > 20) {
		GpuData.value.Clock.Core.shift()
		GpuData.value.Clock.SoC.shift()
		GpuData.value.Clock.Memory.shift()
		GpuData.value.Temperature.shift()
		GpuData.value.Voltage.Core.shift()
		GpuData.value.Voltage.SoC.shift()
		GpuData.value.Power.Core.shift()
		GpuData.value.Power.SoC.shift()
	}
	amd.Clock.map((item) => {
		if (item.Name.includes('Core')) {
			GpuData.value.Clock.Core.push({
				period: 'GPU Core Clock',
				value: item.Value
			})
		}
		if (item.Name.includes('SoC')) {
			GpuData.value.Clock.SoC.push({
				period: 'GPU Core Clock',
				value: item.Value
			})
		}
		if (item.Name.includes('Memory')) {
			GpuData.value.Clock.Memory.push({
				period: 'GPU Core Clock',
				value: item.Value
			})
		}
	})
	GpuData.value.Temperature.push({
		period: 'GPU Temperature',
		value: amd.Temperature[0].Value
	})
	amd.Voltage.map((item) => {
		if (item.Name.includes('Core')) {
			GpuData.value.Voltage.Core.push({
				period: 'GPU Core Voltage',
				value: item.Value
			})
		}
		if (item.Name.includes('SoC')) {
			GpuData.value.Voltage.SoC.push({
				period: 'GPU SoC Voltage',
				value: item.Value
			})
		}
	})
	amd.Power.map((item) => {
		if (item.Name.includes('Core')) {
			GpuData.value.Power.Core.push({
				period: 'GPU Core Power',
				value: item.Value
			})
		}
		if (item.Name.includes('SoC')) {
			GpuData.value.Power.SoC.push({
				period: 'GPU SoC Power',
				value: item.Value
			})
		}
	})
}, 1000)
</script>

<template>
	<div class="grid-9x1">
		<div class="title cell">{{ props.data.GpuAmd.Name }}</div>
		<SparkLine class="cell" :title="`Clock Core`" :data="GpuData.Clock.Core"></SparkLine>
		<SparkLine class="cell" :title="`Clock SoC`" :data="GpuData.Clock.SoC"></SparkLine>
		<SparkLine class="cell" :title="`Clock Memory`" :data="GpuData.Clock.Memory"></SparkLine>
		<SparkLine class="cell" :title="`Temperature`" :data="GpuData.Temperature"></SparkLine>
		<SparkLine class="cell" :title="`Voltage Core`" :data="GpuData.Voltage.Core"></SparkLine>
		<SparkLine class="cell" :title="`Voltage SoC`" :data="GpuData.Voltage.SoC"></SparkLine>
		<SparkLine class="cell" :title="`Power Core`" :data="GpuData.Power.Core"></SparkLine>
		<SparkLine class="cell" :title="`Power SoC`" :data="GpuData.Power.SoC"></SparkLine>
	</div>
</template>

<style lang="scss" scoped>
.grid-9x1 {
	width: 100%;
	height: 100%;
	display: flex;
	flex-direction: column;
	.title {
		height: 5%;
		display: flex;
		align-items: center;
		justify-content: center;
		font-weight: bold;
		font-size: 1.2em;
	}
	.cell {
		height: calc(calc(100% - 5%) / 8);
		width: 100%;
	}
}
</style>
