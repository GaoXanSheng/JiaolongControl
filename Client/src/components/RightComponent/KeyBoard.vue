<script async setup lang="ts">
import { ref, watch } from 'vue'
import { Message } from '@arco-design/web-vue'
import {Keyboard} from "@/utils/bridge.ts";

const loading = ref(false)

const color = ref({ red: 0, green: 0, blue: 0 })
const LightBrightness = ref(0)
const colorPicker = ref('#000000')

async function loadInitialData() {
  const [colorData, brightness] = await Promise.all([
    Keyboard.GetColor(),
    Keyboard.GetLightBrightness()
  ])
  color.value = { ...colorData }
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
        red: parseInt(result[1], 16),
        green: parseInt(result[2], 16),
        blue: parseInt(result[3], 16)
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
        <a-typography-title class="title">KeyBoard Settings</a-typography-title>
      </a-col>

      <div
          class="keys"
          :style="{ backgroundColor: `rgb(${color.red}, ${color.green}, ${color.blue})` }"
      >
        <div class="Preview">
          <a-color-picker v-model="colorPicker" size="mini">
            <a-tag :color="colorPicker">
              <p
                  :style="{
									color: `rgb(${255 - color.blue}, ${255 - color.green}, ${255 - color.red})`
								}"
              >
                Preview
              </p>
            </a-tag>
          </a-color-picker>
        </div>
      </div>

      <a-col v-for="c in ['red', 'green', 'blue']" :key="c" :span="16" class="item">
        <a-input-number v-model="color[c]" :min="0" :max="255" :placeholder="c" model-event="input">
          <template #append>{{ c }}</template>
        </a-input-number>
      </a-col>

      <a-col :span="16" class="item">
        <a-input-number
            v-model="LightBrightness"
            :min="0"
            :max="3"
            placeholder="LightBrightness"
            model-event="input"
        >
          <template #append>LightBrightness</template>
        </a-input-number>
      </a-col>

      <a-col :span="16" class="item">
        <a-button type="primary" :loading="loading" @click="handleClick">确认</a-button>
      </a-col>
    </a-row>
  </div>
</template>

<style lang="scss" scoped>
.Keyboard {
  padding-top: 20px;

  .title {
    text-align: left;
  }

  .keys {
    width: 600px;
    height: 200px;

    .Preview {
      display: flex;
      justify-content: center;
      align-items: center;
      height: 100%;
      text-align: center;
    }
  }
  .item {
    margin-top: 10px;
  }
}
</style>