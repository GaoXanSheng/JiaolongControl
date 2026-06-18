<script setup lang="ts">
import { ref, computed } from 'vue'
import { Message } from '@arco-design/web-vue'
import { AutoFanControl, Fan } from '@/utils/bridge'
import { useConfigStore } from '@/stores/config'
import FanSpeed from "@/components/common/FanSpeed.vue";

const loading = ref(false)
const visible = ref(false)
const configStore = useConfigStore()

const FanPageStore = computed(() => configStore.config?.FanPageStore)

const handleClick = () => {
  if (!FanPageStore.value) return
  if (FanPageStore.value.FanSpeed > 5800 || FanPageStore.value.FanSpeed < 1500) {
    visible.value = true
  } else {
    handleOk()
  }
}

const handleOk = async () => {
  if (!FanPageStore.value) return
  visible.value = false
  loading.value = true
  const isRunningRes = await AutoFanControl.IsRunning()
  if (isRunningRes.Success && isRunningRes.Data) {
    await AutoFanControl.Stop()
  }
  const res = await Fan.SetFanSpeed(FanPageStore.value.FanSpeed)
  res.Success ? Message.success(res.Message) : Message.error(res.Message)
  configStore.debouncedSave()
  loading.value = false
}

const handleCancel = () => {
  visible.value = false
}

async function handleRemoveFanClick() {
  const isRunningRes = await AutoFanControl.IsRunning()
  if (isRunningRes.Success && isRunningRes.Data) {
    await AutoFanControl.Stop()
  }
  const res = await Fan.RemoveFanSpeed()
  res.Success ? Message.success(res.Message) : Message.error(res.Message)
}
</script>

<template>
  <div class="h-full overflow-y-auto text-white p-6 no-scrollbar" v-if="FanPageStore">
    <div class="max-w-[1300px] mx-auto flex flex-col lg:flex-row gap-6">

      <!-- ==================== 左侧：手动风扇控制区 ==================== -->
      <div class="flex-1 space-y-6">
        <!-- 头部标题 -->
        <div>
          <h1 class="text-2xl font-bold tracking-wide">风扇控制</h1>
          <p class="text-[13px] text-gray-500 mt-1">手动调节风扇转速或恢复自动控制</p>
        </div>

        <!-- 转速调节磨砂卡片 -->
        <div class="bg-[#121320]/60 backdrop-blur-md border border-white/[0.05] rounded-xl p-6 shadow-lg">
          <div class="flex flex-col gap-6">
            <div class="flex justify-between items-end">
              <div>
                <span class="text-[10px] font-bold text-purple-400 uppercase tracking-widest block">Manual Control</span>
                <h2 class="text-base font-semibold mt-1 text-gray-200">目标转速设定</h2>
              </div>
              <div class="text-right">
                <span class="text-3xl font-black font-mono text-white leading-none">{{ FanPageStore.FanSpeed }}</span>
                <span class="text-gray-500 text-xs ml-1 font-bold font-mono">RPM</span>
              </div>
            </div>

            <!-- 自定义发光滑块 -->
            <a-slider
                v-model="FanPageStore.FanSpeed"
                :min="0"
                :max="8000"
                :step="100"
                class="w-full"
            />

            <!-- 动作按钮组 -->
            <div class="grid grid-cols-2 gap-4 mt-2">
              <button
                  @click="handleClick"
                  :disabled="loading"
                  class="text-xs font-semibold text-white bg-gradient-to-r from-purple-700 to-indigo-600 hover:from-purple-600 hover:to-indigo-500 disabled:opacity-50 py-2.5 rounded-lg transition-all shadow-[0_0_15px_rgba(138,43,226,0.3)]"
              >
                {{ loading ? '应用中...' : '应用设定' }}
              </button>
              <button
                  @click="handleRemoveFanClick"
                  class="text-xs font-semibold text-gray-300 hover:text-white border border-white/10 hover:border-white/20 bg-white/[0.02] hover:bg-white/[0.05] py-2.5 rounded-lg transition-all"
              >
                移除限制
              </button>
            </div>
          </div>
        </div>
      </div>

      <!-- ==================== 右侧：实时监控与安全提醒栏 ==================== -->
      <div class="w-full lg:w-[360px] shrink-0 space-y-6">
        <!-- 1. 实时状态监控（原先直接跟在下方的监控改到右侧排版） -->
        <FanSpeed />

        <!-- 2. 安全说明卡片 -->
        <div class="bg-[#121320]/60 backdrop-blur-md border border-white/[0.05] rounded-xl p-5 shadow-lg space-y-2.5 select-none">
          <h2 class="text-[13px] font-semibold text-gray-300">安全提示</h2>
          <div class="text-[11px] text-gray-500 leading-relaxed space-y-2">
            <p><strong>手动设定</strong>: 会关闭自动风扇温控后台，使转速锁定在您调整的设定值上。</p>
            <p><strong>高转速磨损</strong>: 长时间处于超过 5800 RPM 极高转速可能会缩短电机寿命并产生刺耳噪音。</p>
            <p><strong>低转速降频</strong>: 重载时转速过低会导致处理器和图形芯片因过热而发生降频物理限速。</p>
          </div>
        </div>
      </div>

    </div>

    <!-- 安全警告暗色模态窗口 -->
    <a-modal v-model:visible="visible" @ok="handleOk" @cancel="handleCancel" simple :mask-closable="false">
      <template #title>⚠️ 安全警告</template>
      <div class="text-[12px] text-gray-300 leading-relaxed">
        设定目标转速高于 <span class="text-rose-400 font-bold font-mono">5800 RPM</span> 或低于 <span class="text-rose-400 font-bold font-mono">1500 RPM</span>，可能会引起系统噪音剧增、硬件热量积攒异常。请确认您在清楚此操作后果的前提下继续。
      </div>
    </a-modal>
  </div>

  <div v-else class="flex items-center justify-center h-full">
    <a-spin dot />
  </div>
