<script setup lang="ts">
import { onMounted, ref, onUnmounted } from 'vue'
import type { HardwareMonitorInfo } from '@/stores/interfaces.ts'
// 引用组件 (保持原样引用，只需确保组件内部样式不冲突)
import CpuHeatMapData from '@/components/ProSettingComponent/CpuHeatMapData.vue'
import FanSpeedData from '@/components/ProSettingComponent/FanSpeedData.vue'
import GpuAmd from '@/components/ProSettingComponent/GpuAmd.vue'
import GpuNvidia from '@/components/ProSettingComponent/GpuNvidia.vue'
import CenterTop from '@/components/ProSettingComponent/CenterTop.vue'
import WMIOperation from '@/utils/WMIOperation'
import { Message } from '@arco-design/web-vue'

// 初始空数据，防止渲染报错
const MonitorInfo = ref<HardwareMonitorInfo | null>(null)
let timer: number | null = null

onMounted(async () => {
  // 初始加载
  try {
    MonitorInfo.value = await WMIOperation.GetHardwareMonitorInfo()
  } catch {
    Message.error('无法连接硬件监控服务')
  }

  // 轮询
  timer = window.setInterval(async () => {
    const info = await WMIOperation.GetHardwareMonitorInfo()
    if(info) MonitorInfo.value = info
  }, 3000)

  document.body.setAttribute('arco-theme', 'light') // 强制亮色主题
})

onUnmounted(() => {
  if (timer) clearInterval(timer)
})
</script>

<template>
  <div class="dashboard" v-if="MonitorInfo">
    <div class="panel left-top">
      <GpuAmd v-if="MonitorInfo.GpuAmd" :data="MonitorInfo" />
    </div>
    <div class="panel center-top">
      <CenterTop />
    </div>
    <div class="panel right">
      <GpuNvidia :data="MonitorInfo" />
    </div>
    <div class="panel left-bottom">
      <FanSpeedData />
    </div>
    <div class="panel center-bottom">
      <CpuHeatMapData :data="MonitorInfo" />
    </div>
  </div>
  <div v-else class="loading">Initializing Monitor...</div>
</template>

<style lang="scss" scoped>


.dashboard {
  position: relative;
  width: 100vw;
  height: 100vh;
  overflow: hidden;

  .panel {
    position: absolute;
    background: #fff;
    border: 1px solid #e5e6eb;
    box-sizing: border-box;
  }

  .left-top {
    top: 0; left: 0;
    width: 20vw; height: 80vh;
  }

  .left-bottom {
    top: 80vh; left: 0;
    width: 20vw; height: 20vh;
  }

  .center-top {
    top: 0; left: 20vw;
    width: 55vw; height: 75vh;
  }

  .center-bottom {
    top: 75vh; left: 20vw;
    width: 55vw; height: 25vh;
    display: flex;
    flex-direction: column;
  }

  .right {
    top: 0; left: 75vw;
    width: 25vw; height: 100vh;
  }
}

.loading {
  height: 100vh;
  font-size: 20px;
}
</style>