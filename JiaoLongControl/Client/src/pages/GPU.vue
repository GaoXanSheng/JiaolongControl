<script setup lang="ts">
import {computed, onMounted, onUnmounted, ref, type Ref, watch} from 'vue'
import {Message} from '@arco-design/web-vue'
import {type GpuCurveCapabilitiesInfo, type GpuCurveStatusInfo, NvidiaGpu} from '@/utils/bridge'
import {useConfigStore} from '@/stores/config'
import {useSystemInfoStore} from '@/stores/systemInfo'
import EditableValue from '@/components/common/EditableValue.vue'
import SparklineCard from '@/components/common/SparklineCard.vue'
import {useRingBuffer} from '@/composables/useRingBuffer'
import {storeToRefs} from 'pinia'

const configStore = useConfigStore()
const systemInfoStore = useSystemInfoStore()
const {
  gpuName,
  gpuDriverVersion,
  gpuDriverDate,
  gpuMemoryTotal,
  gpuBusWidth,
  gpuUtilization,
  gpuMemoryUtilization,
  gpuCoreClock,
  gpuMemoryClock,
  gpuTemp,
  gpuFanSpeed,
} = storeToRefs(systemInfoStore)
const loading = ref(false)
const showAdvanced = ref(false)


// --- Sparkline 实时监控卡片: 定义即渲染, 曲线由 SparklineCard 绘制 ---
const historyLength = 20

const metricCards = [
  { key: 'util', label: 'GPU 使用率', source: gpuUtilization, max: 100, color: '#8A2BE2', unit: '%' },
  { key: 'memUtil', label: '显存使用率', source: gpuMemoryUtilization, max: 100, color: '#3B82F6', unit: '%' },
  { key: 'coreClock', label: '核心频率', source: gpuCoreClock, divisor: 100, max: 30, color: '#8A2BE2', unit: 'MHz', unitClass: 'text-[9px]' },
  { key: 'memClock', label: '显存频率', source: gpuMemoryClock, divisor: 100, max: 100, color: '#3B82F6', unit: 'MHz', unitClass: 'text-[9px]' },
  { key: 'temp', label: 'GPU 温度', source: gpuTemp, max: 100, color: '#10B981', unit: '°C' },
  { key: 'fan', label: '风扇转速', source: gpuFanSpeed, divisor: 100, max: 40, color: '#3B82F6', unit: 'RPM', unitClass: 'text-[9px]' },
].map((card) => ({ ...card, ...useRingBuffer(historyLength) }))

let unwatch: (() => void) | null = null

onMounted(() => {
  metricCards.forEach((card) => card.push(card.source.value / (card.divisor ?? 1)))

  const stopWatchers = metricCards.map((card) =>
    watch(card.source, (v) => card.push(v / (card.divisor ?? 1))),
  )
  unwatch = () => stopWatchers.forEach((fn) => fn())

  void fetchGpuRanges()
  void fetchGpuCurve()
})

onUnmounted(() => {
  if (unwatch) unwatch()
})

// --- Settings and Presets Logic ---
const GPUData = computed(() => configStore.config?.Gpu)
const coreClockRange = ref({ Min: 0, Max: 500 })
const memClockRange = ref({ Min: 0, Max: 1500 })

async function fetchGpuRanges() {
  try {
    const [core, mem] = await Promise.all([
      NvidiaGpu.GetGpuCoreClockRange(),
      NvidiaGpu.GetGpuMemoryClockRange(),
    ])

    if (core.Success && core.Data) {
      const min = core.Data.Min ?? 0
      const max = core.Data.Max ?? 500
      coreClockRange.value = { Min: min, Max: max }
      if (GPUData.value && (GPUData.value.GpuClock < min || GPUData.value.GpuClock > max)) {
        GPUData.value.GpuClock = max
      }
    }

    if (mem.Success && mem.Data) {
      const min = mem.Data.Min ?? 0
      const max = mem.Data.Max ?? 1500
      memClockRange.value = { Min: min, Max: max }
      if (GPUData.value && (GPUData.value.MemoryClock < min || GPUData.value.MemoryClock > max)) {
        GPUData.value.MemoryClock = max
      }
    }
  } catch (err) {
    console.error('Failed to fetch GPU ranges', err)
  }
}

