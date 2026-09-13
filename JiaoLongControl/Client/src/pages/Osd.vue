<script lang="ts" setup>
import {computed, onMounted, ref} from 'vue'
import {Message} from '@arco-design/web-vue'
import SettingToggle from '@/components/common/SettingToggle.vue'
import {useConfigStore} from '@/stores/config'
import {Osd} from '@/utils/bridge'
import type {OsdPosition} from '@/types/config'

const configStore = useConfigStore()
if (!configStore.config) {
  await configStore.fetchConfig()
}
const osdData = computed(() => configStore.config?.Osd)

// ===== 显示效果 =====
const positionOptions: Array<{ value: OsdPosition; label: string }> = [
  { value: 'TopCenter', label: '顶部居中' },
  { value: 'TopRight', label: '顶部右侧' },
  { value: 'BottomCenter', label: '底部居中' },
]
const currentPosition = computed(() => osdData.value?.Position ?? 'TopCenter')

function selectPosition(value: OsdPosition) {
  if (!osdData.value || currentPosition.value === value) return
  osdData.value.Position = value
  configStore.debouncedSave()
}

function saveDebounced() {
  configStore.debouncedSave()
}

// ===== 效果预览: 音量实时控制 + 各状态 OSD 预览 =====
const volume = ref(0)
const muted = ref(false)

async function refreshVolume() {
  try {
    const res = await Osd.GetVolume()
    if (res.Success && res.Data) {
      volume.value = res.Data.Volume
      muted.value = res.Data.Muted
    }
  } catch {
    /* 忽略 */
  }
}

async function onVolumeChange(val: number | [number, number]) {
  if (typeof val !== 'number') return
  try {
    const res = await Osd.SetVolume(val)
    if (!res.Success) Message.error(res.Message)
    muted.value = false
  } catch {
    Message.error('设置音量失败')
  }
}

async function onToggleMute() {
  try {
    const res = await Osd.SetMute(!muted.value)
    if (res.Success) {
      muted.value = !muted.value
    } else {
      Message.error(res.Message)
    }
  } catch {
    Message.error('设置静音失败')
  }
}

async function preview(kind: string, value = 0) {
  try {
    const res = await Osd.ShowTest(kind, value)
    if (!res.Success) Message.error(res.Message)
  } catch {
    Message.error('OSD 预览失败')
  }
}

const previewItems = [
  { kind: 'volume', label: '音量指示' },
  { kind: 'lock', label: '锁定键' },
  { kind: 'keyboard', label: '键盘背光' },
  { kind: 'perf', label: '性能模式' },
]

onMounted(() => {
  refreshVolume()
})
</script>

