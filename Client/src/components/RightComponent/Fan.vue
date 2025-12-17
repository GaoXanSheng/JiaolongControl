<script setup lang="ts">
import { ref } from 'vue'
import { Message } from '@arco-design/web-vue'
import useStore from '@/stores'
import WMIOperation from '@/utils/WMIOperation'

const store = useStore()
const loading = ref(false)

async function handleClick() {
  loading.value = true
  try {
    // 先开启最大转速开关(手动模式)
    await WMIOperation.Fan.SetMaxFanSpeedSwitch(true)
    const msg = await WMIOperation.Fan.SetFanSpeed(store.FanSpeed)

    if (msg === 'Fan Speed Set OK') {
      Message.success('设置成功')
    } else {
      Message.error(msg || '设置失败')
    }
  } catch (e: any) {
    Message.error(e.message || '通信错误')
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="fan-settings">
    <a-row justify="center" :gutter="[0, 20]">
      <a-col :span="20">
        <h2 class="title">Fan Control (Manual)</h2>
      </a-col>
      <a-col :span="20">
        <a-input-number
            v-model="store.FanSpeed"
            :min="1500"
            :max="5800"
            :step="100"
            size="large"
            class="full-width"
        >
          <template #prepend>RPM Target</template>
          <template #suffix>RPM</template>
        </a-input-number>
      </a-col>
      <a-col :span="20">
        <a-button type="primary" long :loading="loading" @click="handleClick">Apply Speed</a-button>
      </a-col>
    </a-row>
  </div>
</template>

<style lang="scss" scoped>
.fan-settings {
  padding: 24px;
  .title {
    text-align: left;
    color: var(--color-text-1);
  }
  .full-width {
    width: 100%;
  }
}
</style>