// --- 核心/显存频率偏移 (Afterburner 主滑条模型) ---
const curveCaps = ref<GpuCurveCapabilitiesInfo | null>(null)
const curveStatus = ref<GpuCurveStatusInfo | null>(null)
const coreOffset = ref(0)
const memOffset = ref(0)
const curveApplying = ref(false)
const memOffsetMax = computed(() => Math.max(500, curveStatus.value?.MemoryOffsetMaxMhz ?? 1500))
const coreOffsetMin = computed(
  () => curveStatus.value?.CoreOffsetMinMhz ?? curveCaps.value?.CoreOffsetMinMhz ?? -250,
)
const coreOffsetMax = computed(
  () => curveStatus.value?.CoreOffsetMaxMhz ?? curveCaps.value?.CoreOffsetMaxMhz ?? 250,
)
const memOffsetMin = computed(() => curveStatus.value?.MemoryOffsetMinMhz ?? 0)

// 把已保存配置映射到界面控件 (页面初始化与取消修改共用)
function initCurveUiFromConfig() {
  if (!GPUData.value) return
  coreOffset.value = Math.min(
    Math.max(GPUData.value.CoreClockOffsetMhz ?? 0, coreOffsetMin.value),
    coreOffsetMax.value,
  )
  memOffset.value = Math.min(
    Math.max(GPUData.value.MemoryClockOffsetMhz ?? 0, memOffsetMin.value),
    memOffsetMax.value,
  )
}

async function fetchGpuCurve() {
  try {
    const caps = await NvidiaGpu.GetGpuCurveCapabilities()
    if (caps.Success && caps.Data) curveCaps.value = caps.Data

    const status = await NvidiaGpu.GetGpuCurveStatus().catch(() => null)
    if (status?.Success && status.Data) curveStatus.value = status.Data

    initCurveUiFromConfig()
  } catch (err) {
    console.error('Failed to fetch GPU curve state', err)
  }
}

/**
 * 通用「执行动作 → 保存配置 → 提示」骨架: 各 apply/reset 处理共用。
 * action 每步失败时提示错误并返回 false (骨架直接结束);
 * 成功后保存配置, 失败提示 saveFailMsg, 成功按 okLevel 提示 action 返回的消息 (缺省 fallbackOkMsg)。
 */
async function runAndSave(
  busy: Ref<boolean>,
  action: () => Promise<string | false>,
  okLevel: 'success' | 'info',
  fallbackOkMsg: string,
  saveFailMsg: string,
  errMsg: string,
  afterSave?: () => Promise<void>,
) {
  busy.value = true
  try {
    const okMessage = await action()
    if (okMessage === false) return
    const saveRes = await configStore.saveConfig()
    if (!saveRes?.Success) {
      Message.error(saveRes?.Message || saveFailMsg)
      return
    }
    okLevel === 'info' ? Message.info(okMessage || fallbackOkMsg) : Message.success(okMessage || fallbackOkMsg)
    await afterSave?.()
  } catch {
    Message.error(errMsg)
  } finally {
    busy.value = false
  }
}

// 常规设置: 只应用 GPU/显存最低频率限制, 不动高级超频
async function handleApplyNormal() {
  const gpu = GPUData.value
  if (!gpu) return
  await runAndSave(loading, async () => {
    const clockRes = await NvidiaGpu.LockGpuClock(gpu.GpuClock)
    if (!clockRes.Success) {
      Message.error(clockRes.Message || 'GPU 最低频率限制失败')
      return false
    }
    const memClockRes = await NvidiaGpu.LockMemoryClock(gpu.MemoryClock)
    if (!memClockRes.Success) {
      Message.error(memClockRes.Message || '显存最低频率限制失败')
      return false
    }
    return '常规设置已应用并保存'
  }, 'success', '常规设置已应用并保存', '设置保存失败', '应用失败，请检查显卡驱动及桥接服务')
}

