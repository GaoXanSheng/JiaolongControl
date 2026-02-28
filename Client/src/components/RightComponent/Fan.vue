<script setup lang="ts">
import {onMounted, ref} from 'vue'
import {Message} from '@arco-design/web-vue'
import useStore from '@/stores'
import {AutoFanControl, Config, Fan} from '@/utils/bridge'
import FanCurveEditor from "@/components/ProSettingComponent/FanCurve/FanCurveEditor.vue";
import FanSpeed from "@/components/ProSettingComponent/FanCurve/FanSpeed.vue";

const store = useStore()
const loading = ref(false)
const visible = ref(false);

const handleClick = () => {
  if (store.FanSpeed > 5800 || store.FanSpeed < 1500) {
    visible.value = true;
  } else {
    handleOk()
  }
};
const handleOk = async () => {
  visible.value = false;
  loading.value = true
  if (await AutoFanControl.IsRunning()) {
    await AutoFanControl.Stop()
  }
  await Fan.SetMaxFanSpeedSwitch(true)
  const res = await Fan.SetFanSpeed(store.FanSpeed)
  if (res) {
    Message.success('设置成功')
  } else {
    Message.error('设置失败')
  }
  loading.value = false
};
const handleCancel = () => {
  visible.value = false;
}
const EnableAdvancedFanControlSystem = ref(false)
onMounted(async () => {
  const config = await Config.GetConfig()
  EnableAdvancedFanControlSystem.value = config.AdvancedFanControlSystem
})

async function handleRemoveFanClick() {
  if (await Fan.RemoveFanSpeed()) {
    Message.success('设置成功')
  } else {
    Message.error('设置失败')
  }
}
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
            :min="0"
            :max="8000"
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
        <a-modal v-model:visible="visible" @ok="handleOk" @cancel="handleCancel">
          <template #title>
            警告
          </template>
          <div>任何大于5800或小于1500转速的应用都会导致系统异常，设置对应转速不代表有足够功率可以跑上去</div>
        </a-modal>
      </a-col>
      <a-col :span="16">
        <a-button type="primary" long @click="handleRemoveFanClick">Remove Speed</a-button>
      </a-col>
    </a-row>
  </div>
  <fan-curve-editor v-else></fan-curve-editor>
  <FanSpeed></FanSpeed>
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