<script setup lang="ts">
import CpuDie from '@/components/common/CpuDie.vue'

import { ref, computed } from 'vue'
import { Message } from '@arco-design/web-vue'
import { CPU, Power, RyzenSmu, type CpuInfo } from '@/utils/bridge.ts'
import { useConfigStore } from '@/stores/config'
import { useSystemInfoStore } from '@/stores/systemInfo'
import type { CpuProfileDataType } from '@/types/config'

const loading = ref(false)
const configStore = useConfigStore()
const systemInfoStore = useSystemInfoStore()

if (!configStore.config) {
  await configStore.fetchConfig()
}

const cpuInfo = ref<CpuInfo | null>(null)
const infoResult = await CPU.GetCpuInfo()
if (infoResult.Success) {
  cpuInfo.value = infoResult.Data
}

// 使用 computed 来简化对配置项的访问，并确保响应性
const CPUData = computed(() => configStore.config?.Cpu)
const SmuData = computed(() => configStore.config?.Smu)
const cpuStats = computed(() => systemInfoStore.cpuStats)

// 页面内部交互状态
const selectedProfile = ref('default')

// 从配置恢复上次选中的档位（不覆盖已保存的值）
if (CPUData.value?.CpuProfile) {
  selectedProfile.value = CPUData.value.CpuProfile
}

// 档位名 -> 配置块字段名（config.yaml 中 Cpu 下的 Default/Performance/Saving/Custom）
function profileKey(profile: string): 'Default' | 'Performance' | 'Saving' | 'Custom' {
  const map: Record<string, 'Default' | 'Performance' | 'Saving' | 'Custom'> = {
    default: 'Default',
    performance: 'Performance',
    saving: 'Saving',
    custom: 'Custom',
  }
  return map[profile] ?? 'Default'
}

// 当前选中档位的参数块（滑块直接绑定它；切换档位即切换绑定的数据源）
const activeProfile = computed<CpuProfileDataType>(() => {
  const cpu = CPUData.value
  return (cpu?.[profileKey(selectedProfile.value)] ?? cpu?.Default) as CpuProfileDataType
})

// 切换档位：只改选中标记，界面滑块绑定的 activeProfile 随之指向该档位块
function selectProfile(profile: string) {
  selectedProfile.value = profile
  if (CPUData.value) CPUData.value.CpuProfile = profile
}

// 统一应用逻辑
async function handleApplyAll() {
  if (!CPUData.value || !activeProfile.value) return
  loading.value = true
  try {
    // 1. 设置长时功耗限制 (PL1)
    await CPU.SetCpuLongPower(activeProfile.value.CpuLongPower)
    // 2. 设置短时功耗限制 (PL2)
    await CPU.SetCpuShortPower(activeProfile.value.CpuShortPower)
    // 3. 设置温度墙
    await CPU.SetCPUTempWall(activeProfile.value.CpuTempWall)
    // 4. 设置最大频率
    await Power.SetCPUMaxFrequency(activeProfile.value.CpuMaxFrequency)
    // 5. 设置睿频开关
    if (activeProfile.value.CpuTurbo) {
      await Power.EnableTurbo()
    } else {
      await Power.DisableTurbo()
    }
    // 6. 设置核心电压偏移 (Curve Optimizer All)
    if (configStore.config?.Smu) {
      await RyzenSmu.SetCurveOptimizerAll(configStore.config.Smu.CurveOptimizerAll)
    }

    // 7. 保存主配置（含当前档位块参数与选中档位，供开机自启等使用）
    const saveRes = await configStore.saveConfig()
    if (saveRes?.Success) {
      Message.success('设置应用成功')
    } else {
      Message.error(saveRes?.Message || '设置保存失败')
    }
  } catch {
    Message.error('应用设置失败，请检查桥接服务。')
  } finally {
    loading.value = false
  }
}

// 重置到默认档位
function handleReset() {
  selectProfile('default')
  Message.info('参数已重置为默认配置')
}