// 常规设置: 只解除频率限制并重置滑条, 不动高级超频
async function handleResetNormal() {
  await runAndSave(loading, async () => {
    const clockRes = await NvidiaGpu.ResetGpuClock()
    if (!clockRes.Success) {
      Message.error(clockRes.Message || 'GPU 频率重置失败')
      return false
    }
    const memClockRes = await NvidiaGpu.ResetMemoryClock()
    if (!memClockRes.Success) {
      Message.error(memClockRes.Message || '显存频率重置失败')
      return false
    }
    if (GPUData.value) {
      GPUData.value.GpuClock = coreClockRange.value.Max
      GPUData.value.MemoryClock = memClockRange.value.Max
    }
    return '常规设置已恢复默认'
  }, 'info', '常规设置已恢复默认', '重置值保存失败', '重置失败，请检查显卡驱动及桥接服务')
}

// 高级超频: 只应用核心/显存频率偏移, 不动常规锁频
async function handleApplyCurve() {
  const gpu = GPUData.value
  if (!gpu) return
  await runAndSave(curveApplying, async () => {
    const curveRes = await NvidiaGpu.SetGpuOffsets(coreOffset.value, memOffset.value)
    if (!curveRes.Success) {
      Message.error(curveRes.Message || '频率偏移设置失败')
      return false
    }
    gpu.CoreClockOffsetMhz = coreOffset.value
    gpu.MemoryClockOffsetMhz = memOffset.value
    return curveRes.Message || '高级超频已应用'
  }, 'success', '高级超频已应用', '设置已应用但保存失败', '应用失败，请检查显卡驱动及桥接服务', fetchGpuCurve)
}

// 高级超频: 只清零核心/显存偏移, 不动常规锁频
async function handleResetCurve() {
  await runAndSave(curveApplying, async () => {
    const curveRes = await NvidiaGpu.ResetGpuCurve()
    if (!curveRes.Success) {
      Message.error(curveRes.Message || '偏移重置失败')
      return false
    }
    coreOffset.value = 0
    memOffset.value = 0
    if (GPUData.value) {
      GPUData.value.CoreClockOffsetMhz = 0
      GPUData.value.MemoryClockOffsetMhz = 0
    }
    return '高级超频已重置为默认'
  }, 'info', '高级超频已重置为默认', '重置值保存失败', '重置失败，请检查显卡驱动及桥接服务')
}

// --- 名词解释 ---
const showGlossary = ref(false)
const glossary = [
  {
    term: 'V/F 曲线',
    desc: '电压与频率的对应关系：电压越高，GPU 能稳定运行的频率越高。降压的本质是让曲线在更低电压处达到目标频率。',
  },
  {
    term: '睿频 (Boost)',
    desc: '负载下 GPU 自动提升到的高频状态，实际频率受功耗、温度和电压共同约束。',
  },
  {
    term: '核心频率偏移',
    desc: '把整条 V/F 曲线统一平移：+100 MHz 即全频段超频 100 MHz，-50 MHz 即全频段降压 50 MHz。与 Afterburner 的核心时钟偏移 (Core Clock Offset) 相同。',
  },
  {
    term: '显存频率偏移',
    desc: '在出厂显存频率基础上的增量 (MHz)，提升显存带宽，高分辨率游戏受益明显。设置后重启会自动恢复。',
  },
  {
    term: '最低频率限制 (常规设置)',
    desc: '把核心/显存频率限制在不低于指定值：负载时可自然睿频到驱动上限，不会降到设置值以下，睿频能力不受影响。',
  },
  {
    term: '工作点',
    desc: '驱动根据当前负载、温度与功耗上限自动选择的电压/频率点，会随负载动态变化。',
  },
]
</script>

