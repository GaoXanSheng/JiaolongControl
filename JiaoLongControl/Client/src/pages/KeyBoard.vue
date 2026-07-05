<script setup lang="ts">
import {ref, watch} from 'vue'
import {Message} from '@arco-design/web-vue'
import {Keyboard} from "@/utils/bridge.ts";

const loading = ref(false)

const color = ref({red: 0, green: 0, blue: 0})
const LightBrightness = ref(0)
const colorPicker = ref('#000000')

async function loadInitialData() {
  const colorData = (await Keyboard.GetColor()).Data
  const brightness = (await Keyboard.GetLightBrightness()).Data
  color.value = {...colorData}
  LightBrightness.value = brightness
  colorPicker.value = rgbToHex(color.value.red, color.value.green, color.value.blue)
}

await loadInitialData()

function rgbToHex(r: number, g: number, b: number) {
  return `#${[r, g, b].map((x) => x.toString(16).padStart(2, '0')).join('')}`
}

function hexToRgb(hex: string) {
  const result = /^#?([a-f\d]{2})([a-f\d]{2})([a-f\d]{2})$/i.exec(hex)
  return result
      ? {
        red: parseInt(result[1]!, 16),
        green: parseInt(result[2]!, 16),
        blue: parseInt(result[3]!, 16)
      }
      : null
}

// 自动同步 colorPicker <-> color
watch(color, (val) => {
  colorPicker.value = rgbToHex(val.red, val.green, val.blue)
})

watch(colorPicker, (val) => {
  const rgb = hexToRgb(val)
  if (rgb) Object.assign(color.value, rgb)
})

async function handleClick() {
  loading.value = true
  const [colorRes, brightnessRes] = await Promise.all([
    Keyboard.SetColor(color.value.red, color.value.green, color.value.blue),
    Keyboard.SetLightBrightness(LightBrightness.value)
  ])

  if (colorRes && brightnessRes) {
    Message.success('应用成功')
  } else {
    Message.error('应用失败')
  }
  loading.value = false
}
</script>

<template>
  <div class="Keyboard">
    <a-row justify="center">
      <a-col :span="16">
        <a-typography-title class="title">键盘设置</a-typography-title>
      </a-col>

      <div class="keys-container">
        <!-- 模拟键盘外壳 -->
        <div
            class="keyboard-shell"
            :style="{
        '--kb-color': `rgb(${color.red}, ${color.green}, ${color.blue})`,
        '--kb-glow': `rgba(${color.red}, ${color.green}, ${color.blue}, ${LightBrightness / 3 * 0.5})`
      }"
        >
          <!-- 背景发光层 -->
          <div class="glow-layer"></div>

          <!-- 按键格子网格 -->
          <div class="key-grid">
            <div v-for="i in 52" :key="i" class="key-cap"></div>
          </div>

          <!-- 原有的 Color Picker 悬浮在键盘中央 -->
          <div class="Preview-overlay">
            <a-color-picker v-model="colorPicker" size="mini">
              <a-tag :color="colorPicker" class="picker-tag">
          <span :style="{ color: color.red + color.green + color.blue > 380 ? '#000' : '#fff' }">
            调整颜色
          </span>
              </a-tag>
            </a-color-picker>
          </div>
        </div>
      </div>
      <a-col v-for="c in ['red', 'green', 'blue'] as const" :key="c" :span="16" class="item">
        <div class="slider-wrapper">
          <span class="slider-label" :class="c">{{ c.toUpperCase() }}</span>
          <a-slider
              v-model="color[c]"
              :min="0"
              :max="255"
              show-input
              class="custom-slider"
          />
        </div>
      </a-col>
      <a-col :span="16" class="item">
        <div class="slider-wrapper">
          <span class="slider-label brightness">BRIGHTNESS</span>
          <a-slider
              v-model="LightBrightness"
              :min="0"
              :max="3"
              step="1"
              show-ticks
              class="custom-slider"
          />
        </div>
      </a-col>
      <a-col :span="16" class="item" style="text-align: center;">
        <a-button type="primary" :loading="loading" @click="handleClick">
          确认
        </a-button>
      </a-col>
    </a-row>
  </div>
</template>

<style lang="scss" scoped>
.Keyboard {
  padding-top: 20px;

  .keys-container {
    width: 100%;
    display: flex;
    justify-content: center;
    padding: 20px 0;

    .keyboard-shell {
      position: relative;
      width: 600px;
      height: 200px;
      background: #1a1a1a; // 键盘底座颜色
      border-radius: 12px;
      padding: 15px;
      border: 2px solid #333;
      overflow: hidden;
      box-shadow: 0 10px 30px rgba(0, 0, 0, 0.5), 0 0 20px var(--kb-glow); // 外部发光
      transition: all 0.3s ease;

      // 背景发光晕染

      .glow-layer {
        position: absolute;
        inset: 0;
        background: radial-gradient(circle at center, var(--kb-glow) 0%, transparent 80%);
        pointer-events: none;
      }

      // 按键网格

      .key-grid {
        display: grid;
        grid-template-columns: repeat(13, 1fr); // 模拟13列按键
        grid-template-rows: repeat(4, 1fr); // 模拟4行
        gap: 6px;
        height: 100%;
        opacity: 0.9;

        .key-cap {
          background: rgba(40, 40, 40, 0.8); // 半透明黑色键帽
          border-radius: 4px;
          border: 1px solid rgba(255, 255, 255, 0.05);
          position: relative;

          // 键帽底部的背光溢出效果

          &::after {
            content: '';
            position: absolute;
            inset: -2px;
            border-radius: 4px;
            background: var(--kb-color);
            filter: blur(4px);
            opacity: 0.3; // 灯光强度由亮度变量控制
            z-index: -1;
          }
        }
      }

      // 交互层：中间的颜色选择器

      .Preview-overlay {
        position: absolute;
        top: 50%;
        left: 50%;
        transform: translate(-50%, -50%);
        z-index: 10;

        .picker-tag {
          backdrop-filter: blur(10px);
          border: 1px solid rgba(255, 255, 255, 0.2);
          cursor: pointer;
          padding: 0 15px;
          height: 32px;
          line-height: 30px;
          font-weight: bold;
        }
      }
    }
  }



  // 针对 Arco Slider 的内部输入框样式微调（可选）

  :deep(.arco-slider-input) {
    background-color: var(--color-fill-2);
    border-radius: 4px;
  }
}
</style>