</template>

<style lang="scss" scoped>
/* 隐藏滚动条 */
.no-scrollbar::-webkit-scrollbar {
  display: none;
}
.no-scrollbar {
  -ms-overflow-style: none;
  scrollbar-width: none;
}

/* 高发光 Slider 拖拽钮及轨道重写 */
:deep(.arco-slider-bar) {
  background: linear-gradient(90deg, #6366f1 0%, #8A2BE2 100%) !important;
  height: 5px !important;
  border-radius: 99px;
}
:deep(.arco-slider-track) {
  background-color: rgba(255, 255, 255, 0.04) !important;
  height: 5px !important;
  border-radius: 99px;
}
:deep(.arco-slider-button) {
  background-color: #ffffff !important;
  border: 2.5px solid #8A2BE2 !important;
  width: 13px !important;
  height: 13px !important;
  box-shadow: 0 0 10px rgba(138, 43, 226, 0.7) !important;
}

/* 重构 Arco Modal 的深色磨砂遮罩及按钮样式 */
:deep(.arco-modal) {
  background-color: #121320 !important;
  border: 1px solid rgba(255, 255, 255, 0.08) !important;
  border-radius: 12px !important;
  box-shadow: 0 12px 36px rgba(0, 0, 0, 0.6) !important;

  .arco-modal-header {
    border-bottom: 1px solid rgba(255, 255, 255, 0.05) !important;
    .arco-modal-title {
      color: #ffffff !important;
      font-size: 13px !important;
    }
  }

  .arco-modal-footer {
    border-top: 1px solid rgba(255, 255, 255, 0.05) !important;

    .arco-btn-secondary {
      background-color: rgba(255, 255, 255, 0.02) !important;
      border: 1px solid rgba(255, 255, 255, 0.05) !important;
      color: rgba(255, 255, 255, 0.6) !important;
      border-radius: 6px !important;
      font-size: 11px !important;
    }
    .arco-btn-primary {
      background-color: #e11d48 !important;
      border: none !important;
      color: #ffffff !important;
      border-radius: 6px !important;
      font-size: 11px !important;
      box-shadow: 0 0 10px rgba(225, 29, 72, 0.3) !important;
    }
  }
}
</style>