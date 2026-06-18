<script setup lang="ts">
import { ref, computed } from 'vue'
import { Message } from '@arco-design/web-vue'
import { CPU, Power } from '@/utils/bridge.ts'
import { useConfigStore } from '@/stores/config'

const loading = ref(false)
const configStore = useConfigStore()

// 使用 computed 来简化对配置项的访问，并确保响应性
const CPUData = computed(() => configStore.config?.AdvancedCPUSystemConfig)

// 页面内部交互状态
const selectedProfile = ref('default')
const cpuVoltageOffset = ref(-50) // 模拟核心电压偏移
const cpuSpeedShift = ref(true)    // 模拟 Speed Shift
const cStateControl = ref('auto')   // 模拟 C-State

// 配置文件切换预设值（点击卡片时自动调整滑块，增强交互感）
function selectProfile(profile: string) {
  selectedProfile.value = profile
  if (!CPUData.value) return

  if (profile === 'default') {
    CPUData.value.CpuLongPower = 45
    CPUData.value.CpuShortPower = 65
    CPUData.value.CpuMaxFrequency = 4400
    CPUData.value.CpuTempWall = 80
  } else if (profile === 'performance') {
    CPUData.value.CpuLongPower = 65
    CPUData.value.CpuShortPower = 90
    CPUData.value.CpuMaxFrequency = 4700
    CPUData.value.CpuTempWall = 95
  } else if (profile === 'saving') {
    CPUData.value.CpuLongPower = 30
    CPUData.value.CpuShortPower = 45
    CPUData.value.CpuMaxFrequency = 3200
    CPUData.value.CpuTempWall = 75
  }
}

// 统一应用逻辑
async function handleApplyAll() {
  if (!CPUData.value) return
  loading.value = true
  try {
    // 1. 设置长时功耗限制 (PL1)
    await CPU.SetCpuLongPower(CPUData.value.CpuLongPower)
    // 2. 设置短时功耗限制 (PL2)
    await CPU.SetCpuShortPower(CPUData.value.CpuShortPower)
    // 3. 设置温度墙
    await CPU.SetCPUTempWall(CPUData.value.CpuTempWall)
    // 4. 设置最大频率
    await Power.SetCPUMaxFrequency(CPUData.value.CpuMaxFrequency)
    // 5. 设置睿频开关
    if (CPUData.value.CpuTurbo) {
      await Power.EnableTurbo()
    } else {
      await Power.DisableTurbo()
    }

    // 保存并提示
    configStore.debouncedSave()
    Message.success('设置应用成功')
  } catch (error) {
    Message.error('应用设置失败，请检查桥接服务。')
  } finally {
    loading.value = false
  }
}

// 重置到初始状态
function handleReset() {
  selectProfile('default')
  Message.info('参数已重置为默认配置')
}

// 取消修改
function handleCancel() {
  configStore.loadConfig?.() // 重新加载 store 原始配置
  Message.info('已取消修改')
}
</script>

