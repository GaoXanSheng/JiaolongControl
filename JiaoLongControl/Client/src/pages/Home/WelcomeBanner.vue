<script setup lang="ts">
import imgCPU from '@/assets/icon/iconCPU.png'
import imgGPU from '@/assets/icon/gpu2.png'

defineProps<{
  cpuTemp: number
  gpuTemp: number
  activeModeName: string
  activeModeIcon: string
}>()
</script>

<template>
  <div
    class="glass-card h-[280px] relative overflow-hidden flex flex-col justify-between p-10 group"
  >
    <!-- 右侧背景视频和渐变层，无缝融入背景 -->
    <div class="absolute right-0 top-0 bottom-0 w-[50%] z-0 pointer-events-none overflow-hidden">
      <video
        src="@/assets/BackgroundVideo.mp4"
        autoplay
        loop
        muted
        playsinline
        class="w-full h-full object-cover opacity-60 group-hover:opacity-75 transition-opacity duration-700 scale-115 transform origin-right"
      ></video>
    </div>
    <div
      class="absolute inset-0 bg-gradient-to-r from-cyber-card via-cyber-card to-transparent z-10 pointer-events-none"
    ></div>

    <!-- 文字内容 & 视频 -->
    <div class="flex justify-between items-start relative z-20">
      <div class="space-y-2 max-w-xl">
        <p class="text-1xl text-gray-400">欢迎回来</p>
        <h1
          class="text-2xl font-semibold tracking-wide bg-gradient-to-r from-ink via-indigo-400 to-purple-400 bg-clip-text text-transparent"
        >
          JiaoLong Control
        </h1>
        <p class="text-sm text-gray-400 mt-2">为极致性能而生，掌控每一分潜能</p>
      </div>
    </div>

    <!-- 底部指标 -->
    <div class="relative z-20 flex gap-12 mt-auto">
      <div class="flex items-center gap-3">
        <div class="w-10 h-10 rounded-full bg-blue-500/10 flex items-center justify-center">
          <span
            class="icon-mask icon-tint-blue-bright w-6 h-6"
            :style="{ WebkitMaskImage: `url(${imgCPU})`, maskImage: `url(${imgCPU})` }"
          ></span>
        </div>
        <div>
          <div class="text-xs text-gray-400">CPU</div>
          <div class="text-xl font-semibold">{{ cpuTemp }}°C</div>
        </div>
      </div>
      <div class="flex items-center gap-3">
        <div class="w-10 h-10 rounded-full bg-purple-500/10 flex items-center justify-center">
          <span
            class="icon-mask icon-tint-green w-6 h-6"
            :style="{ WebkitMaskImage: `url(${imgGPU})`, maskImage: `url(${imgGPU})` }"
          ></span>
        </div>
        <div>
          <div class="text-xs text-gray-400">GPU</div>
          <div class="text-xl font-semibold">{{ gpuTemp }}°C</div>
        </div>
      </div>
      <div class="flex items-center gap-3">
        <div class="w-10 h-10 rounded-full bg-ink/5 flex items-center justify-center">
          <span
            class="icon-mask icon-silhouette w-5 h-5 opacity-90"
            :style="{
              WebkitMaskImage: `url(${activeModeIcon})`,
              maskImage: `url(${activeModeIcon})`,
            }"
          ></span>
        </div>
        <div>
          <div class="text-xs text-gray-400">模式</div>
          <div class="text-xl font-semibold">{{ activeModeName }}</div>
        </div>
      </div>
    </div>
  </div>
</template>
