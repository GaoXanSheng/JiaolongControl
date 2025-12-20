<script setup lang="ts">
import {onMounted, ref} from 'vue'
import {Message} from '@arco-design/web-vue'
import useStore from '@/stores'
import {Config, Fan} from '@/utils/bridge'
import FanCurveEditor from "@/components/ProSettingComponent/FanCurve/FanCurveEditor.vue";

const store = useStore()
const loading = ref(false)

async function handleClick() {
  loading.value = true
  await Fan.SetMaxFanSpeedSwitch(true)
  const res = await Fan.SetFanSpeed(store.FanSpeed)
  if (res) {
    Message.success('设置成功')
  } else {
    Message.error('设置失败')
  }
  loading.value = false
}
const EnableAdvancedFanControlSystem = ref(false)
onMounted(async () => {
  const config = await Config.GetConfig()
  EnableAdvancedFanControlSystem.value = config.EnableAdvancedFanControlSystem
})
</script>

<template>
  <div class="fan-settings" v-if="!EnableAdvancedFanControlSystem">
    <a-row justify="center" :gutter="[0, 20]">
      <a-col :span="16">
        <a-typography-title class="title">风扇设置</a-typography-title>
      </a-col>
      <a-col :span="16">
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
      <a-col :span="16">
        <a-button type="primary" long :loading="loading" @click="handleClick">Apply Speed</a-button>
      </a-col>
    </a-row>
  </div>
  <fan-curve-editor v-else></fan-curve-editor>
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