<script setup lang="ts">
import RightSide from '@/components/rightSide.vue'
import TitleBar from '@/components/TitleBar.vue'
import useStore, { HomeCardType } from '@/stores'

const store = useStore()
function onClickMenuItem(key: any) {
    store.$state.SwitchPages = key
}
</script>

<template>
  <div class="flex flex-col h-screen text-white overflow-hidden select-none">
    <!-- 极简标题栏 -->
    <TitleBar class="z-50 bg-transparent" />

    <div class="flex flex-1 overflow-hidden">
      
      <!-- 侧边栏 (严格对标图左) -->
      <aside class="w-[240px] flex flex-col shadow-10 mt-6">

        <div class="flex-1 h-10 px-4 flex flex-col justify-between overflow-y-auto no-scrollbar">
          <!-- 主导航 -->
          <nav class="space-y-1.5">
            <button
              v-for="item in HomeCardType" 
              :key="item.eum"
              @click="onClickMenuItem(item.eum)"
              :class="[
                'w-full flex items-center gap-4 px-5 py-3.5 rounded-[5px] transition-all duration-300',
                store.SwitchPages === item.eum ? 'nav-item-active' : 'hover:bg-white/[0.03] text-gray-400 hover:text-gray-1000'
              ]"
            >
              <div class="w-5 h-5 flex items-center justify-center">
                <img 
                  :src="item.icon" 
                  :class="['w-full h-full brightness-0 transition-all', store.SwitchPages === item.eum ? 'invert opacity-100' : 'invert opacity-60 group-hover:opacity-100']" 
                  alt="icon"
                />
              </div>
              <span class="font-medium text-[13px] tracking-wide">{{ item.title }}</span>
            </button>
          </nav>

          <!-- 底部工具/设置 -->
          <div class="pb-6">
            <button class="w-full flex items-center justify-between px-5 py-4 text-gray-400 hover:text-white transition-colors">
              <div class="flex items-center gap-4">
                <icon-settings class="text-lg" />
                <span class="font-medium text-[13px] tracking-wide">更多</span>
              </div>
              <icon-right />
            </button>
          </div>
        </div>
      </aside>

      <!-- 主内容区 -->
      <main class="flex-1 relative overflow-hidden">
        <!-- 主内容背景发光点缀 -->
        <div class="absolute top-[-20%] right-[-10%] w-[800px] h-[800px] bg-[radial-gradient(circle_at_center,rgba(138,43,226,0.08),transparent_60%)] pointer-events-none"></div>
        <div class="absolute bottom-[-20%] left-[-10%] w-[600px] h-[600px] bg-[radial-gradient(circle_at_center,rgba(59,130,246,0.05),transparent_60%)] pointer-events-none"></div>
        
        <div class="relative h-full z-10">
          <RightSide />
        </div>
      </main>
    </div>
  </div>
</template>