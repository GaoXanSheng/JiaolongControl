<script setup lang="ts">
import { VueUiHeatmap, type VueUiHeatmapConfig } from 'vue-data-ui'

type VueUiHeatmapDatasetItem = {
	name: string
	values: number[]
}

interface Props {
	title: string
	data: VueUiHeatmapDatasetItem[]
}

const props = defineProps<Props>()
const config: VueUiHeatmapConfig = {
	style: {
		backgroundColor: '#F3F4F6',
		color: '#1A1A1A',
		fontFamily: 'inherit',
		layout: {
			padding: { top: 36, right: 12, bottom: 12, left: 48 },
			cells: {
				value: { show: true, fontSize: 8, bold: false, roundingValue: 0.1, color: '#1A1A1A' },
				colors: { hot: '#ff6400', cold: '#5f8bee', underlayer: '#F3F4F6' },
				spacing: 0.5,
				selected: { border: 2, color: '#1A1A1A' }
			},
			dataLabels: {
				prefix: '',
				suffix: '',
				xAxis: {
					show: true,
					values: [
						1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24,
						25, 26
					],
					fontSize: 12,
					color: '#1A1A1A',
					bold: false,
					offsetX: 0,
					offsetY: 0,
					showOnlyAtModulo: null,
					rotation: 0
				},
				yAxis: {
					show: true,
					values: [],
					fontSize: 12,
					color: '#1A1A1A',
					bold: false,
					offsetY: 0,
					offsetX: 0
				}
			}
		},
		title: {
			text: props.title,
			color: '#1A1A1A',
			fontSize: 20,
			bold: true,
			textAlign: 'center',
			paddingLeft: 0,
			paddingRight: 0
		},
		legend: {
			show: false
		},
		tooltip: {
			show: true,
			backgroundColor: '#F3F4F6',
			color: '#1A1A1A',
			fontSize: 14,
			roundingValue: 0,
			customFormat: null,
			borderRadius: 4,
			borderColor: '#e1e5e8',
			borderWidth: 1,
			backgroundOpacity: 30,
			position: 'center',
			offsetY: 24
		}
	},
	userOptions: {
		show: false
	},
	table: {
		show: false,
		responsiveBreakpoint: 400,
		colNames: { xAxis: 'Week' },
		th: { backgroundColor: '#F3F4F6', color: '#1A1A1A', outline: 'none' },
		td: { backgroundColor: '#F3F4F6', color: '#1A1A1A', outline: 'none', roundingValue: 0 }
	}
}
</script>

<template>
	<VueUiHeatmap :dataset="props.data" :config="config"></VueUiHeatmap>
</template>

<style scoped></style>
