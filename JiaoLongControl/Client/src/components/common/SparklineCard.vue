<script lang="ts" setup>
import {computed} from 'vue'
import {buildSparkline} from '@/utils/chart'

/**
 * 迷你监控卡片: 标签 + 当前值 + SVG sparkline。
 * GPU / RyzenSmu 等页面的实时监控小卡共用此组件。
 */
const props = withDefaults(
  defineProps<{
    /** 指标名 */
    label: string
    /** 当前值 (调用方已完成格式化) */
    value: number | string
    /** 值单位 (如 % / MHz / °C / RPM) */
    unit?: string
    /** 单位字号 class, 默认 text-[10px] */
    unitClass?: string
    /** 值文本 class, 默认 ink 色; 高温警示等场景可覆盖 */
    valueClass?: string
    /** 历史数据缓冲 */
    history: number[]
    /** y 轴数值上限 */
    max: number
    /** 曲线颜色 (hex) */
    color: string
    /** 渐变 id, 必须全页唯一 */
    gradientId: string
    /** 折线宽, 默认 1.2 */
    strokeWidth?: number
    /** 渐变顶部不透明度, 默认 0.3 */
    gradientOpacity?: number
    /** svg 元素 class, 默认 h-8 + opacity-70 */
    svgClass?: string
  }>(),
  {
    unit: '',
    unitClass: 'text-[10px]',
    valueClass: 'text-base font-bold text-ink font-mono',
    strokeWidth: 1.2,
    gradientOpacity: 0.3,
    svgClass: 'w-full h-8 opacity-70 mt-1',
  },
)

const chart = computed(() =>
  buildSparkline(props.history, { width: 160, height: 40, max: props.max, area: true }),
)
</script>

<template>
  <div
    class="bg-ink/[0.02] border border-ink/[0.04] p-3 rounded-lg flex flex-col justify-between"
  >
    <div>
      <span class="text-[10px] text-gray-500 block">{{ label }}</span>
      <span :class="valueClass">
        {{ value }}
        <span :class="unitClass" class="text-gray-500 font-bold">{{ unit }}</span>
      </span>
    </div>
    <svg :class="svgClass" preserveAspectRatio="none" viewBox="0 0 160 40">
      <defs>
        <linearGradient :id="gradientId" x1="0" x2="0" y1="0" y2="1">
          <stop :stop-color="color" :stop-opacity="gradientOpacity" offset="0%" />
          <stop :stop-color="color" offset="100%" stop-opacity="0" />
        </linearGradient>
      </defs>
      <path
        :d="chart.line"
        :stroke="color"
        :stroke-linecap="strokeWidth > 1.2 ? 'round' : undefined"
        :stroke-width="strokeWidth"
        fill="none"
      />
      <path :d="chart.area" :fill="`url(#${gradientId})`" />
    </svg>
  </div>
</template>
