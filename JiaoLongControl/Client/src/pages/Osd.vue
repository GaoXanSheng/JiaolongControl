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
  { value: 'BottomCenter', label: '底部居中' },
  { value: 'Custom', label: '自定义' },
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

// ===== 自定义位置: 屏幕比例预览区拖拽 (与服务端相同的行程百分比映射) =====
const PILL_W = 52
const PILL_H = 14
const previewEl = ref<HTMLElement | null>(null)
let previewDragging = false

const pillStyle = computed(() => {
  const x = ((osdData.value?.CustomX ?? 50) / 100).toFixed(4)
  const y = ((osdData.value?.CustomY ?? 8) / 100).toFixed(4)
  return {
    left: `calc((100% - ${PILL_W}px) * ${x})`,
    top: `calc((100% - ${PILL_H}px) * ${y})`,
    width: `${PILL_W}px`,
    height: `${PILL_H}px`,
  }
})

function applyPreviewPosition(e: PointerEvent) {
  const el = previewEl.value
  if (!el || !osdData.value) return
  const rect = el.getBoundingClientRect()
  const travelX = Math.max(1, rect.width - PILL_W)
  const travelY = Math.max(1, rect.height - PILL_H)
  const clampPct = (v: number) => Math.round(Math.min(100, Math.max(0, v)))
  osdData.value.CustomX = clampPct(((e.clientX - rect.left - PILL_W / 2) / travelX) * 100)
  osdData.value.CustomY = clampPct(((e.clientY - rect.top - PILL_H / 2) / travelY) * 100)
  configStore.debouncedSave()
}

function onPillPointerDown(e: PointerEvent) {
  if (!osdData.value) return
  previewDragging = true
  if (currentPosition.value !== 'Custom') osdData.value.Position = 'Custom'
  ;(e.currentTarget as HTMLElement).setPointerCapture(e.pointerId)
  configStore.debouncedSave()
}

function onPillPointerMove(e: PointerEvent) {
  if (previewDragging) applyPreviewPosition(e)
}

function onPillPointerUp() {
  previewDragging = false
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
    // 回读实际值，避免滑条与系统真实音量脱节（如设备切换后）
    await refreshVolume()
  } catch {
    Message.error('设置音量失败')
  }
}

