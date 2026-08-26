<script setup lang="ts">
import RightSide from '@/components/layout/RightSide.vue'
import TitleBar from '@/components/layout/TitleBar.vue'
import useStore, { HomeCardType } from '@/stores'

const store = useStore()

function onClickMenuItem(key: number) {
  store.setPage(key)
}

// 过滤掉 eum 为 8 的“设置”选项，主导航菜单中仅渲染除“设置”之外的其他项
const mainNavItems = HomeCardType.filter((item) => item.eum !== 8)
</script>

<template>
  <div class="flex flex-col h-screen text-white overflow-hidden select-none">
    <!-- 极简标题栏 -->
    <TitleBar class="z-50 bg-transparent" />

    <div class="flex flex-1 overflow-hidden">
      <!-- 侧边栏 (严格对标图左) -->
      <aside class="w-[240px] flex flex-col shadow-10 mt-6">
        <div class="flex-1 h-10 px-4 flex flex-col justify-between overflow-y-auto no-scrollbar">
          <!-- 主导航：这里改用过滤后的 mainNavItems 数组 -->
          <nav class="space-y-1.5">
            <button
              v-for="item in mainNavItems"
              :key="item.eum"
              :class="[
                'w-full flex items-center gap-4 px-5 py-3.5 rounded-[5px] transition-all duration-300',
                store.SwitchPages === item.eum
                  ? 'nav-item-active'
                  : 'hover:bg-white/[0.03] text-gray-400 hover:text-gray-1000',
              ]"
              @click="onClickMenuItem(Number(item.eum))"
            >
              <!-- 图标容器 -->
              <div class="w-5 h-5 flex items-center justify-center relative">
                <!-- 1. 背景氛围炫光 -->
                <span
                  v-if="store.SwitchPages === item.eum"
                  class="absolute w-5 h-5 bg-blue-500/40 rounded-full blur-[8px] animate-pulse pointer-events-none"
                ></span>

                <!-- 2. 图标本身 -->
                <img
                  :src="item.icon"
                  :class="[
                    'relative z-10 w-full h-full brightness-0 transition-all duration-300',
                    store.SwitchPages === item.eum
                      ? 'invert opacity-100 drop-shadow-[0_0_6px_rgba(59,130,246,0.9)]'
                      : 'invert opacity-60 group-hover:opacity-100',
                  ]"
                  alt="icon"
                />
              </div>
              <span class="font-medium text-[13px] tracking-wide">{{ item.title }}</span>
            </button>
          </nav>

          <!-- 底部工具/设置：依然保持独立渲染，触发 eum 为 8 的事件 -->
          <div class="pb-6">
            <button
              :class="[
                'w-full flex items-center justify-between px-5 py-4 transition-colors',
                store.SwitchPages === 8 ? 'text-white' : 'text-gray-400 hover:text-white',
              ]"
              @click="onClickMenuItem(8)"
            >
              <div class="flex items-center gap-4">
                <div class="w-5 h-5 flex items-center justify-center relative">
                  <!-- 设置图标背景氛围炫光 -->
                  <span
                    v-if="store.SwitchPages === 8"
                    class="absolute w-5 h-5 bg-blue-500/40 rounded-full blur-[8px] animate-pulse pointer-events-none"
                  ></span>

                  <!-- 设置图标轮廓发光 -->
                  <icon-settings
                    :class="[
                      'text-lg relative z-10 transition-all duration-300',
                      store.SwitchPages === 8
                        ? 'text-blue-400 drop-shadow-[0_0_6px_rgba(59,130,246,0.9)]'
                        : '',
                    ]"
                  />
                </div>
                <span class="font-medium text-[13px] tracking-wide">设置</span>
              </div>
              <icon-right />
            </button>
          </div>
        </div>
      </aside>

      <!-- 主内容区 -->
      <main class="flex-1 relative overflow-hidden">
        <!-- 主内容背景发光点缀 -->
        <div
          class="absolute top-[-20%] right-[-10%] w-[800px] h-[800px] bg-[radial-gradient(circle_at_center,rgba(138,43,226,0.08),transparent_60%)] pointer-events-none"
        ></div>
        <div
          class="absolute bottom-[-20%] left-[-10%] w-[600px] h-[600px] bg-[radial-gradient(circle_at_center,rgba(59,130,246,0.05),transparent_60%)] pointer-events-none"
        ></div>

        <div class="relative h-full z-10">
          <RightSide />
        </div>
      </main>
    </div>
  </div>
</template>