<template>
  <div v-if="GPUData && gpuName" class="h-full overflow-y-auto text-ink p-6 no-scrollbar">
    <div class="max-w-[1300px] mx-auto flex flex-col lg:flex-row gap-6">
      <!-- ==================== 左/中：显卡主要设置区 ==================== -->
      <div class="flex-1 space-y-6">
        <!-- 头部标题 -->
        <div>
          <h1 class="text-2xl font-bold tracking-wide">GPU 设置</h1>
          <p class="text-[13px] text-gray-500 mt-1">调整 GPU 的性能参数，发挥显卡最佳性能。</p>
        </div>

        <!-- 1. 选择 GPU 与卡片详情 -->
        <div
          class="panel-card flex flex-col md:flex-row justify-between gap-6"
        >
          <div class="space-y-3 md:w-1/2">
            <span class="text-[11px] text-gray-500 font-semibold block uppercase">当前 GPU</span>
            <span class="flex items-center gap-2">
              <span class="w-1.5 h-1.5 rounded-full bg-[#76B900] font-bold"></span>
              {{ gpuName }}
            </span>
          </div>

          <div
            class="grid grid-cols-2 gap-x-6 gap-y-3 text-[11px] text-gray-400 md:w-1/2 md:border-l md:border-ink/[0.05] md:pl-6 pt-1"
          >
            <div>
              <span class="text-gray-600 block mb-0.5">驱动版本</span>
              <span class="text-ink font-medium font-mono">{{ gpuDriverVersion }}</span>
            </div>
            <div>
              <span class="text-gray-600 block mb-0.5">显存容量</span>
              <span class="text-ink font-medium font-mono">{{ gpuMemoryTotal }}</span>
            </div>
            <div>
              <span class="text-gray-600 block mb-0.5">驱动日期</span>
              <span class="text-ink font-medium font-mono">{{ gpuDriverDate }}</span>
            </div>
            <div>
              <span class="text-gray-600 block mb-0.5">总线宽度</span>
              <span class="text-ink font-medium font-mono">{{ gpuBusWidth }}</span>
            </div>
          </div>
        </div>
        <!-- 2. 模式切换按钮 -->
        <div class="flex gap-2">
          <button
            :class="[
              'flex-1 text-xs font-medium px-4 py-2.5 rounded-lg transition-all border',
              !showAdvanced
                ? 'bg-gradient-to-r from-purple-700 to-indigo-600 text-white border-transparent shadow-[0_0_12px_rgba(138,43,226,0.25)]'
                : 'bg-ink/[0.02] text-gray-400 border-ink/10 hover:text-ink hover:border-ink/20',
            ]"
            @click="showAdvanced = false"
          >
            常规设置
          </button>
          <button
            :class="[
              'flex-1 text-xs font-medium px-4 py-2.5 rounded-lg transition-all border',
              showAdvanced
                ? 'bg-gradient-to-r from-purple-700 to-indigo-600 text-white border-transparent shadow-[0_0_12px_rgba(138,43,226,0.25)]'
                : 'bg-ink/[0.02] text-gray-400 border-ink/10 hover:text-ink hover:border-ink/20',
            ]"
            @click="showAdvanced = true"
          >
            高级超频
          </button>
        </div>

        <!-- 常规设置面板 -->
        <div
          v-if="!showAdvanced"
          class="panel-card space-y-5"
        >
          <div class="space-y-5">
            <div class="space-y-2">
              <div class="flex justify-between items-center text-xs">
                <span class="text-gray-300 flex items-center gap-1"
                  >GPU 最低频率
                  <span
                    class="text-gray-500 cursor-pointer text-[10px] hover:text-gray-300"
                    title="设置频率下限：负载时可自然睿频到驱动上限，但不会降到该值以下，睿频能力不受影响"
                    >ⓘ</span
                  >
                </span>
                <EditableValue
                  v-model="GPUData.GpuClock"
                  :max="coreClockRange.Max"
                  :min="coreClockRange.Min"
                  class="text-purple-400 font-medium font-mono"
                  unit="MHz"
                />
              </div>
              <a-slider
                v-model="GPUData.GpuClock"
                :min="coreClockRange.Min"
                :max="coreClockRange.Max"
                class="w-full"
              />
            </div>

            <div class="space-y-2">
              <div class="flex justify-between items-center text-xs">
                <span class="text-gray-300 flex items-center gap-1"
                  >显存最低频率
                  <span
                    class="text-gray-500 cursor-pointer text-[10px] hover:text-gray-300"
                    title="设置频率下限：可自然跑满驱动上限，但不会降到该值以下"
                    >ⓘ</span
                  ></span
                >
                <EditableValue
                  v-model="GPUData.MemoryClock"
                  :max="memClockRange.Max"
                  :min="memClockRange.Min"
                  class="text-purple-400 font-medium font-mono"
                  unit="MHz"
                />
              </div>
              <a-slider
                v-model="GPUData.MemoryClock"
                :min="memClockRange.Min"
                :max="memClockRange.Max"
                class="w-full"
              />
            </div>

            <!-- 功耗限制：笔记本 TGP 由固件/EC 管理，驱动接口不可用，暂不提供 -->
          </div>

          <div class="flex justify-between items-center pt-2 border-t border-ink/[0.04]">
            <button
              class="flex items-center gap-2 text-xs text-gray-400 hover:text-ink border border-ink/10 hover:border-ink/20 bg-ink/[0.02] hover:bg-ink/[0.05] px-4 py-2 rounded-lg transition-colors"
              @click="handleResetNormal"
            >
              重置
            </button>
            <button
              :disabled="loading"
              class="text-xs font-medium text-white bg-gradient-to-r from-purple-700 to-indigo-600 hover:from-purple-600 hover:to-indigo-500 disabled:opacity-50 px-6 py-2 rounded-lg transition-all shadow-[0_0_15px_rgba(138,43,226,0.3)]"
              @click="handleApplyNormal"
            >
              {{ loading ? '应用中...' : '应用' }}
            </button>
          </div>
        </div>

        <!-- 高级超频面板 -->
        <div
          v-if="showAdvanced"
          class="panel-card space-y-6"
        >
          <div
            v-if="curveCaps && !curveCaps.Supported"
            class="text-[11px] text-amber-400/90 bg-amber-500/10 border border-amber-500/20 rounded-lg px-3 py-2"
          >
            本机不支持核心频率偏移：{{ curveCaps.Reason }}。显存偏移仍可设置，超频可用「常规设置」把显存最低频率拉满代替。
          </div>

          <!-- 锁频上限观测: 应用核心偏移后此读数若随偏移上移, 说明「锁最低频率」与超频可共存 -->
          <div
            v-if="curveStatus && curveStatus.NvmlMaxSmClockMhz != null"
            class="flex justify-between items-center text-[11px] text-gray-500"
            title="驱动侧最大 SM 频率读数。应用核心偏移后点击应用, 此值随偏移上涨即代表锁最低频率不会封顶超频"
          >
            <span class="flex items-center gap-1"
              >驱动锁频上限读数
              <span class="text-gray-600 cursor-pointer text-[10px] hover:text-gray-400">ⓘ</span>
            </span>
            <span class="font-mono text-gray-400"
              >{{ curveStatus.NvmlMaxSmClockMhz }} MHz</span
            >
          </div>

          <!-- 核心频率偏移 -->
          <div v-if="curveCaps?.Supported" class="space-y-2">
            <div class="flex justify-between items-center text-xs">
              <span class="text-gray-300 flex items-center gap-1"
                >核心频率偏移
                <span
                  class="text-gray-500 cursor-pointer text-[10px] hover:text-gray-300"
                  title="把整条 V/F 曲线统一平移：+100 MHz 即全频段超频 100 MHz，-50 MHz 即全频段降压（与 Afterburner 的核心时钟偏移相同）"
                  >ⓘ</span
                ></span
              >
              <span class="text-purple-400 font-medium font-mono">
                <EditableValue
                  v-model="coreOffset"
                  :max="coreOffsetMax"
                  :min="coreOffsetMin"
                  :step="5"
                  class="text-purple-400 font-medium font-mono"
                  signed
                  unit="MHz"
                />
                <span
                  v-if="coreOffset !== 0"
                  :class="coreOffset > 0 ? 'text-amber-400' : 'text-emerald-400'"
                  class="text-[10px] font-sans"
                  >{{ coreOffset > 0 ? '超频' : '降压' }}</span
                >
              </span>
            </div>
            <a-slider
              v-model="coreOffset"
              :max="coreOffsetMax"
              :min="coreOffsetMin"
              :step="5"
              class="w-full"
            />
          </div>

          <!-- 显存频率偏移 -->
          <div class="space-y-2">
            <div class="flex justify-between items-center text-xs">
              <span class="text-gray-300 flex items-center gap-1">显存频率偏移</span>
              <span class="text-purple-400 font-medium font-mono"
                ><EditableValue
                  v-model="memOffset"
                  :max="memOffsetMax"
                  :min="memOffsetMin"
                  :step="50"
                  class="text-purple-400 font-medium font-mono"
                  signed
                  unit="MHz"
                /></span>
            </div>
            <a-slider
              v-model="memOffset"
              :max="memOffsetMax"
              :min="memOffsetMin"
              :step="50"
              class="w-full"
            />
          </div>

          <div class="flex justify-between items-center pt-2 border-t border-ink/[0.04]">
            <button
              class="flex items-center gap-2 text-xs text-gray-400 hover:text-ink border border-ink/10 hover:border-ink/20 bg-ink/[0.02] hover:bg-ink/[0.05] px-4 py-2 rounded-lg transition-colors"
              @click="handleResetCurve"
            >
              重置
            </button>
            <button
              :disabled="curveApplying"
              class="text-xs font-medium text-white bg-gradient-to-r from-purple-700 to-indigo-600 hover:from-purple-600 hover:to-indigo-500 disabled:opacity-50 px-6 py-2 rounded-lg transition-all shadow-[0_0_15px_rgba(138,43,226,0.3)]"
              @click="handleApplyCurve"
            >
              {{ curveApplying ? '应用中...' : '应用' }}
            </button>
          </div>
        </div>
      </div>

      <!-- ==================== 右侧：显卡信息与实时监控栏 ==================== -->
      <div class="w-full lg:w-[360px] shrink-0 space-y-6 lg:pt-[115px]">
        <!-- 3. 实时监控面板 -->
        <div
          class="panel-card space-y-4"
        >
          <div class="flex justify-between items-center">
            <h2 class="text-[13px] font-semibold text-gray-300">实时监控</h2>
          </div>
          <div class="grid grid-cols-2 gap-3">
            <SparklineCard
              v-for="card in metricCards"
              :key="card.key"
              :color="card.color"
              :gradient-id="`gpu-sg-${card.key}`"
              :history="card.buffer"
              :label="card.label"
              :max="card.max"
              :unit="card.unit"
              :unit-class="card.unitClass"
              :value="card.source.value"
            />
          </div>
        </div>

        <!-- 说明卡片 -->
        <div
          class="panel-card space-y-2.5"
        >
          <h2 class="text-[13px] font-semibold text-gray-300">说明</h2>
          <div class="text-[11px] text-gray-500 leading-relaxed space-y-2">
            <p>核心频率偏移把整条电压-频率曲线统一平移：正值超频，负值降压。</p>
            <p>显存频率偏移在出厂显存频率基础上提升带宽。</p>
            <p>修改设置后请点击"应用"以生效。</p>
          </div>
          <div
            class="text-[11px] text-blue-400 hover:text-blue-300 cursor-pointer pt-1 flex items-center gap-0.5 font-medium transition-colors"
            @click="showGlossary = !showGlossary"
          >
            {{ showGlossary ? '收起术语说明' : '了解更多' }} <span>&gt;</span>
          </div>
          <div v-if="showGlossary" class="space-y-2.5 pt-1">
            <div v-for="item in glossary" :key="item.term" class="text-[11px] leading-relaxed">
              <span class="text-gray-300 font-medium">{{ item.term }}</span>
              <span class="text-gray-500 ml-2">{{ item.desc }}</span>
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
