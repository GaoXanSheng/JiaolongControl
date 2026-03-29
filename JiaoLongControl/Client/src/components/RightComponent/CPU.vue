<script async setup lang="ts">
import {ref} from 'vue'
import {Message} from '@arco-design/web-vue'
import {Config, CPU, Power} from '@/utils/bridge.ts'

const loading = ref(false)
const config = await Config.GetConfig()
const CPUData = ref(config.Data.AdvancedCPUSystemConfig)


async function saveConfig() {
  config.Data.AdvancedCPUSystemConfig = CPUData.value;
  await Config.SetConfig(config.Data)
}

async function SetCpuLongPower() {
  loading.value = true
  const res = await CPU.SetCpuLongPower(CPUData.value.CpuLongPower)
  res.Success ? Message.success(res.Message) : Message.error(res.Message)
  await saveConfig()
  loading.value = false
}

async function SetCPUTempWall() {
  loading.value = true
  const res = await CPU.SetCPUTempWall(CPUData.value.CpuTempWall)
  res.Success ? Message.success(res.Message) : Message.error(res.Message)
  await saveConfig()
  loading.value = false
}

async function SetCpuShortPower() {
  loading.value = true
  const res = await CPU.SetCpuShortPower(CPUData.value.CpuShortPower)
  res.Success ? Message.success(res.Message) : Message.error(res.Message)
  await saveConfig()
  loading.value = false
}

async function SetCPUMaxFrequency() {
  loading.value = true
  const res = await Power.SetCPUMaxFrequency(CPUData.value.CPUMaxFrequency)
  res.Success ? Message.success(res.Message) : Message.error(res.Message)
  await saveConfig()
  loading.value = false
}

// async function SetCPUMaxState() {
//   loading.value = true
//   const res = await Power.SetCPUMaxState(CPUData.value.CPUMaxState)
//   res.Success ? Message.success(res.Message) : Message.error(res.Message)
//   await saveConfig()
//   loading.value = false
// }

async function SetCPUTurbo() {
  loading.value = true
  if (!CPUData.value.CPUTurbo) {
    const res = await Power.DisableTurbo()
    CPUData.value.CPUTurbo = false
    res.Success ? Message.success(res.Message) : Message.error(res.Message)
  }else {
    const res = await Power.EnableTurbo()
    CPUData.value.CPUTurbo = true
    res.Success ? Message.success(res.Message) : Message.error(res.Message)
  }
  await saveConfig()
  loading.value = false
}
</script>

<template>
  <a-layout class="layout-Content">
    <a-layout>
      <a-layout-header style="padding-left: 20px;">
        <a-col :span="16">
          <a-typography-title class="title"> CPU 设置</a-typography-title>
        </a-col>
      </a-layout-header>
      <a-layout style="padding: 0 30px;">
        <a-layout-content>
          <a-row justify="center" :gutter="[0, 20]">
            <a-col :span="16" class="item">
              <div class="slider-wrapper">
                <span class="slider-label">短时CPU功耗</span>
                <a-slider
                    :min="30"
                    :max="255"
                    step="1"
                    show-ticks
                    show-input
                    v-model="CPUData.CpuShortPower"
                    class="custom-slider"
                />
                <a-button type="primary" :loading="loading" @click="SetCpuShortPower">应用</a-button>
              </div>
            </a-col>
            <a-col :span="16" class="item">
              <div class="slider-wrapper">
                <span class="slider-label">长时CPU功耗</span>
                <a-slider
                    :min="30"
                    :max="255"
                    step="1"
                    show-ticks
                    show-input
                    v-model="CPUData.CpuLongPower"
                    class="custom-slider"
                />
                <a-button type="primary" :loading="loading" @click="SetCpuLongPower">应用</a-button>
              </div>
            </a-col>
            <a-col :span="16" class="item">
              <div class="slider-wrapper">
                <span class="slider-label">温度墙</span>
                <a-slider
                    :min="1"
                    :max="100"
                    step="1"
                    show-ticks
                    show-input
                    v-model="CPUData.CpuTempWall"
                    class="custom-slider"
                />
                <a-button type="primary" :loading="loading" @click="SetCPUTempWall">应用</a-button>
              </div>
            </a-col>
            <a-col :span="16" class="item">
              <div class="slider-wrapper">
                <span class="slider-label">最大CPU频率</span>
                <a-slider
                    :min="0"
                    :max="5400"
                    step="100"
                    show-ticks
                    show-input
                    class="custom-slider"
                    v-model="CPUData.CPUMaxFrequency"
                />
                <a-button type="primary" :loading="loading" @click="SetCPUMaxFrequency">应用</a-button>
              </div>
            </a-col>
<!--            <a-col :span="16" class="item">-->
<!--              <div class="slider-wrapper">-->
<!--                <span class="slider-label">最大CPU状态</span>-->
<!--                <a-slider-->
<!--                    :min="0"-->
<!--                    :max="100"-->
<!--                    step="1"-->
<!--                    show-ticks-->
<!--                    show-input-->
<!--                    class="custom-slider"-->
<!--                    v-model="CPUData.CPUMaxState"-->
<!--                />-->
<!--                <a-button type="primary" :loading="loading" @click="SetCPUMaxState">应用</a-button>-->
<!--              </div>-->
<!--            </a-col>-->
            <a-col :span="16" class="item">
              <div class="slider-wrapper">
                <span class="slider-label">CPU 睿频</span>
                <a-switch v-model="CPUData.CPUTurbo" :loading="loading" @click="SetCPUTurbo">
                  <template #checked-icon>
                    <icon-check/>
                  </template>
                  <template #unchecked-icon>
                    <icon-close/>
                  </template>
                </a-switch>
              </div>
            </a-col>
          </a-row>
        </a-layout-content>
      </a-layout>
    </a-layout>
  </a-layout>
</template>
<style lang="scss" scoped>
</style>
