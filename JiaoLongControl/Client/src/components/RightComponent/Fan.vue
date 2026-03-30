<script async setup lang="ts">
import {onMounted, ref} from 'vue'
import {Message} from '@arco-design/web-vue'
import {AutoFanControl, Config, Fan} from '@/utils/bridge'
import FanCurveEditor from "@/components/ProSettingComponent/FanCurve/FanCurveEditor.vue";
import FanSpeed from "@/components/ProSettingComponent/FanCurve/FanSpeed.vue";

const loading = ref(false)
const visible = ref(false);
const config = await Config.GetConfig()
const FanPageStore = ref(config.Data.FanPageStore)

const handleClick = () => {
  if (FanPageStore.value.FanSpeed > 5800 || FanPageStore.value.FanSpeed < 1500) {
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
  const res = await Fan.SetFanSpeed(FanPageStore.value.FanSpeed)
  res.Success ? Message.success(res.Message) : Message.error(res.Message)
  config.Data.FanPageStore = FanPageStore.value;
  await Config.SetConfig(config.Data)
  loading.value = false
};
const handleCancel = () => {
  visible.value = false;
}
const EnableAdvancedFanControlSystem = ref(false)
onMounted(async () => {
  const config = (await Config.GetConfig()).Data
  EnableAdvancedFanControlSystem.value = config.AdvancedFanControlSystem
})

async function handleRemoveFanClick() {
  const res = await Fan.RemoveFanSpeed()
  res.Success ? Message.success(res.Message) : Message.error(res.Message)
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
            v-model="FanPageStore.FanSpeed"
            :min="0"
            :max="8000"
            :step="100"
            size="large"
            class="full-width"
        >
          <template #prepend>目标转速</template>
          <template #suffix>RPM</template>
        </a-input-number>
      </a-col>
      <a-col :span="16">
        <a-button type="primary" long :loading="loading" @click="handleClick">设定转速</a-button>
        <a-modal v-model:visible="visible" @ok="handleOk" @cancel="handleCancel">
          <template #title>
            警告
          </template>
          <div>任何大于5800或小于1500转速的应用都会导致系统异常，设置对应转速不代表有足够功率可以跑上去</div>
        </a-modal>
      </a-col>
      <a-col :span="16">
        <a-button type="primary" long @click="handleRemoveFanClick">移除转速设置</a-button>
      </a-col>
    </a-row>
  </div>
  <fan-curve-editor v-else></fan-curve-editor>
  <FanSpeed></FanSpeed>
</template>

<style lang="scss" scoped>
.fan-settings {
  padding: 24px;

  .full-width {
    width: 100%;
  }
}
</style>