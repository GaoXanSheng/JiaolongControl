<script setup lang="ts">
import { ref, watch } from 'vue'
import { Message } from '@arco-design/web-vue'
import { Keyboard } from '@/utils/bridge.ts'

const loading = ref(false)

const color = ref({ red: 0, green: 0, blue: 0 })
const LightBrightness = ref(0)
const colorPicker = ref('#8A2BE2')

// 快捷配色预设
const colorPresets = [
  { name: '炫彩紫', hex: '#8A2BE2', r: 138, g: 43, b: 226 },
  { name: '冰晶蓝', hex: '#00F0FF', r: 0, g: 240, b: 255 },
  { name: '极光绿', hex: '#00FF66', r: 0, g: 255, b: 102 },
  { name: '烈焰红', hex: '#FF3366', r: 255, g: 51, b: 102 },
  { name: '暖阳黄', hex: '#FFCC00', r: 255, g: 204, b: 0 },
  { name: '纯净白', hex: '#FFFFFF', r: 255, g: 255, b: 255 },
]

async function loadInitialData() {
  try {
    const colorRes = await Keyboard.GetColor()
    const brightnessRes = await Keyboard.GetLightBrightness()
    if (colorRes?.Success && colorRes.Data) {
      color.value = { ...colorRes.Data }
      colorPicker.value = rgbToHex(color.value.red, color.value.green, color.value.blue)
    }
    if (brightnessRes?.Success && brightnessRes.Data !== undefined) {
      LightBrightness.value = brightnessRes.Data
    }
  } catch (e) {
    console.error('Failed to load keyboard settings', e)
  }
}

await loadInitialData()

function rgbToHex(r: number, g: number, b: number) {
  return `#${[r, g, b].map((x) => Math.max(0, Math.min(255, x)).toString(16).padStart(2, '0')).join('')}`
}

function hexToRgb(hex: string) {
  const result = /^#?([a-f\d]{2})([a-f\d]{2})([a-f\d]{2})$/i.exec(hex)
  return result
    ? {
        red: parseInt(result[1]!, 16),
        green: parseInt(result[2]!, 16),
        blue: parseInt(result[3]!, 16),
      }
    : null
}

watch(
  color,
  (val) => {
    colorPicker.value = rgbToHex(val.red, val.green, val.blue)
  },
  { deep: true },
)

watch(colorPicker, (val) => {
  const rgb = hexToRgb(val)
  if (rgb) Object.assign(color.value, rgb)
})

function applyPreset(preset: (typeof colorPresets)[0]) {
  color.value = { red: preset.r, green: preset.g, blue: preset.b }
}

async function handleApply() {
  loading.value = true
  try {
    const [colorRes, brightnessRes] = await Promise.all([
      Keyboard.SetColor(color.value.red, color.value.green, color.value.blue),
      Keyboard.SetLightBrightness(LightBrightness.value),
    ])

    if (colorRes && brightnessRes) {
      Message.success('键盘灯效设置已应用')
    } else {
      Message.error('应用设置失败')
    }
  } catch {
    Message.error('应用设置异常')
  } finally {
    loading.value = false
  }
}

function handleReset() {
  color.value = { red: 138, green: 43, blue: 226 }
  LightBrightness.value = 2
  Message.info('已恢复默认背光设置')
}
</script>

