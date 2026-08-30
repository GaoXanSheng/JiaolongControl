<script setup lang="ts">
import { SystemPerMode } from '@/utils/bridge'

defineProps<{
  modes: Array<{
    id: SystemPerMode
    name: string
    icon: string
    active: boolean
  }>
}>()

const emit = defineEmits<{
  (e: 'change-mode', id: SystemPerMode): void
}>()
</script>

<template>
  <div class="col-span-5 glass-card p-6 flex flex-col">
    <h2 class="text-[15px] font-medium text-ink/90 mb-6">性能模式</h2>
    <div class="flex-1 grid grid-cols-3 gap-3 items-center">
      <button
        v-for="mode in modes"
        :key="mode.id"
        :class="[
          'py-6 rounded-2xl flex flex-col items-center justify-center gap-3 transition-all duration-300',
          mode.active
            ? 'mode-btn-active bg-purple-900/20 border'
            : 'bg-panel-raised border border-ink/[0.06] hover:bg-ink/5',
        ]"
        @click="emit('change-mode', mode.id)"
      >
        <span
          v-if="mode.active"
          class="icon-mask icon-silhouette-accent w-10 h-10 transition-all duration-300 opacity-100 scale-110"
          :style="{ WebkitMaskImage: `url(${mode.icon})`, maskImage: `url(${mode.icon})` }"
        ></span>
        <span
          v-else
          class="icon-mask icon-silhouette w-10 h-10 transition-all duration-300 opacity-65 group-hover:opacity-100"
          :style="{ WebkitMaskImage: `url(${mode.icon})`, maskImage: `url(${mode.icon})` }"
        ></span>
        <span :class="['text-xs', mode.active ? 'text-ink font-medium' : 'text-gray-400']">{{
          mode.name
        }}</span>
      </button>
    </div>
  </div>
</template>

<style scoped>
/* 选中模式按钮: 深色紫 500 边框 + 发光; 浅色为淡灰底 + 灰边框, 不发光 */
.mode-btn-active {
  border-color: #a855f7;
  box-shadow: 0 0 15px rgba(138, 43, 226, 0.3);
}

[data-theme='light'] .mode-btn-active {
  border-color: rgba(13, 14, 21, 0.16);
  background-color: rgba(13, 14, 21, 0.05);
  box-shadow: none;
}
</style>
