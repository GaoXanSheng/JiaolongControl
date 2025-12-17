<script setup lang="ts">
import { ref, watch, onMounted } from 'vue'
import { Message } from '@arco-design/web-vue'
import WMIOperation from '@/utils/WMIOperation'

const loading = ref(false)
const color = ref({ red: 0, green: 0, blue: 0 })
const LightBrightness = ref(0)
const colorPicker = ref('#000000')

const rgbToHex = (r: number, g: number, b: number) =>
    `#${[r, g, b].map((x) => x.toString(16).padStart(2, '0')).join('')}`

const hexToRgb = (hex: string) => {
  const result = /^#?([a-f\d]{2})([a-f\d]{2})([a-f\d]{2})$/i.exec(hex)
  return result ? {
    red: parseInt(result[1], 16),
    green: parseInt(result[2], 16),
    blue: parseInt(result[3], 16)
  } : null
}

onMounted(async () => {
  const [colorData, brightness] = await Promise.all([
    WMIOperation.Keyboard.Color.Get(),
    WMIOperation.Keyboard.LightBrightness.Get()
  ])
  color.value = colorData
  LightBrightness.value = brightness
  colorPicker.value = rgbToHex(color.value.red, color.value.green, color.value.blue)
})

watch(color, (val) => colorPicker.value = rgbToHex(val.red, val.green, val.blue), { deep: true })
watch(colorPicker, (val) => {
  const rgb = hexToRgb(val)
  if (rgb) Object.assign(color.value, rgb)
})

async function handleClick() {
  loading.value = true
  try {
    await Promise.all([
      WMIOperation.Keyboard.Color.Set(color.value.red, color.value.green, color.value.blue),
      WMIOperation.Keyboard.LightBrightness.Set(LightBrightness.value)
    ])
    Message.success('应用成功')
  } catch {
    Message.error('应用失败')
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="keyboard-settings">
    <a-row justify="center" :gutter="[0, 20]">
      <a-col :span="20">
        <h2 class="section-title">Keyboard Settings</h2>
      </a-col>

      <!-- 预览区域 -->
      <div class="preview-area" :style="{ backgroundColor: `rgb(${color.red}, ${color.green}, ${color.blue})` }">
        <div class="preview-content">
          <a-color-picker v-model="colorPicker" size="mini">
            <!-- 动态反色文本 -->
            <a-tag :color="colorPicker" class="picker-tag">
							<span :style="{ color: `rgb(${255 - color.blue}, ${255 - color.green}, ${255 - color.red})` }">
								Pick Color
							</span>
            </a-tag>
          </a-color-picker>
        </div>
      </div>

      <!-- 控件区域 -->
      <a-col v-for="c in ['red', 'green', 'blue']" :key="c" :span="20">
        <a-input-number v-model="color[c as keyof typeof color]" :min="0" :max="255" class="input-full">
          <template #prepend>{{ c }}</template>
        </a-input-number>
      </a-col>

      <a-col :span="20">
        <a-input-number v-model="LightBrightness" :min="0" :max="3" class="input-full">
          <template #prepend>Brightness (0-3)</template>
        </a-input-number>
      </a-col>

      <a-col :span="20">
        <a-button type="primary" long :loading="loading" @click="handleClick">Apply Changes</a-button>
      </a-col>
    </a-row>
  </div>
</template>

<style lang="scss" scoped>
.keyboard-settings {
  padding: 24px;
  max-width: 520px;
  margin: 0 auto;

  display: flex;
  flex-direction: column;
  gap: 20px;

  .section-title {
    font-size: 18px;
    font-weight: 600;
    color: var(--color-text-1);
    margin-bottom: 4px;
  }

  /* 颜色预览区 */
  .preview-area {
    position: relative;
    width: 100%;
    height: 160px;
    overflow: hidden;

    background-color: #000;
    transition: background-color 0.25s ease;
    display: flex;
    align-items: center;
    justify-content: center;


    .preview-content {
      position: relative;
      z-index: 1;
      display: flex;
      align-items: center;
      justify-content: center;
    }

    .picker-tag {
      padding: 6px 14px;
      font-size: 13px;
      font-weight: 500;
      cursor: pointer;

      backdrop-filter: blur(8px);
      background: rgba(255, 255, 255, 0.15);
      border: 1px solid rgba(255, 255, 255, 0.25);

      transition: all 0.2s ease;
    }
  }
}

</style>