// 取消修改：强制从后端重新加载原始配置
async function handleCancel() {
  await configStore.fetchConfig(true) // 重新加载 store 原始配置
  Message.info('已取消修改')
}
</script>

<template>
  <div v-if="CPUData" class="h-full overflow-y-auto text-white p-6 no-scrollbar">
    <div class="max-w-[1300px] mx-auto flex flex-col lg:flex-row gap-6">
      <!-- ==================== 左/中：CPU 设置区域 ==================== -->
      <div class="flex-1 space-y-6">
        <!-- 头部标题 -->
        <div>
          <h1 class="text-2xl font-bold tracking-wide">CPU 设置</h1>
          <p class="text-[13px] text-gray-500 mt-1">调整 CPU 的性能参数，发挥处理器最佳性能。</p>
        </div>

        <!-- 1. CPU 配置文件 -->
        <div
          class="bg-[#121320]/60 backdrop-blur-md border border-white/[0.05] rounded-xl p-5 shadow-lg"
        >
          <div class="flex justify-between items-center mb-4">
            <h2 class="text-[13px] font-semibold text-gray-300">CPU 配置文件</h2>
          </div>

          <div class="grid grid-cols-2 md:grid-cols-4 gap-3">
            <!-- 默认配置 -->
            <div
              :class="[
                'border rounded-xl p-4 cursor-pointer transition-all duration-300 flex flex-col justify-between h-[96px]',
                selectedProfile === 'default'
                  ? 'border-[#8A2BE2] bg-[#1a182f] shadow-[0_0_15px_rgba(138,43,226,0.25)]'
                  : 'border-white/[0.05] bg-[#121320] hover:border-white/10',
              ]"
              @click="selectProfile('default')"
            >
              <span
                class="text-xs font-semibold"
                :class="selectedProfile === 'default' ? 'text-white' : 'text-gray-300'"
                >默认配置</span
              >
              <span class="text-[11px] text-gray-500">平衡性能与功耗</span>
            </div>

            <!-- 高性能模式 -->
            <div
              :class="[
                'border rounded-xl p-4 cursor-pointer transition-all duration-300 flex flex-col justify-between h-[96px]',
                selectedProfile === 'performance'
                  ? 'border-[#8A2BE2] bg-[#1a182f] shadow-[0_0_15px_rgba(138,43,226,0.25)]'
                  : 'border-white/[0.05] bg-[#121320] hover:border-white/10',
              ]"
              @click="selectProfile('performance')"
            >
              <span
                class="text-xs font-semibold"
                :class="selectedProfile === 'performance' ? 'text-white' : 'text-gray-300'"
                >高性能模式</span
              >
              <span class="text-[11px] text-gray-500">释放最大性能</span>
            </div>

            <!-- 节能模式 -->
            <div
              :class="[
                'border rounded-xl p-4 cursor-pointer transition-all duration-300 flex flex-col justify-between h-[96px]',
                selectedProfile === 'saving'
                  ? 'border-[#8A2BE2] bg-[#1a182f] shadow-[0_0_15px_rgba(138,43,226,0.25)]'
                  : 'border-white/[0.05] bg-[#121320] hover:border-white/10',
              ]"
              @click="selectProfile('saving')"
            >
              <span
                class="text-xs font-semibold"
                :class="selectedProfile === 'saving' ? 'text-white' : 'text-gray-300'"
                >节能模式</span
              >
              <span class="text-[11px] text-gray-500">降低功耗与温度</span>
            </div>

            <!-- 自定义配置 -->
            <div
              :class="[
                'border rounded-xl p-4 cursor-pointer transition-all duration-300 flex flex-col justify-between h-[96px]',
                selectedProfile === 'custom'
                  ? 'border-[#8A2BE2] bg-[#1a182f] shadow-[0_0_15px_rgba(138,43,226,0.25)]'
                  : 'border-white/[0.05] bg-[#121320] hover:border-white/10',
              ]"
              @click="selectProfile('custom')"
            >
              <span
                class="text-xs font-semibold"
                :class="selectedProfile === 'custom' ? 'text-white' : 'text-gray-300'"
                >自定义配置</span
              >
              <span class="text-[11px] text-gray-500">自定义参数设置</span>
            </div>
          </div>
        </div>

        <!-- 2. 核心设置 -->
        <div
          class="bg-[#121320]/60 backdrop-blur-md border border-white/[0.05] rounded-xl p-5 shadow-lg space-y-6"
        >
          <h2 class="text-[13px] font-semibold text-gray-300">核心设置</h2>

          <div class="space-y-6">
            <!-- 短时功耗限制 (PL1) -->
            <div class="space-y-2">
              <div class="flex justify-between items-center text-xs">
                <span class="text-gray-300 flex items-center gap-1"
                  >功耗限制 (PL1)
                  <span class="text-gray-500 cursor-pointer text-[10px] hover:text-gray-300"
                    >ⓘ</span
                  ></span
                >
                <span class="text-purple-400 font-medium font-mono"
                  >{{ activeProfile.CpuLongPower }} W</span
                >
              </div>
              <a-slider v-model="activeProfile.CpuLongPower" :min="30" :max="255" class="w-full" />
            </div>

            <!-- 长时功耗限制 (PL2) -->
            <div class="space-y-2">
              <div class="flex justify-between items-center text-xs">
                <span class="text-gray-300 flex items-center gap-1"
                  >长时功耗限制 (PL2)
                  <span class="text-gray-500 cursor-pointer text-[10px] hover:text-gray-300"
                    >ⓘ</span
                  ></span
                >
                <span class="text-purple-400 font-medium font-mono"
                  >{{ activeProfile.CpuShortPower }} W</span
                >
              </div>
              <a-slider v-model="activeProfile.CpuShortPower" :min="30" :max="255" class="w-full" />
            </div>

            <!-- 核心电压偏移 (Curve Optimizer) -->
            <div class="space-y-2">
              <div class="flex justify-between items-center text-xs">
                <span class="text-gray-300 flex items-center gap-1"
                  >核心电压偏移 (CO)
                  <span class="text-gray-500 cursor-pointer text-[10px] hover:text-gray-300"
                    >ⓘ</span
                  ></span
                >
                <span class="text-purple-400 font-medium font-mono">{{
                  configStore.config?.Smu?.CurveOptimizerAll ?? 0
                }}</span>
              </div>
              <a-slider
                v-if="SmuData"
                v-model="SmuData.CurveOptimizerAll"
                :min="-50"
                :max="50"
                class="w-full"
              />
            </div>

            <!-- CPU 温度墙 -->
            <div class="space-y-2">
              <div class="flex justify-between items-center text-xs">
                <span class="text-gray-300 flex items-center gap-1"
                  >CPU 温度墙
                  <span class="text-gray-500 cursor-pointer text-[10px] hover:text-gray-300"
                    >ⓘ</span
                  ></span
                >
                <span class="text-purple-400 font-medium font-mono"
                  >{{ activeProfile.CpuTempWall }} °C</span
                >
              </div>
              <a-slider v-model="activeProfile.CpuTempWall" :min="60" :max="105" class="w-full" />
            </div>

            <!-- 最大睿频频率 -->
            <div class="space-y-2">
              <div class="flex justify-between items-center text-xs">
                <span class="text-gray-300 flex items-center gap-1"
                  >最大睿频频率
                  <span class="text-gray-500 cursor-pointer text-[10px] hover:text-gray-300"
                    >ⓘ</span
                  ></span
                >
                <span class="text-purple-400 font-medium font-mono"
                  >{{ (activeProfile.CpuMaxFrequency / 1000).toFixed(1) }} GHz</span
                >
              </div>
              <a-slider
                v-model="activeProfile.CpuMaxFrequency"
                :min="1000"
                :max="5400"
                :step="100"
                class="w-full"
              />
            </div>
          </div>
        </div>
        <!-- 4. 底部全局控制栏 -->
        <div class="flex justify-between items-center pt-2">
          <button
            class="flex items-center gap-2 text-xs text-gray-400 hover:text-white border border-white/10 hover:border-white/20 bg-white/[0.02] hover:bg-white/[0.05] px-4 py-2 rounded-lg transition-colors"
            @click="handleReset"
          >
            <!-- 刷新旋转小图标 -->
            <svg class="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path
                stroke-linecap="round"
                stroke-linejoin="round"
                stroke-width="2"
                d="M4 4v5h.582m15.356 2A8.001 8.001 0 1121.21 7.89M9 11l3-3 3 3m-3-3v12"
              />
            </svg>
            重置
          </button>

          <div class="flex gap-3">
            <button
              class="text-xs text-gray-400 hover:text-white border border-white/5 bg-transparent hover:bg-white/[0.03] px-5 py-2 rounded-lg transition-colors"
              @click="handleCancel"
            >
              取消
            </button>
            <button
              :disabled="loading"
              class="text-xs font-medium text-white bg-gradient-to-r from-purple-700 to-indigo-600 hover:from-purple-600 hover:to-indigo-500 disabled:opacity-50 px-6 py-2 rounded-lg transition-all shadow-[0_0_15px_rgba(138,43,226,0.3)]"
              @click="handleApplyAll"
            >
              {{ loading ? '应用中...' : '应用' }}
            </button>
          </div>
        </div>
      </div>

      <!-- ==================== 右侧：信息与说明栏 ==================== -->
      <div class="w-full lg:w-[360px] shrink-0 space-y-6 lg:pt-[115px]">
        <!-- 1. CPU 信息卡片 -->
        <div
          class="bg-[#121320]/60 backdrop-blur-md border border-white/[0.05] rounded-xl p-5 shadow-lg"
        >
          <h2 class="text-[13px] font-semibold text-gray-300 mb-4">CPU 信息</h2>
          <div class="flex items-center gap-4 h-[96px]">
            <!-- 高保真 3D 芯片矢量线稿 (CpuDie) -->
            <div
              class="w-16 h-16 bg-white/[0.02] border border-white/[0.05] rounded-xl flex items-center justify-center relative"
            >
                            <CpuDie />
            </div>

            <div class="space-y-1 text-[11px] text-gray-400">
              <div class="text-[13px] font-bold text-white">
                {{ cpuInfo?.Name || 'Unknown CPU' }}
              </div>
              <div>{{ cpuInfo?.Cores || 0 }} 核心 / {{ cpuInfo?.Threads || 0 }} 线程</div>
              <div>
                基础频率
                {{ cpuInfo?.BaseFreqMhz ? (cpuInfo.BaseFreqMhz / 1000).toFixed(1) : 0 }} GHz
              </div>
            </div>
          </div>
        </div>

        <!-- 2. 实时状态卡片 -->
        <div
          class="bg-[#121320]/60 backdrop-blur-md border border-white/[0.05] rounded-xl p-5 shadow-lg space-y-4"
        >
          <h2 class="text-[13px] font-semibold text-gray-300">实时状态</h2>

          <div class="space-y-3.5">
            <!-- 频率 -->
            <div class="space-y-1.5">
              <div class="flex justify-between text-[11px]">
                <span class="text-gray-400">频率</span>
                <span class="text-white font-mono font-medium"
                  >{{
                    cpuStats?.FrequencyMhz ? (cpuStats.FrequencyMhz / 1000).toFixed(2) : '0.00'
                  }}
                  GHz</span
                >
              </div>
              <div class="h-1.5 bg-white/[0.03] rounded-full overflow-hidden">
                <div
                  class="h-full bg-[#8A2BE2]"
                  :style="{
                    width: `${Math.min(((cpuStats?.FrequencyMhz || 0) / (activeProfile.CpuMaxFrequency || 5000)) * 100, 100)}%`,
                  }"
                ></div>
              </div>
            </div>

            <!-- 电压 -->
            <div class="space-y-1.5">
              <div class="flex justify-between text-[11px]">
                <span class="text-gray-400">电压</span>
                <span class="text-white font-mono font-medium"
                  >{{ cpuStats?.Voltage ? cpuStats.Voltage.toFixed(3) : '0.000' }} V</span
                >
              </div>
              <div class="h-1.5 bg-white/[0.03] rounded-full overflow-hidden">
                <div
                  class="h-full bg-[#8A2BE2]"
                  :style="{ width: `${Math.min(((cpuStats?.Voltage || 0) / 1.5) * 100, 100)}%` }"
                ></div>
              </div>
            </div>

            <!-- 使用率 -->
            <div class="space-y-1.5">
              <div class="flex justify-between text-[11px]">
                <span class="text-gray-400">使用率</span>
                <span class="text-white font-mono font-medium">{{ cpuStats?.Usage || 0 }} %</span>
              </div>
              <div class="h-1.5 bg-white/[0.03] rounded-full overflow-hidden">
                <div
                  class="h-full bg-[#3B82F6]"
                  :style="{ width: `${cpuStats?.Usage || 0}%` }"
                ></div>
              </div>
            </div>

            <!-- 温度 -->
            <div class="space-y-1.5">
              <div class="flex justify-between text-[11px]">
                <span class="text-gray-400">温度</span>
                <span class="text-white font-mono font-medium"
                  >{{ cpuStats?.Temperature || 0 }} °C</span
                >
              </div>
              <div class="h-1.5 bg-white/[0.03] rounded-full overflow-hidden">
                <div
                  class="h-full bg-[#8A2BE2]"
                  :style="{ width: `${Math.min(cpuStats?.Temperature || 0, 100)}%` }"
                ></div>
              </div>
            </div>
          </div>
        </div>

        <!-- 3. 核心分布卡片 -->
        <div
          class="bg-[#121320]/60 backdrop-blur-md border border-white/[0.05] rounded-xl p-5 shadow-lg space-y-3.5"
        >
          <div class="flex justify-between items-center">
            <h2 class="text-[13px] font-semibold text-gray-300">核心分布</h2>
            <button
              class="bg-white/[0.04] border border-white/10 hover:bg-white/[0.08] text-[10px] text-gray-400 hover:text-white px-2 py-0.5 rounded transition"
            >
              详情
            </button>
          </div>

          <div class="space-y-2">
            <!-- 动态渲染所有核心 -->
            <div class="grid grid-cols-6 gap-1.5">
              <div
                v-for="i in cpuInfo?.Cores || 0"
                :key="'core' + i"
                class="bg-purple-950/20 border border-purple-500/25 rounded-lg py-2 text-center"
              >
                <div class="text-[8px] text-purple-400 leading-none">C</div>
                <div class="text-xs text-purple-300 font-bold font-mono mt-0.5">
                  {{ String(i - 1).padStart(2, '0') }}
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- 4. 说明卡片 -->
        <div
          class="bg-[#121320]/60 backdrop-blur-md border border-white/[0.05] rounded-xl p-5 shadow-lg space-y-2.5"
        >
          <h2 class="text-[13px] font-semibold text-gray-300">说明</h2>
          <div class="text-[11px] text-gray-500 leading-relaxed space-y-2">
            <p>功耗限制决定了 CPU 可持续运行的最大功耗。</p>
            <p>电压偏移可在保证稳定性的前提下降低功耗和温度。</p>
            <p>修改设置后请点击“应用”以生效。</p>
          </div>
          <div
            class="text-[11px] text-blue-400 hover:text-blue-300 cursor-pointer pt-1 flex items-center gap-0.5 font-medium transition-colors"
          >
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


/* 深度重写 Arco Switch */
:deep(.arco-switch-checked) {
  background-color: #8a2be2 !important;
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
