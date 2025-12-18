<script setup lang="ts">
import { type VueUiSparklineConfig } from 'vue-data-ui'
import { VueUiSparkline } from 'vue-data-ui'
import {type Ref, ref} from 'vue'

interface Props {
	title: string
	data: VueUiSparklineDatasetItem[]
}
const props = defineProps<Props>()

type VueUiSparklineDatasetItem = {
	period: string
	value: number
}
const dataset: Ref<VueUiSparklineDatasetItem[]> = ref(props.data)
const config: VueUiSparklineConfig = {
	type: 'line',
	downsample: { threshold: 500 },
	style: {
		backgroundColor: '#f3f4f6ff',
		fontFamily: 'inherit',
		chartWidth: 290,
		scaleMin: null,
		scaleMax: null,
		padding: { top: 12, right: 0, bottom: 3, left: 0 },
		animation: { show: true, animationFrames: 360 },
		line: { color: '#5f8bee', strokeWidth: 3, smooth: true },
		bar: { borderRadius: 3, color: '#5f8bee' },
		zeroLine: { color: '#505050', strokeWidth: 1 },
		plot: { show: true, radius: 4, stroke: '#FFFFFF', strokeWidth: 1 },
		verticalIndicator: { show: true, strokeWidth: 1.5, color: '#5f8bee', strokeDasharray: 3 },
		dataLabel: {
			show: true,
			offsetX: 0,
			offsetY: 0,
			position: 'left',
			fontSize: 48,
			bold: true,
			color: '#1A1A1A',
			roundingValue: 1,
			valueType: 'latest',
			prefix: '',
			suffix: ''
		},
		title: {
			show: true,
			textAlign: 'left',
			color: '#1A1A1A',
			fontSize: 16,
			bold: true,
			text: props.title
		},
		tooltip: {
			show: false,
			fontSize: 14,
			color: '#1A1A1A',
			backgroundColor: '#FFFFFF',
			offsetY: 0,
			borderWidth: 0,
			borderColor: '#E1E5E8',
			borderRadius: 2,
			backgroundOpacity: 100
		},
		area: { show: true, useGradient: true, opacity: 30, color: '#5f8bee' }
	}
}
</script>

<template>
	<VueUiSparkline :dataset="dataset" :config="config" />
</template>

<style scoped></style>