<template>
  <div class="h-full overflow-y-auto text-white p-6 no-scrollbar">
    <div class="max-w-[1300px] mx-auto flex flex-col lg:flex-row gap-6">
      <!-- ==================== 左侧：键盘控制区 ==================== -->
      <div class="flex-1 space-y-6">
        <!-- 头部标题 -->
        <div>
          <h1 class="text-2xl font-bold tracking-wide">键盘 RGB 灯效</h1>
          <p class="text-[13px] text-gray-500 mt-1">自定义 RGB 背光颜色与灯光亮度。</p>
        </div>

        <!-- 1. 键盘灯效可视化预览卡片 -->
        <div
          class="bg-[#121320]/60 backdrop-blur-md border border-white/[0.05] rounded-xl p-5 shadow-lg"
        >
          <div class="flex justify-between items-center mb-4">
            <h2 class="text-[13px] font-semibold text-gray-300">灯效实时预览</h2>
            <div class="flex items-center gap-2">
              <span class="text-xs text-gray-400">颜色拾取器:</span>
              <a-color-picker v-model="colorPicker" size="mini">
                <div
                  class="w-6 h-6 rounded-md border border-white/20 cursor-pointer shadow-sm transition-transform hover:scale-105"
                  :style="{ backgroundColor: colorPicker }"
                ></div>
              </a-color-picker>
            </div>
          </div>

          <!-- 模拟键盘面板 -->
          <div class="w-full flex justify-center py-4">
            <div
              class="relative w-full max-w-[640px] h-[190px] bg-[#12131e] rounded-xl p-3.5 border border-white/10 overflow-hidden transition-all duration-300"
              :style="{
                boxShadow: `0 10px 30px rgba(0, 0, 0, 0.6), 0 0 ${LightBrightness * 12}px rgba(${color.red}, ${color.green}, ${color.blue}, ${LightBrightness * 0.25})`,
              }"
            >
              <!-- 灯光溢出画幅 -->
              <div
                class="absolute inset-0 pointer-events-none transition-all duration-300"
                :style="{
                  background: `radial-gradient(circle at center, rgba(${color.red}, ${color.green}, ${color.blue}, ${LightBrightness * 0.2}) 0%, transparent 85%)`,
                }"
              ></div>

              <!-- 按键矩阵线稿 -->
              <div class="grid grid-cols-13 grid-rows-4 gap-1.5 h-full relative z-10">
                <div
                  v-for="i in 52"
                  :key="i"
                  class="bg-[#1a1b2b]/90 border border-white/[0.06] rounded flex items-center justify-center relative overflow-hidden transition-all duration-300"
                >
                  <div
                    class="absolute inset-0 opacity-40 blur-[3px] transition-all duration-300"
                    :style="{
                      backgroundColor: `rgb(${color.red}, ${color.green}, ${color.blue})`,
                      opacity: LightBrightness > 0 ? (LightBrightness / 3) * 0.6 : 0,
                    }"
                  ></div>
                  <span class="w-1.5 h-1.5 rounded-full bg-white/20"></span>
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- 2. 快捷配色预设 -->
        <div
          class="bg-[#121320]/60 backdrop-blur-md border border-white/[0.05] rounded-xl p-5 shadow-lg"
        >
          <h2 class="text-[13px] font-semibold text-gray-300 mb-3">快捷预设</h2>
          <div class="grid grid-cols-3 sm:grid-cols-6 gap-3">
            <div
              v-for="preset in colorPresets"
              :key="preset.name"
              class="border border-white/[0.05] hover:border-white/20 bg-[#121320] hover:bg-[#1a182f] rounded-xl p-3 cursor-pointer transition-all duration-300 flex flex-col items-center gap-2 group"
              @click="applyPreset(preset)"
            >
              <div
                class="w-8 h-8 rounded-full border border-white/20 shadow-md group-hover:scale-110 transition-transform"
                :style="{ backgroundColor: preset.hex, boxShadow: `0 0 10px ${preset.hex}66` }"
              ></div>
              <span class="text-xs text-gray-300 group-hover:text-white font-medium">{{
                preset.name
              }}</span>
            </div>
          </div>
        </div>

        <!-- 3. 灯光通道与亮度手动调节 -->
        <div
          class="bg-[#121320]/60 backdrop-blur-md border border-white/[0.05] rounded-xl p-5 shadow-lg space-y-6"
        >
          <h2 class="text-[13px] font-semibold text-gray-300">RGB 通道与亮度</h2>

          <div class="space-y-5">
            <!-- 红色通道 (Red) -->
            <div class="space-y-2">
              <div class="flex justify-between items-center text-xs">
                <span class="text-gray-300 flex items-center gap-1 font-semibold text-red-400"
                  >红色通道 (R)</span
                >
                <span class="text-red-400 font-medium font-mono">{{ color.red }}</span>
              </div>
              <a-slider v-model="color.red" :min="0" :max="255" class="w-full red-slider" />
            </div>

            <!-- 绿色通道 (Green) -->
            <div class="space-y-2">
              <div class="flex justify-between items-center text-xs">
                <span class="text-gray-300 flex items-center gap-1 font-semibold text-green-400"
                  >绿色通道 (G)</span
                >
                <span class="text-green-400 font-medium font-mono">{{ color.green }}</span>
              </div>
              <a-slider v-model="color.green" :min="0" :max="255" class="w-full green-slider" />
            </div>

            <!-- 蓝色通道 (Blue) -->
            <div class="space-y-2">
              <div class="flex justify-between items-center text-xs">
                <span class="text-gray-300 flex items-center gap-1 font-semibold text-blue-400"
                  >蓝色通道 (B)</span
                >
                <span class="text-blue-400 font-medium font-mono">{{ color.blue }}</span>
              </div>
              <a-slider v-model="color.blue" :min="0" :max="255" class="w-full blue-slider" />
            </div>

            <!-- 背光亮度 -->
            <div class="space-y-2 pt-2 border-t border-white/[0.05]">
              <div class="flex justify-between items-center text-xs">
                <span class="text-gray-300 flex items-center gap-1">背光亮度等级</span>
                <span class="text-purple-400 font-medium font-mono"
                  >Level {{ LightBrightness }}</span
                >
              </div>
              <a-slider v-model="LightBrightness" :min="0" :max="3" :step="1" class="w-full" />
            </div>
          </div>
        </div>

        <!-- 4. 底部动作栏 -->
        <div class="flex justify-between items-center pt-2">
          <button
            class="flex items-center gap-2 text-xs text-gray-400 hover:text-white border border-white/10 hover:border-white/20 bg-white/[0.02] hover:bg-white/[0.05] px-4 py-2 rounded-lg transition-colors"
            @click="handleReset"
          >
            <svg class="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path
                stroke-linecap="round"
                stroke-linejoin="round"
                stroke-width="2"
                d="M4 4v5h.582m15.356 2A8.001 8.001 0 1121.21 7.89M9 11l3-3 3 3m-3-3v12"
              />
            </svg>
            重置
          </button>

          <button
            :disabled="loading"
            class="text-xs font-medium text-white bg-gradient-to-r from-purple-700 to-indigo-600 hover:from-purple-600 hover:to-indigo-500 disabled:opacity-50 px-6 py-2 rounded-lg transition-all shadow-[0_0_15px_rgba(138,43,226,0.3)]"
            @click="handleApply"
          >
            {{ loading ? '应用中...' : '应用' }}
          </button>
        </div>
      </div>

      <!-- ==================== 右侧：信息与说明栏 ==================== -->
      <div class="w-full lg:w-[360px] shrink-0 space-y-6 lg:pt-[115px]">
        <!-- 1. 当前颜色色板卡片 -->
        <div
          class="bg-[#121320]/60 backdrop-blur-md border border-white/[0.05] rounded-xl p-5 shadow-lg space-y-4"
        >
          <h2 class="text-[13px] font-semibold text-gray-300">当前配色方案</h2>
          <div
            class="w-full h-24 rounded-xl border border-white/10 flex flex-col justify-end p-3 transition-all duration-300 shadow-lg relative overflow-hidden"
            :style="{ backgroundColor: `rgb(${color.red}, ${color.green}, ${color.blue})` }"
          >
            <div class="absolute inset-0 bg-black/20 backdrop-blur-[1px]"></div>
            <div
              class="relative z-10 flex justify-between items-center text-xs font-mono font-bold"
              :style="{ color: color.red + color.green + color.blue > 380 ? '#000' : '#fff' }"
            >
              <span>{{ rgbToHex(color.red, color.green, color.blue) }}</span>
              <span>RGB({{ color.red }}, {{ color.green }}, {{ color.blue }})</span>
            </div>
          </div>
          <div class="flex justify-between items-center text-xs text-gray-400">
            <span>当前亮度级别</span>
            <span class="text-white font-mono font-bold bg-white/10 px-2 py-0.5 rounded"
              >档位 {{ LightBrightness }}</span
            >
          </div>
        </div>

        <!-- 2. 说明卡片 -->
        <div
          class="bg-[#121320]/60 backdrop-blur-md border border-white/[0.05] rounded-xl p-5 shadow-lg space-y-2.5"
        >
          <h2 class="text-[13px] font-semibold text-gray-300">使用说明</h2>
          <div class="text-[11px] text-gray-500 leading-relaxed space-y-2">
            <p>通过 R/G/B 三通道滑块、快捷预设或颜色拾取器设置背光颜色。</p>
            <p>背光亮度设置为 0 时将关闭键盘灯光。</p>
            <p>点击“应用”即可生效并保存硬件状态。</p>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<style lang="scss" scoped>
.no-scrollbar::-webkit-scrollbar {
  display: none;
}
.no-scrollbar {
  -ms-overflow-style: none;
  scrollbar-width: none;
}



:deep(.red-slider .arco-slider-bar) {
  background: linear-gradient(90deg, #ef4444 0%, #f87171 100%) !important;
}
:deep(.red-slider .arco-slider-button) {
  border-color: #ef4444 !important;
  box-shadow: 0 0 10px rgba(239, 68, 68, 0.7) !important;
}

:deep(.green-slider .arco-slider-bar) {
  background: linear-gradient(90deg, #22c55e 0%, #4ade80 100%) !important;
}
:deep(.green-slider .arco-slider-button) {
  border-color: #22c55e !important;
  box-shadow: 0 0 10px rgba(34, 197, 94, 0.7) !important;
}

:deep(.blue-slider .arco-slider-bar) {
  background: linear-gradient(90deg, #3b82f6 0%, #60a5fa 100%) !important;
}
:deep(.blue-slider .arco-slider-button) {
  border-color: #3b82f6 !important;
  box-shadow: 0 0 10px rgba(59, 130, 246, 0.7) !important;
}
</style>
