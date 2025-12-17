<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { VueUiSparklineDatasetItem } from 'vue-data-ui'
import SparkLine from '@/components/ProSettingComponent/SparkLine.vue'
import WMIOperation from '@/utils/WMIOperation.ts'

const CPUFanSpeedData = ref<VueUiSparklineDatasetItem[]>([
	{
		period: 'CPUFanSpeed',
		value: 0
	}
])
const GPUFanSpeedData = ref<VueUiSparklineDatasetItem[]>([
	{
		period: 'GPUFanSpeed',
		value: 0
	}
])
async function GetFanSpeed() {
	if (CPUFanSpeedData.value.length > 20) {
		CPUFanSpeedData.value.shift()
	}
	if (GPUFanSpeedData.value.length > 20) {
		GPUFanSpeedData.value.shift()
	}
	const { CPUFanSpeed, GPUFanSpeed } = await WMIOperation.Fan.GetFanSpeed()
	CPUFanSpeedData.value.push({
		period: 'CPUFanSpeed',
		value: CPUFanSpeed
	})
	GPUFanSpeedData.value.push({
		period: 'GPUFanSpeed',
		value: GPUFanSpeed
	})
}
onMounted(async () => {
	await GetFanSpeed()
	setInterval(() => GetFanSpeed(), 1000)
})
</script>

<template>
	<div class="fanSpeed">
		<SparkLine class="cell" title="CPUFan" :data="CPUFanSpeedData"></SparkLine>
		<SparkLine class="cell" title="GPUFan" :data="GPUFanSpeedData"></SparkLine>
	</div>
</template>

<style scoped>
.fanSpeed {
	width: 100%;
	height: 100%;
	.cell {
		height: calc(100% / 2);
		width: 100%;
	}
}
</style>