async function onToggleMute() {
  try {
    const res = await Osd.SetMute(!muted.value)
    if (res.Success) {
      muted.value = !muted.value
      await refreshVolume()
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
  { kind: 'media', label: '媒体播放' },
]

onMounted(() => {
  refreshVolume()
})
</script>

<template>
  <div v-if="osdData" class="h-full overflow-y-auto text-ink p-6 no-scrollbar">
    <div class="max-w-[1300px] mx-auto">
      <!-- 头部标题: 通栏显示, 下方左右两列顶部齐平 -->
      <div>
        <h1 class="text-2xl font-bold tracking-wide">OSD 屏幕显示</h1>
        <p class="text-[13px] text-gray-500 mt-1">
          OSD悬浮指示器：按键时在屏幕顶部即时显示状态，完成后悄然隐去
        </p>
      </div>

      <div class="flex flex-col lg:flex-row gap-6 pt-6">
        <!-- ==================== 左列: 开关与显示项目 ==================== -->
        <div class="flex-1 space-y-4">
          <SettingToggle
            config-path="Osd.Enabled"
            description="启用后，按下音量键、大小写锁定等按键时，屏幕上会弹出置顶的胶囊指示器（无需重启，即时生效）"
            title="启用 OSD 屏幕显示"
          />
          <SettingToggle
            config-path="Osd.ShowVolume"
            description="按下音量增大 / 减小 / 静音键时，显示系统音量条与百分比；OSD 停留期间可直接拖动进度条调整音量，或点击小喇叭按钮切换静音"
            title="音量指示"
          />
          <SettingToggle
            description="按下大写锁定 / 数字锁定 / 滚动锁定键时，显示对应的开关状态"
            title="锁定键状态"
            config-path="Osd.ShowLockKeys"
          />
          <SettingToggle
            config-path="Osd.ShowKeyboardBacklight"
            description="通过本软件调整键盘背光亮度时，显示当前档位（Fn 组合键由 EC 直接处理）；OSD 停留期间可直接点击分段选择 0~3 档"
            title="键盘背光档位"
          />
          <SettingToggle
            description="切换性能模式时（软件内或原生 Fn 热键）显示当前模式名称（原生热键经 EC 状态轮询检测）"
            title="性能模式切换"
            config-path="Osd.ShowPerformanceMode"
          />
          <SettingToggle
            config-path="Osd.ShowPerfTelemetry"
            description="开启后，模式切换 OSD 的标题附带模式名称，并在读取完成后补充显示当前 CPU / GPU 温度与双风扇转速（经 EC 与 NVIDIA API 后台读取，约需零点几秒，读取失败时仅显示模式名）"
            title="模式 OSD 附带温度/转速"
          />
          <SettingToggle
            config-path="Osd.ShowFnLock"
            description="原生 Fn 组合键切换功能键锁定时显示状态（EC 状态轮询检测，变化后约一个轮询间隔内弹出）"
            title="功能键锁定 (Fn Lock)"
          />
          <SettingToggle
            title="触摸板锁定"
            config-path="Osd.ShowTouchpad"
            description="原生 Fn 组合键切换触摸板锁定时显示状态（EC 状态轮询检测，变化后约一个轮询间隔内弹出）"
          />
          <SettingToggle
            config-path="Osd.ShowMedia"
            description="系统有程序开始播放音乐或切歌时，显示曲名与歌手（经 Windows 系统媒体会话检测，支持网易云、Spotify、浏览器等）；胶囊右侧提供上一曲 / 播放暂停 / 下一曲控制按钮"
            title="媒体播放提示"
          />
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
              <div
                class="flex items-center gap-1 p-1 rounded-xl bg-ink/[0.04] border border-ink/[0.06]"
              >
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

              <!-- 位置预览: 16:9 屏幕示意, 拖动胶囊即保存为自定义位置 -->
              <div
                class="relative w-full mt-2.5 rounded-lg border border-ink/[0.08] bg-ink/[0.03] overflow-hidden select-none"
                style="aspect-ratio: 16 / 9"
              >
                <div ref="previewEl" class="absolute inset-x-0 top-0 bottom-[10px]">
                  <div
                    :style="pillStyle"
                    class="absolute rounded-full bg-gradient-to-r from-purple-500 to-blue-500 shadow-[0_0_8px_var(--color-glow-purple)] cursor-grab active:cursor-grabbing touch-none"
                    @pointerdown="onPillPointerDown"
                    @pointermove="onPillPointerMove"
                    @pointerup="onPillPointerUp"
                  />
                </div>
                <div
                  class="absolute inset-x-0 bottom-0 h-[10px] bg-ink/[0.06] border-t border-ink/[0.06]"
                />
              </div>
              <p class="text-[10px] text-gray-600 leading-relaxed mt-1.5">
                拖动胶囊到任意位置即保存为自定义位置（真实 OSD 出现在主屏的对应处）；
                选择预设位置时忽略自定义坐标。当前：X {{ osdData.CustomX }}% · Y
                {{ osdData.CustomY }}%
              </p>
            </div>

            <div class="space-y-1.5">
              <div class="flex justify-between items-center text-[11px]">
                <span class="text-gray-400">出入场时长</span>
                <span class="text-ink font-mono">{{ osdData.AnimationMs }} ms</span>
              </div>
              <a-slider
                v-model="osdData.AnimationMs"
                :max="3000"
                :min="200"
                :step="20"
                class="slider-purple"
                @change="saveDebounced"
              />
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
                :min="100"
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
          <div
            class="bg-panel/60 backdrop-blur-md border border-ink/[0.05] rounded-xl p-5 shadow-lg"
          >
            <h2 class="text-[13px] font-semibold text-gray-300 mb-3">名词解释</h2>
            <div class="text-[11px] text-gray-500 leading-relaxed space-y-2">
              <p>
                <strong>可交互胶囊</strong>:
                音量 / 键盘背光 / 媒体 OSD 停留期间可直接操作（拖动调音量、点分段选背光档位、按钮控制播放），操作不抢走当前应用焦点，锁定键等其他 OSD 保持点击穿透。
              </p>
              <p>
                <strong>键盘事件触发</strong>:
                音量与锁定键经全局键盘钩子监听，按键不会被拦截，系统行为不受影响。
              </p>
              <p>
                <strong>自动缩放</strong>:
                OSD 按显示器 DPI 物理像素定位与渲染，系统缩放调整后位置与清晰度自动适配。
              </p>
              <p>
                <strong>主题自适应</strong>:
                胶囊面板自动跟随应用的深色 / 浅色主题，强调色与工具箱配色一致。
              </p>
            </div>
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
