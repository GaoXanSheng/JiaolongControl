<script setup lang="ts">
import { ref } from 'vue'
import { PerformanceMode, SystemPerMode } from '@/utils/bridge'
import iconQuiet from '@/assets/icon/iconQuiet.png'
import iconBalanced from '@/assets/icon/iconBalanced.png'
import iconPerformance from '@/assets/icon/iconPerformance.png'
import iconCustom from '@/assets/icon/iconCustom.png'

const performanceModes = ref([
  { id: SystemPerMode.BalanceMode, name: '静音', icon: iconQuiet, active: false },
  { id: SystemPerMode.PerformanceMode, name: '平衡', icon: iconBalanced, active: false },
  { id: SystemPerMode.QuietMode, name: '高性能', icon: iconPerformance, active: false },
  { id: SystemPerMode.CustomMode, name: '自定义', icon: iconCustom, active: false }
])

PerformanceMode.Get().then(res => {
  performanceModes.value.forEach(e => {
    e.active = e.id === res.Data;
  })
})

function setMode(id: SystemPerMode) {
  performanceModes.value.forEach(m => {
    m.active = (m.id === id)
    if (id != SystemPerMode.CustomMode) {
      PerformanceMode.Set(id)
    }
  })
}
</script>

<template>
  <div class="col-span-5 glass-card p-6 flex flex-col">
    <h2 class="text-[15px] font-medium text-white/90 mb-6">性能模式</h2>
    <div class="flex-1 grid grid-cols-4 gap-4 items-center">
      <button
        v-for="mode in performanceModes" :key="mode.id"
        @click="setMode(mode.id)"
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
