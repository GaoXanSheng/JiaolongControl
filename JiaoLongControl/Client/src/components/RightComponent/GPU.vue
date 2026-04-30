<template>
  <a-layout class="layout-Content">
    <a-layout>
      <a-layout-header style="padding-left: 20px;">
        <a-col :span="16">
          <a-typography-title class="title">GPU 设置</a-typography-title>
        </a-col>
      </a-layout-header>
      <a-layout style="padding: 0 30px;">
        <a-layout-content>
          <a-row justify="center" :gutter="[0, 20]">
            <GPUDirectConnection></GPUDirectConnection>
            <GPUUnlockDB></GPUUnlockDB>
            <a-col :span="16" class="item">
              <div class="slider-wrapper">
                <span class="slider-label">锁定GPU时钟</span>
                <a-slider
                    :min="0"
                    :max="3000"
                    step="1"
                    show-input
                    v-model="GPUData.GpuClock"
                    class="custom-slider"
                />
                <a-button type="primary" :loading="loading" @click="RemoveGPUClock">清除</a-button>
                <a-button type="primary" :loading="loading" @click="SetGPUClock">应用</a-button>
              </div>
            </a-col>
            <a-col :span="16" class="item">
              <div class="slider-wrapper">
                <span class="slider-label">锁定GPU内存</span>
                <a-slider
                    :min="0"
                    :max="10000"
                    step="1"
                    show-input
                    v-model="GPUData.MemoryClock"
                    class="custom-slider"
                />
                <a-button type="primary" :loading="loading" @click="RemoveGPUMemoryClock">清除</a-button>
                <a-button type="primary" :loading="loading" @click="SetGPUMemoryClock">应用</a-button>
              </div>
            </a-col>
            <a-col :span="16" class="item">
              <div class="slider-wrapper">
                <span class="slider-label">设置GPU功率</span>
                <a-slider
                    :min="0"
                    :max="140"
                    step="1"
                    show-input
                    v-model="GPUData.PowerLimit"
                    class="custom-slider"
                />
                <a-button type="primary" :loading="loading" @click="SetGPUPower">应用</a-button>
              </div>
            </a-col>
          </a-row>
        </a-layout-content>
      </a-layout>
    </a-layout>
  </a-layout>
</template>
<script async lang="ts" setup>
import GPUDirectConnection from "@/components/RightComponent/setting/GPUDirectConnection.vue";
import {ref} from "vue";
import {Config, NvidiaGpu} from "@/utils/bridge.ts";
import {Message} from "@arco-design/web-vue";
import GPUUnlockDB from "@/components/RightComponent/setting/GPUUnlockDB.vue";

const config = await Config.GetConfig()
const loading = ref(false)
const GPUData = ref(config.Data.NvidiaGpuConfig)

async function saveConfig() {
  config.Data.NvidiaGpuConfig = GPUData.value;
  await Config.SetConfig(config.Data)
}

async function SetGPUClock() {
  loading.value = true
  const res = await NvidiaGpu.LockGpuClock(GPUData.value.GpuClock)
  res.Success ? Message.success(res.Message) : Message.error(res.Message)
  await saveConfig()
  loading.value = false

}

async function RemoveGPUClock() {
  loading.value = true
  const res = await NvidiaGpu.ResetGpuClock()
  res.Success ? Message.success(res.Message) : Message.error(res.Message)
  await saveConfig()
  loading.value = false
}

async function SetGPUMemoryClock() {
  loading.value = true
  const res = await NvidiaGpu.LockMemoryClock(GPUData.value.MemoryClock)
  res.Success ? Message.success(res.Message) : Message.error(res.Message)
  await saveConfig()
  loading.value = false
}

async function RemoveGPUMemoryClock() {
  loading.value = true
  const res = await NvidiaGpu.ResetMemoryClock()
  res.Success ? Message.success(res.Message) : Message.error(res.Message)
  await saveConfig()
  loading.value = false
}

async function SetGPUPower() {
  loading.value = true
  const res = await NvidiaGpu.SetPowerLimit(GPUData.value.PowerLimit)
  res.Success ? Message.success(res.Message) : Message.error(res.Message)
  await saveConfig()
  loading.value = false
}

</script>
<style lang="scss" scoped>

</style>