<template>
  <div v-if="osdData" class="h-full overflow-y-auto text-ink p-6 no-scrollbar">
    <div class="max-w-[1300px] mx-auto flex flex-col lg:flex-row gap-6">
      <!-- ==================== 左列: 开关与显示项目 ==================== -->
      <div class="flex-1 space-y-6">
        <!-- 头部标题 -->
        <div>
          <h1 class="text-2xl font-bold tracking-wide">OSD 屏幕显示</h1>
          <p class="text-[13px] text-gray-500 mt-1">
            OSD悬浮指示器：按键时在屏幕顶部即时显示状态，完成后悄然隐去
          </p>
        </div>

        <div class="space-y-4">
          <SettingToggle
            config-path="Osd.Enabled"
            description="启用后，按下音量键、大小写锁定等按键时，屏幕上会弹出置顶的胶囊指示器（无需重启，即时生效）"
            title="启用 OSD 屏幕显示"
          />
          <SettingToggle
            config-path="Osd.ShowVolume"
            description="按下音量增大 / 减小 / 静音键时，显示系统音量条与百分比"
            title="音量指示"
          />
          <SettingToggle
            config-path="Osd.ShowLockKeys"
            description="按下大写锁定 / 数字锁定 / 滚动锁定键时，显示对应的开关状态"
            title="锁定键状态"
          />
          <SettingToggle
            config-path="Osd.ShowKeyboardBacklight"
            description="通过本软件调整键盘背光亮度时，显示当前档位（Fn 组合键由 EC 直接处理，不产生系统键盘事件）"
            title="键盘背光档位"
          />
          <SettingToggle
            config-path="Osd.ShowPerformanceMode"
            description="切换性能模式时（软件内或原生 Fn 热键）显示当前模式名称（原生热键经 EC 状态轮询检测）"
            title="性能模式切换"
          />
          <SettingToggle
            config-path="Osd.ShowFnLock"
            description="原生 Fn 组合键切换功能键锁定时显示状态（EC 状态轮询检测，变化后约一个轮询间隔内弹出）"
            title="功能键锁定 (Fn Lock)"
          />
          <SettingToggle
            config-path="Osd.ShowTouchpad"
            description="原生 Fn 组合键切换触摸板锁定时显示状态（EC 状态轮询检测，变化后约一个轮询间隔内弹出）"
            title="触摸板锁定"
          />
        </div>
      </div>

      <!-- ==================== 右列: 显示效果与预览 ==================== -->
      <div class="w-full lg:w-[380px] shrink-0 space-y-6">
        <!-- 显示效果 -->
        <div
          class="bg-panel/60 backdrop-blur-md border border-ink/[0.05] rounded-xl p-5 shadow-lg space-y-5"
        >
          <h3
            class="text-xs font-black text-purple-400 uppercase tracking-widest border-l-4 border-purple-600 pl-2.5"
          >
            显示效果
          </h3>

          <div>
            <div class="text-[11px] text-gray-400 mb-2">显示位置</div>
            <div class="flex items-center gap-1 p-1 rounded-xl bg-ink/[0.04] border border-ink/[0.06]">
              <button
                v-for="opt in positionOptions"
                :key="opt.value"
                :class="
                  currentPosition === opt.value
                    ? 'bg-cyber-purple text-white shadow-[0_0_10px_var(--color-glow-purple)]'
                    : 'text-muted hover:text-ink'
                "
                class="flex-1 px-2 py-1.5 rounded-lg text-xs font-medium transition-all cursor-pointer"
                @click="selectPosition(opt.value)"
              >
                {{ opt.label }}
              </button>
            </div>
          </div>

          <div class="space-y-1.5">
            <div class="flex justify-between items-center text-[11px]">
              <span class="text-gray-400">显示时长</span>
              <span class="text-ink font-mono">{{ osdData.DurationMs }} ms</span>
            </div>
            <a-slider
              v-model="osdData.DurationMs"
              :max="5000"
              :min="500"
              :step="100"
              class="slider-purple"
              @change="saveDebounced"
            />
          </div>

          <div class="space-y-1.5">
            <div class="flex justify-between items-center text-[11px]">
              <span class="text-gray-400">面板不透明度</span>
              <span class="text-ink font-mono">{{ osdData.Opacity }}%</span>
            </div>
            <a-slider
              v-model="osdData.Opacity"
              :max="100"
              :min="30"
              class="slider-blue"
              @change="saveDebounced"
            />
          </div>

          <div class="space-y-1.5">
            <div class="flex justify-between items-center text-[11px]">
              <span class="text-gray-400">界面缩放</span>
              <span class="text-ink font-mono">{{ osdData.Scale }}%</span>
            </div>
            <a-slider
              v-model="osdData.Scale"
              :max="200"
              :min="50"
              :step="5"
              class="slider-orange"
              @change="saveDebounced"
            />
          </div>

          <div class="space-y-1.5">
            <div class="flex justify-between items-center text-[11px]">
              <span class="text-gray-400">EC 状态轮询间隔</span>
              <span class="text-ink font-mono">{{ osdData.PollIntervalMs }} ms</span>
            </div>
            <a-slider
              v-model="osdData.PollIntervalMs"
              :max="5000"
              :min="500"
              :step="100"
              class="slider-red"
              @change="saveDebounced"
            />
            <p class="text-[10px] text-gray-600 leading-relaxed">
              原生 Fn 热键（性能模式 / 背光 / Fn 锁 / 触摸板锁）优先经 HID_EVENT20
              事件通道即时检测；此间隔仅在事件通道不可用时作为轮询兜底
            </p>
          </div>
        </div>

        <!-- 效果预览 -->
        <div
          class="bg-panel/60 backdrop-blur-md border border-ink/[0.05] rounded-xl p-5 shadow-lg space-y-4"
        >
          <h3
            class="text-xs font-black text-blue-400 uppercase tracking-widest border-l-4 border-blue-500 pl-2.5"
          >
            效果预览
          </h3>

          <div class="space-y-1.5">
            <div class="flex justify-between items-center text-[11px]">
              <span class="text-gray-400">系统音量（拖动即弹出 OSD）</span>
              <span class="text-ink font-mono">{{ volume }}%{{ muted ? ' · 静音' : '' }}</span>
            </div>
            <div class="flex items-center gap-3">
              <a-slider
                v-model="volume"
                :max="100"
                :min="0"
                class="flex-1 slider-blue"
                @change="onVolumeChange"
              />
              <a-button
                class="!bg-blue-600/10 !text-blue-400 !border-blue-500/25 hover:!bg-blue-600 hover:!text-white rounded-md px-3 font-semibold transition"
                size="small"
                @click="onToggleMute"
              >
                {{ muted ? '取消静音' : '静音' }}
              </a-button>
            </div>
          </div>

          <div class="grid grid-cols-2 gap-2">
            <a-button
              v-for="item in previewItems"
              :key="item.kind"
              class="!rounded-lg font-semibold !bg-ink/[0.04] !text-ink !border-ink/[0.08] hover:!bg-purple-600/20 hover:!text-purple-300 transition"
              @click="preview(item.kind)"
            >
              预览{{ item.label }}
            </a-button>
          </div>
        </div>

        <!-- 说明 -->
        <div class="bg-panel/60 backdrop-blur-md border border-ink/[0.05] rounded-xl p-5 shadow-lg">
          <h2 class="text-[13px] font-semibold text-gray-300 mb-3">名词解释</h2>
          <div class="text-[11px] text-gray-500 leading-relaxed space-y-2">
            <p>
              <strong>键盘事件触发</strong>: 音量与锁定键经全局键盘钩子监听，按键不会被拦截，系统行为不受影响。
            </p>
            <p>
              <strong>自动缩放</strong>: OSD 按显示器 DPI 物理像素定位与渲染，系统缩放调整后位置与清晰度自动适配。
            </p>
            <p>
              <strong>主题自适应</strong>: 胶囊面板自动跟随应用的深色 / 浅色主题，强调色与工具箱配色一致。
            </p>
          </div>
        </div>
      </div>
    </div>
  </div>
  <div v-else class="flex items-center justify-center h-full">
    <a-spin dot />
  </div>
