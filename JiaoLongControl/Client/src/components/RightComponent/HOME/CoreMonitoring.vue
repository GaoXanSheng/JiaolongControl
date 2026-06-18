<script setup lang="ts">
import { computed } from 'vue'
import VChart from 'vue-echarts'
import { use } from 'echarts/core'
import { CanvasRenderer } from 'echarts/renderers'
import { PieChart } from 'echarts/charts'
import { GridComponent, TooltipComponent } from 'echarts/components'

use([CanvasRenderer, PieChart, GridComponent, TooltipComponent])

const props = defineProps<{
  cpuUsage: number
  gpuUsage: number
  cpuTemp: number
  gpuTemp: number
  cpuFreq: string
  gpuFreq: string
}>()

const getRingOption = (value: number, colorStart: string, colorEnd: string, format: string = '{value}%') => ({
  series: [
    {
      type: 'pie',
      radius: ['75%', '90%'],
      avoidLabelOverlap: false,
      silent: true,
      label: {
        show: true,
        position: 'center',
        formatter: format,
        fontSize: 24,
        fontWeight: 'bold',
        color: '#FFFFFF'
      },
      data: [
        {
          value: value,
          itemStyle: {
            color: {
              type: 'linear', x: 0, y: 0, x2: 1, y2: 1,
              colorStops: [{ offset: 0, color: colorStart }, { offset: 1, color: colorEnd }]
            },
            borderRadius: 10
          }
        },
        {
          value: 100 - (format.includes('%') ? value : (value / 100) * 100),
          itemStyle: { color: 'rgba(255, 255, 255, 0.05)' }
        }
      ]
    }
  ]
})

const cpuUsageOption = computed(() => getRingOption(props.cpuUsage, '#3B82F6', '#8A2BE2'))
const gpuUsageOption = computed(() => getRingOption(props.gpuUsage, '#10B981', '#3B82F6'))
const cpuTempOption = computed(() => getRingOption(props.cpuTemp, '#3B82F6', '#3B82F6', '{value}°C'))
const gpuTempOption = computed(() => getRingOption(props.gpuTemp, '#8A2BE2', '#8A2BE2', '{value}°C'))
</script>

<template>
  <div class="col-span-7 glass-card p-6 flex flex-col">
    <h2 class="text-[15px] font-medium text-white/90 mb-2">核心监控</h2>
    <div class="flex-1 flex justify-around items-center px-4">
      <!-- CPU 使用率 -->
      <div class="flex flex-col items-center">
        <div class="w-32 h-32 relative">
          <VChart :option="cpuUsageOption" autoresize />
        </div>
        <span class="text-xs text-gray-400 mt-2">CPU 使用率</span>
        <span class="text-xs text-gray-500 mt-1">{{ cpuFreq }}</span>
      </div>

      <!-- GPU 使用率 -->
      <div class="flex flex-col items-center">
        <div class="w-32 h-32 relative">
          <VChart :option="gpuUsageOption" autoresize />
        </div>
        <span class="text-xs text-gray-400 mt-2">GPU 使用率</span>
        <span class="text-xs text-gray-500 mt-1">{{ gpuFreq }}</span>
      </div>

      <!-- CPU 温度 -->
      <div class="flex flex-col items-center">
        <div class="w-32 h-32 relative">
          <VChart :option="cpuTempOption" autoresize />
        </div>
        <span class="text-xs text-gray-400 mt-2">CPU 温度</span>
        <span class="text-xs text-gray-500 mt-1 opacity-0">-</span>
      </div>

      <!-- GPU 温度 -->
      <div class="flex flex-col items-center">
        <div class="w-32 h-32 relative">
          <VChart :option="gpuTempOption" autoresize />
        </div>
        <span class="text-xs text-gray-400 mt-2">GPU 温度</span>
        <span class="text-xs text-gray-500 mt-1 opacity-0">-</span>
      </div>
    </div>
  </div>
</template>
