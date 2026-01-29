<script async setup lang="ts">
import {ref} from 'vue'
import {Message} from '@arco-design/web-vue'
import {Config, CPU} from '@/utils/bridge.ts'

const loading = ref(false)
const config = await Config.GetConfig()
const CPUData = ref(config.AdvancedCPUSystemConfig)

async function handleClick() {
  loading.value = true
  const result = [
    await CPU.SetCpuLongPower(CPUData.value.CpuLongPower),
    await CPU.SetCpuShortPower(CPUData.value.CpuShortPower),
    await CPU.SetCPUTempWall(CPUData.value.CpuTempWall),
  ]
  result.map((item) => {
    if (item) {
      Message.success('应用成功')
    } else {
      Message.error('应用失败')
    }
  })
  config.AdvancedCPUSystemConfig = CPUData.value;
  await Config.SetConfig(config)
  loading.value = false
}
</script>

<template>
  <div class="CPU">
    <a-row justify="center">
      <a-col :span="16">
        <a-typography-title class="title"> CPU Settings</a-typography-title>
      </a-col>
      <a-col :span="16" class="item">
        <a-input-number
            v-model="CPUData.CpuShortPower"
            placeholder="ShortPower"
            :min="30"
            :max="255"
            model-event="input"
        >
          <template #append> 短时CPU功耗</template>
        </a-input-number>
      </a-col>
      <a-col :span="16" class="item">
        <a-input-number
            v-model="CPUData.CpuLongPower"
            placeholder="LongPower"
            :min="30"
            :max="255"
            model-event="input"
        >
          <template #append> 长时CPU功耗</template>
        </a-input-number>
      </a-col>
      <a-space direction="vertical" size="large"></a-space>
      <a-col :span="16" class="item">
        <a-input-number
            v-model="CPUData.CpuTempWall"
            placeholder="TempWall"
            :min="1"
            :max="100"
            model-event="input"
        >
          <template #append> 温度墙</template>
        </a-input-number>
      </a-col>
      <a-col :span="16" class="item">
        <a-button type="primary" :loading="loading" @click="handleClick">确认</a-button>
      </a-col>
    </a-row>
  </div>
</template>

<style lang="scss" scoped>
.CPU {
  padding-top: 20px;

  .title {
    text-align: left;
  }

  .item {
    margin-top: 10px;
  }
}
</style>