<template>
  <div class="h-full overflow-y-auto text-white p-6 no-scrollbar" v-if="CPUData">
    <div class="max-w-[1300px] mx-auto flex flex-col lg:flex-row gap-6">

      <!-- ==================== 左/中：CPU 设置区域 ==================== -->
      <div class="flex-1 space-y-6">
        <!-- 头部标题 -->
        <div>
          <h1 class="text-2xl font-bold tracking-wide">CPU 设置</h1>
          <p class="text-[13px] text-gray-500 mt-1">调整 CPU 的性能参数，发挥处理器最佳性能。</p>
        </div>

        <!-- 1. CPU 配置文件 -->
        <div class="bg-[#121320]/60 backdrop-blur-md border border-white/[0.05] rounded-xl p-5 shadow-lg">
          <div class="flex justify-between items-center mb-4">
            <h2 class="text-[13px] font-semibold text-gray-300">CPU 配置文件</h2>
          </div>

          <div class="grid grid-cols-2 md:grid-cols-4 gap-3">
            <!-- 默认配置 -->
            <div
                @click="selectProfile('default')"
                :class="[
                'border rounded-xl p-4 cursor-pointer transition-all duration-300 flex flex-col justify-between h-[96px]',
                selectedProfile === 'default' 
                  ? 'border-[#8A2BE2] bg-[#1a182f] shadow-[0_0_15px_rgba(138,43,226,0.25)]' 
                  : 'border-white/[0.05] bg-[#121320] hover:border-white/10'
              ]"
            >
              <span class="text-xs font-semibold" :class="selectedProfile === 'default' ? 'text-white' : 'text-gray-300'">默认配置</span>
              <span class="text-[11px] text-gray-500">平衡性能与功耗</span>
            </div>

            <!-- 高性能模式 -->
            <div
                @click="selectProfile('performance')"
                :class="[
                'border rounded-xl p-4 cursor-pointer transition-all duration-300 flex flex-col justify-between h-[96px]',
                selectedProfile === 'performance' 
                  ? 'border-[#8A2BE2] bg-[#1a182f] shadow-[0_0_15px_rgba(138,43,226,0.25)]' 
                  : 'border-white/[0.05] bg-[#121320] hover:border-white/10'
              ]"
            >
              <span class="text-xs font-semibold" :class="selectedProfile === 'performance' ? 'text-white' : 'text-gray-300'">高性能模式</span>
              <span class="text-[11px] text-gray-500">释放最大性能</span>
            </div>

            <!-- 节能模式 -->
            <div
                @click="selectProfile('saving')"
                :class="[
                'border rounded-xl p-4 cursor-pointer transition-all duration-300 flex flex-col justify-between h-[96px]',
                selectedProfile === 'saving' 
                  ? 'border-[#8A2BE2] bg-[#1a182f] shadow-[0_0_15px_rgba(138,43,226,0.25)]' 
                  : 'border-white/[0.05] bg-[#121320] hover:border-white/10'
              ]"
            >
              <span class="text-xs font-semibold" :class="selectedProfile === 'saving' ? 'text-white' : 'text-gray-300'">节能模式</span>
              <span class="text-[11px] text-gray-500">降低功耗与温度</span>
            </div>

            <!-- 自定义配置 -->
            <div
                @click="selectProfile('custom')"
                :class="[
                'border rounded-xl p-4 cursor-pointer transition-all duration-300 flex flex-col justify-between h-[96px]',
                selectedProfile === 'custom' 
                  ? 'border-[#8A2BE2] bg-[#1a182f] shadow-[0_0_15px_rgba(138,43,226,0.25)]' 
                  : 'border-white/[0.05] bg-[#121320] hover:border-white/10'
              ]"
            >
              <span class="text-xs font-semibold" :class="selectedProfile === 'custom' ? 'text-white' : 'text-gray-300'">自定义配置</span>
              <span class="text-[11px] text-gray-500">自定义参数设置</span>
            </div>
          </div>
        </div>

        <!-- 2. 核心设置 -->
        <div class="bg-[#121320]/60 backdrop-blur-md border border-white/[0.05] rounded-xl p-5 shadow-lg space-y-6">
          <h2 class="text-[13px] font-semibold text-gray-300">核心设置</h2>

          <div class="space-y-6">
            <!-- 功耗限制 (PL1) -->
            <div class="space-y-2">
              <div class="flex justify-between items-center text-xs">
                <span class="text-gray-300 flex items-center gap-1">功耗限制 (PL1) <span class="text-gray-500 cursor-pointer text-[10px] hover:text-gray-300">ⓘ</span></span>
                <span class="text-purple-400 font-medium font-mono">{{ CPUData.CpuLongPower }} W</span>
              </div>
              <a-slider v-model="CPUData.CpuLongPower" :min="30" :max="255" class="w-full" />
            </div>

            <!-- 短时功耗限制 (PL2) -->
            <div class="space-y-2">
              <div class="flex justify-between items-center text-xs">
                <span class="text-gray-300 flex items-center gap-1">短时功耗限制 (PL2) <span class="text-gray-500 cursor-pointer text-[10px] hover:text-gray-300">ⓘ</span></span>
                <span class="text-purple-400 font-medium font-mono">{{ CPUData.CpuShortPower }} W</span>
              </div>
              <a-slider v-model="CPUData.CpuShortPower" :min="30" :max="255" class="w-full" />
            </div>

            <!-- 核心电压偏移 (电压微调演示) -->
            <div class="space-y-2">
              <div class="flex justify-between items-center text-xs">
                <span class="text-gray-300 flex items-center gap-1">核心电压偏移 <span class="text-gray-500 cursor-pointer text-[10px] hover:text-gray-300">ⓘ</span></span>
                <span class="text-purple-400 font-medium font-mono">{{ cpuVoltageOffset }} mV</span>
              </div>
              <a-slider v-model="cpuVoltageOffset" :min="-150" :max="0" class="w-full" />
            </div>

            <!-- 最大睿频频率 -->
            <div class="space-y-2">
              <div class="flex justify-between items-center text-xs">
                <span class="text-gray-300 flex items-center gap-1">最大睿频频率 <span class="text-gray-500 cursor-pointer text-[10px] hover:text-gray-300">ⓘ</span></span>
                <span class="text-purple-400 font-medium font-mono">{{ (CPUData.CpuMaxFrequency / 1000).toFixed(1) }} GHz</span>
              </div>
              <a-slider v-model="CPUData.CpuMaxFrequency" :min="1000" :max="5400" :step="100" class="w-full" />
            </div>
          </div>
        </div>
        <!-- 4. 底部全局控制栏 -->
        <div class="flex justify-between items-center pt-2">
          <button
              @click="handleReset"
              class="flex items-center gap-2 text-xs text-gray-400 hover:text-white border border-white/10 hover:border-white/20 bg-white/[0.02] hover:bg-white/[0.05] px-4 py-2 rounded-lg transition-colors"
          >
            <!-- 刷新旋转小图标 -->
            <svg class="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 1121.21 7.89M9 11l3-3 3 3m-3-3v12" />
            </svg>
            重置
          </button>

          <div class="flex gap-3">
            <button
                @click="handleCancel"
                class="text-xs text-gray-400 hover:text-white border border-white/5 bg-transparent hover:bg-white/[0.03] px-5 py-2 rounded-lg transition-colors"
            >
              取消
            </button>
            <button
                @click="handleApplyAll"
                :disabled="loading"
                class="text-xs font-medium text-white bg-gradient-to-r from-purple-700 to-indigo-600 hover:from-purple-600 hover:to-indigo-500 disabled:opacity-50 px-6 py-2 rounded-lg transition-all shadow-[0_0_15px_rgba(138,43,226,0.3)]"
            >
              {{ loading ? '应用中...' : '应用' }}
            </button>
          </div>
        </div>
      </div>

      <!-- ==================== 右侧：信息与说明栏 ==================== -->
      <div class="w-full lg:w-[360px] shrink-0 space-y-6">

        <!-- 1. CPU 信息卡片 -->
        <div class="bg-[#121320]/60 backdrop-blur-md border border-white/[0.05] rounded-xl p-5 shadow-lg">
          <h2 class="text-[13px] font-semibold text-gray-300 mb-4">CPU 信息</h2>
          <div class="flex items-center gap-4">
            <!-- 高保真 3D 芯片矢量线稿 -->
            <div class="w-16 h-16 bg-white/[0.02] border border-white/[0.05] rounded-xl flex items-center justify-center relative">
              <svg class="w-12 h-12 text-purple-500/80 opacity-80" viewBox="0 0 100 100" fill="none" stroke="currentColor" stroke-width="1.5">
                <polygon points="50,15 85,35 50,55 15,35" stroke-width="2" />
                <polygon points="50,22 81,40 50,58 19,40" class="opacity-40" />
                <polygon points="50,28 68,38 50,48 32,38" fill="rgba(138,43,226,0.15)" stroke-width="1.5" />
                <path d="M15,35 L15,40 L50,60 L85,40 L85,35" />
                <path d="M50,55 L50,60" />
                <path d="M25,38 L35,43" stroke-width="1" class="opacity-20" />
                <path d="M75,38 L65,43" stroke-width="1" class="opacity-20" />
              </svg>
            </div>

            <div class="space-y-1 text-[11px] text-gray-400">
              <div class="text-[13px] font-bold text-white">Intel Core i7-12700H</div>
              <div>Alder Lake</div>
              <div>14 核心 / 20 线程</div>
              <div>基础频率 2.3 GHz</div>
            </div>
          </div>
        </div>

        <!-- 2. 实时状态卡片 -->
        <div class="bg-[#121320]/60 backdrop-blur-md border border-white/[0.05] rounded-xl p-5 shadow-lg space-y-4">
          <h2 class="text-[13px] font-semibold text-gray-300">实时状态</h2>

          <div class="space-y-3.5">
            <!-- 频率 -->
            <div class="space-y-1.5">
              <div class="flex justify-between text-[11px]">
                <span class="text-gray-400">频率</span>
                <span class="text-white font-mono font-medium">3.60 GHz</span>
              </div>
              <div class="h-1.5 bg-white/[0.03] rounded-full overflow-hidden">
                <div class="h-full bg-[#8A2BE2]" style="width: 76%"></div>
              </div>
            </div>

            <!-- 电压 -->
            <div class="space-y-1.5">
              <div class="flex justify-between text-[11px]">
                <span class="text-gray-400">电压</span>
                <span class="text-white font-mono font-medium">1.120 V</span>
              </div>
              <div class="h-1.5 bg-white/[0.03] rounded-full overflow-hidden">
                <div class="h-full bg-[#8A2BE2]" style="width: 50%"></div>
              </div>
            </div>

            <!-- 功耗 -->
            <div class="space-y-1.5">
              <div class="flex justify-between text-[11px]">
                <span class="text-gray-400">功耗</span>
                <span class="text-white font-mono font-medium">38 W</span>
              </div>
              <div class="h-1.5 bg-white/[0.03] rounded-full overflow-hidden">
                <div class="h-full bg-[#3B82F6]" style="width: 32%"></div>
              </div>
            </div>

            <!-- 温度 -->
            <div class="space-y-1.5">
              <div class="flex justify-between text-[11px]">
                <span class="text-gray-400">温度</span>
                <span class="text-white font-mono font-medium">58 °C</span>
              </div>
              <div class="h-1.5 bg-white/[0.03] rounded-full overflow-hidden">
                <div class="h-full bg-[#8A2BE2]" style="width: 58%"></div>
              </div>
            </div>
          </div>
        </div>

        <!-- 3. 核心分布卡片 -->
        <div class="bg-[#121320]/60 backdrop-blur-md border border-white/[0.05] rounded-xl p-5 shadow-lg space-y-3.5">
          <div class="flex justify-between items-center">
            <h2 class="text-[13px] font-semibold text-gray-300">核心分布</h2>
            <button class="bg-white/[0.04] border border-white/10 hover:bg-white/[0.08] text-[10px] text-gray-400 hover:text-white px-2 py-0.5 rounded transition">详情</button>
          </div>

          <div class="space-y-2">
            <!-- P-Cores 性能核（6个，占满整行） -->
            <div class="grid grid-cols-6 gap-1.5">
              <div v-for="i in 6" :key="'p'+i" class="bg-purple-950/20 border border-purple-500/25 rounded-lg py-2 text-center">
                <div class="text-[8px] text-purple-400 leading-none">P</div>
                <div class="text-xs text-purple-300 font-bold font-mono mt-0.5">{{ String(i).padStart(2, '0') }}</div>
              </div>
            </div>
          </div>
        </div>

        <!-- 4. 说明卡片 -->
        <div class="bg-[#121320]/60 backdrop-blur-md border border-white/[0.05] rounded-xl p-5 shadow-lg space-y-2.5">
          <h2 class="text-[13px] font-semibold text-gray-300">说明</h2>
          <div class="text-[11px] text-gray-500 leading-relaxed space-y-2">
            <p>功耗限制决定了 CPU 可持续运行的最大功耗。</p>
            <p>电压偏移可在保证稳定性的前提下降低功耗和温度。</p>
            <p>修改设置后请点击“应用”以生效。</p>
          </div>
          <div class="text-[11px] text-blue-400 hover:text-blue-300 cursor-pointer pt-1 flex items-center gap-0.5 font-medium transition-colors">
            了解更多 <span>&gt;</span>
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
/* 隐藏默认滚动条 */
.no-scrollbar::-webkit-scrollbar {
  display: none;
}
.no-scrollbar {
  -ms-overflow-style: none;
  scrollbar-width: none;
}

/* 深度重写 Arco Slider 为高透炫光紫色 */
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

/* 深度重写 Arco Switch */
:deep(.arco-switch-checked) {
  background-color: #8A2BE2 !important;
}

/* 重写下拉菜单为深色模式样式 */
:deep(.select-dark .arco-select-view-single) {
  background-color: #17192a !important;
  border: 1px solid rgba(255, 255, 255, 0.05) !important;
  color: #ffffff !important;
  border-radius: 8px !important;
  height: 32px !important;
}
</style>