</template>

<style lang="scss" scoped>
/* 与 RyzenSmu 页一致的分色滑条 */
:deep(.slider-purple .arco-slider-bar) {
  background: linear-gradient(90deg, #6366f1 0%, var(--color-accent-purple) 100%) !important;
  height: 5px !important;
  border-radius: 99px;
}
:deep(.slider-purple .arco-slider-button) {
  border: 2px solid var(--color-accent-purple) !important;
  box-shadow: 0 0 8px rgba(138, 43, 226, 0.6) !important;
}

:deep(.slider-blue .arco-slider-bar) {
  background: linear-gradient(90deg, #3b82f6 0%, #1d4ed8 100%) !important;
  height: 5px !important;
  border-radius: 99px;
}
:deep(.slider-blue .arco-slider-button) {
  border: 2px solid #3b82f6 !important;
  box-shadow: 0 0 8px rgba(59, 130, 246, 0.6) !important;
}

:deep(.slider-orange .arco-slider-bar) {
  background: linear-gradient(90deg, #ff7d00 0%, #ff5000 100%) !important;
  height: 5px !important;
  border-radius: 99px;
}
:deep(.slider-orange .arco-slider-button) {
  border: 2px solid #ff7d00 !important;
  box-shadow: 0 0 8px rgba(255, 125, 0, 0.6) !important;
}

:deep(.slider-red .arco-slider-bar) {
  background: linear-gradient(90deg, #f43f5e 0%, #e11d48 100%) !important;
  height: 5px !important;
  border-radius: 99px;
}
:deep(.slider-red .arco-slider-button) {
  border: 2px solid #f43f5e !important;
  box-shadow: 0 0 8px rgba(225, 29, 72, 0.6) !important;
}
</style>
