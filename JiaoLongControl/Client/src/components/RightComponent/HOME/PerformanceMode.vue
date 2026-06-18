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
    <h2 class="text-[15px] font-medium text-white/90 mb-6">性能模式</h2>
    <div class="flex-1 grid grid-cols-4 gap-4 items-center">
      <button
        v-for="mode in modes" :key="mode.id"
        @click="emit('change-mode', mode.id)"
        :class="[
          'py-6 rounded-2xl flex flex-col items-center justify-center gap-3 transition-all duration-300',
          mode.active 
            ? 'bg-purple-900/20 border border-purple-500 shadow-[0_0_15px_rgba(138,43,226,0.3)]' 
            : 'bg-[#1A1C23] border border-transparent hover:bg-white/5'
        ]"
      >
        <img
          :src="mode.icon"
          :class="['w-10 h-10 object-contain transition-all duration-300 brightness-0 invert', mode.active ? 'opacity-100 scale-110' : 'opacity-40 group-hover:opacity-100']"
        />
        <span :class="['text-xs', mode.active ? 'text-white font-medium' : 'text-gray-500']">{{ mode.name }}</span>
      </button>
    </div>
  </div>
</template>
