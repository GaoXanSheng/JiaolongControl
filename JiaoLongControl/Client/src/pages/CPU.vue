<script setup lang="ts">
import { ref, computed } from 'vue'
import { Message } from '@arco-design/web-vue'
import { CPU, Power, RyzenSmu } from '@/utils/bridge.ts'
import { useConfigStore } from '@/stores/config'
import { useSystemInfoStore } from '@/stores/systemInfo'

const loading = ref(false)
const configStore = useConfigStore()
const systemInfoStore = useSystemInfoStore()

if (!configStore.config) {
  await configStore.fetchConfig()
}

const cpuInfo = ref<any>(null)
const infoResult = await CPU.GetCpuInfo()
if (infoResult.Success) {
  cpuInfo.value = infoResult.Data
}

// 使用 computed 来简化对配置项的访问，并确保响应性
const CPUData = computed(() => configStore.config?.Cpu)
const cpuStats = computed(() => systemInfoStore.cpuStats)

// 页面内部交互状态
const selectedProfile = ref('default')

// 从配置恢复上次选中的档位（不覆盖已保存的值）
if (CPUData.value?.CpuProfile) {
  selectedProfile.value = CPUData.value.CpuProfile
}

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
  CPUData.value.CpuProfile = profile
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
    // 6. 设置核心电压偏移 (Curve Optimizer All)
    if (configStore.config?.Smu) {
      await RyzenSmu.SetCurveOptimizerAll(configStore.config.Smu.CurveOptimizerAll)
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
  configStore.fetchConfig() // 重新加载 store 原始配置
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
            <!-- 短时功耗限制 (PL1) -->
            <div class="space-y-2">
              <div class="flex justify-between items-center text-xs">
                <span class="text-gray-300 flex items-center gap-1">功耗限制 (PL1) <span class="text-gray-500 cursor-pointer text-[10px] hover:text-gray-300">ⓘ</span></span>
                <span class="text-purple-400 font-medium font-mono">{{ CPUData.CpuLongPower }} W</span>
              </div>
              <a-slider v-model="CPUData.CpuLongPower" :min="30" :max="255" class="w-full" />
            </div>

            <!-- 长时功耗限制 (PL2) -->
            <div class="space-y-2">
              <div class="flex justify-between items-center text-xs">
                <span class="text-gray-300 flex items-center gap-1">长时功耗限制 (PL2) <span class="text-gray-500 cursor-pointer text-[10px] hover:text-gray-300">ⓘ</span></span>
                <span class="text-purple-400 font-medium font-mono">{{ CPUData.CpuShortPower }} W</span>
              </div>
              <a-slider v-model="CPUData.CpuShortPower" :min="30" :max="255" class="w-full" />
            </div>

            <!-- 核心电压偏移 (Curve Optimizer) -->
            <div class="space-y-2">
              <div class="flex justify-between items-center text-xs">
                <span class="text-gray-300 flex items-center gap-1">核心电压偏移 (CO) <span class="text-gray-500 cursor-pointer text-[10px] hover:text-gray-300">ⓘ</span></span>
                <span class="text-purple-400 font-medium font-mono">{{ configStore.config?.Smu?.CurveOptimizerAll ?? 0 }}</span>
              </div>
              <a-slider v-model="configStore.config.Smu.CurveOptimizerAll" :min="-50" :max="50" class="w-full" />
            </div>

            <!-- CPU 温度墙 -->
            <div class="space-y-2">
              <div class="flex justify-between items-center text-xs">
                <span class="text-gray-300 flex items-center gap-1">CPU 温度墙 <span class="text-gray-500 cursor-pointer text-[10px] hover:text-gray-300">ⓘ</span></span>
                <span class="text-purple-400 font-medium font-mono">{{ CPUData.CpuTempWall }} °C</span>
              </div>
              <a-slider v-model="CPUData.CpuTempWall" :min="60" :max="105" class="w-full" />
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
      <div class="w-full lg:w-[360px] shrink-0 space-y-6 lg:pt-[115px]">

        <!-- 1. CPU 信息卡片 -->
        <div class="bg-[#121320]/60 backdrop-blur-md border border-white/[0.05] rounded-xl p-5 shadow-lg">
          <h2 class="text-[13px] font-semibold text-gray-300 mb-4">CPU 信息</h2>
          <div class="flex items-center gap-4 h-[96px]">
            <!-- 高保真 3D 芯片矢量线稿 -->
            <div class="w-16 h-16 bg-white/[0.02] border border-white/[0.05] rounded-xl flex items-center justify-center relative">
              <svg t="1784304129102" class="icon" viewBox="0 0 1024 1024" version="1.1" xmlns="http://www.w3.org/2000/svg" p-id="16169" width="200" height="200"><path d="M661.205333 130.005333c33.92-4.352 33.92-4.352 33.92 29.269334 0 9.642667-0.853333 19.413333 0.384 28.885333 0.597333 4.778667 5.674667 8.832 7.509334 13.653333 2.986667 7.978667 4.906667 16.341333 8.149333 27.434667 21.418667 1.706667 47.36-2.901333 67.328 16.64 20.266667 19.754667 17.792 45.013333 17.834667 70.912l15.786666 0.981333c2.986667 0.213333 8.106667-0.426667 8.746667 0.938667 10.410667 21.76 29.866667 9.301333 45.141333 11.861333 9.344 1.578667 19.157333 0.298667 29.866667 0.298667v36.906667c-18.090667 0-35.84-0.682667-53.504 0.341333-6.528 0.384-12.714667 5.12-19.285333 7.082667-5.12 1.536-10.709333 2.389333-16.085334 2.389333-7.722667-0.085333-11.050667 2.816-10.794666 10.752 0.426667 11.52 0.085333 23.082667 0.085333 35.84 6.528 0.341333 11.477333 0.554667 16.384 0.853333 2.986667 0.256 8.192-0.170667 8.704 1.109334 8.149333 20.522667 25.728 9.557333 39.04 11.477333 11.349333 1.621333 23.082667 0.341333 35.584 0.341333v37.290667c-19.968 0-39.210667-0.512-58.368 0.341333-4.565333 0.170667-8.874667 5.034667-13.525333 7.253334-4.096 2.005333-8.746667 5.034667-12.842667 4.650666-12.586667-1.152-16.128 4.181333-15.189333 15.786667 0.853333 9.984 0.170667 20.138667 0.170666 31.786667 6.698667 0.341333 12.330667-0.170667 17.408 1.109333 3.968 1.024 7.210667 4.522667 10.965334 6.528 4.053333 2.133333 8.32 5.12 12.544 5.290667 19.2 0.554667 38.528 0.256 58.752 0.256v37.290666c-18.517333 0-36.693333-0.213333-54.826667 0.085334-7.04 0.085333-16-2.986667-17.621333 8.96-0.256 1.493333-5.674667 2.858667-8.789334 3.157333-5.418667 0.512-10.965333 0.128-18.346666 0.128 0 14.122667-0.768 26.752 0.426666 39.168 0.384 3.242667 6.4 7.466667 10.538667 8.490667 7.253333 1.792 16.810667-2.56 18.517333 10.24 0.213333 1.450667 8.021333 2.645333 12.330667 2.730666 18.773333 0.298667 37.546667 0.128 57.514667 0.128v37.077334c-18.218667 0-36.352-0.554667-54.4 0.341333-4.693333 0.213333-9.045333 5.248-13.866667 7.338667-5.034667 2.218667-10.453333 4.906667-15.744 4.949333-9.045333 0.042667-13.226667 2.858667-12.373333 12.330667 0.512 6.016 0.256 12.16 0 18.218666-1.536 34.090667-24.448 56.533333-58.666667 57.514667-4.565333 0.128-9.173333 0.469333-13.653333 0-11.349333-1.194667-17.152 2.005333-16.298667 14.890667 0.298667 4.48-4.778667 9.258667-7.125333 14.037333-2.005333 4.138667-4.992 8.448-5.12 12.757333-0.554667 18.645333-0.256 37.376-0.256 57.344h-36.778667v-55.04c0-6.997333 0-12.202667-8.234667-17.152-5.376-3.2-5.12-15.786667-7.722666-25.557333h-44.8v15.786667c-0.085333 2.517333 0.469333 6.613333-0.725334 7.296-23.04 12.8-8.064 34.261333-11.818666 51.285333-1.578667 7.168-0.298667 14.933333-0.298667 23.552h-37.077333v-55.381333c-0.042667-6.826667 1.578667-14.08-8.746667-16.554667-2.389333-0.597333-3.84-9.472-3.797333-14.549333 0.085333-8.96-2.986667-13.354667-12.373334-12.672-5.034667 0.384-10.24 0.469333-15.232-0.042667-14.208-1.450667-24.448 1.024-21.077333 18.944 0.341333 1.877333 0.213333 5.290667-0.810667 5.802667-20.352 9.856-9.002667 28.16-11.178666 42.581333-1.493333 10.282667-0.256 20.992-0.256 32.256H439.466667c0-18.389333 0.128-36.437333-0.085334-54.442667-0.085333-7.04 1.749333-13.653333-8.362666-18.517333-4.778667-2.304-4.096-16-6.314667-26.453333-11.818667 0-24.533333-0.768-36.992 0.512-3.285333 0.341333-7.68 6.528-8.533333 10.624-1.450667 7.552 1.706667 16.298667-9.984 19.029333-1.877333 0.426667-2.901333 7.893333-2.986667 12.117333-0.298667 18.688-0.128 37.333333-0.128 56.917334h-36.949333c0-18.261333 0.512-36.309333-0.384-54.314667-0.213333-4.608-5.162667-8.874667-7.68-13.44-1.792-3.157333-4.053333-6.442667-4.522667-9.898667-0.768-5.376-0.213333-10.88-0.213333-17.493333-25.685333-2.304-51.626667 2.56-71.637334-17.706667-19.413333-19.754667-16.213333-44.586667-17.152-70.016-6.229333-0.384-11.648-0.426667-16.938666-1.109333-3.328-0.426667-9.088-1.408-9.301334-2.858667-2.346667-13.994667-12.970667-10.197333-21.376-10.325333-16.725333-0.298667-33.493333-0.085333-51.370666-0.085333v-36.693334c19.2 0 37.888 0.597333 56.533333-0.341333 5.12-0.256 9.813333-5.589333 15.061333-7.808a46.933333 46.933333 0 0 1 16.213334-4.522667c8.192-0.213333 12.117333-2.56 11.733333-11.434666-0.554667-11.52-0.128-23.04-0.128-35.626667-6.698667-0.341333-11.605333-0.597333-16.554667-0.938667-2.986667-0.213333-8.192 0.213333-8.704-1.109333-7.936-19.925333-24.917333-9.429333-37.888-11.093333-11.477333-1.493333-23.296-0.341333-36.181333-0.341334v-37.034666l57.173333-0.042667c6.4 0 13.056 1.450667 15.104-8.362667 0.469333-2.261333 9.386667-4.138667 14.250667-3.882666 10.24 0.512 13.525333-3.584 12.970667-13.354667-0.682667-11.008-0.170667-22.058667-0.170667-35.114667-7.082667 0-12.544 0.170667-17.92-0.085333-2.986667-0.128-8.277333-0.554667-8.448-1.621333-3.242667-16.938667-16.554667-9.770667-25.728-10.325334-15.573333-0.981333-31.274667-0.256-47.616-0.256v-37.461333c19.498667 0 38.229333 0.597333 56.874667-0.384 5.12-0.256 9.770667-5.888 15.061333-7.68 8.192-2.773333 16.768-4.352 27.690667-7.04 0-11.733333 0.597333-24.746667-0.512-37.546667-0.256-2.901333-6.229333-6.613333-10.154667-7.509333-7.253333-1.706667-16.554667 2.602667-18.986667-9.770667-0.426667-1.92-8.96-3.157333-13.738666-3.242666-18.688-0.384-37.418667-0.170667-56.490667-0.170667v-36.906667c18.773333 0 37.034667 0.085333 55.253333-0.042666 7.338667-0.042667 14.122667 1.152 18.218667-9.130667 1.621333-4.181333 13.952-4.096 23.125333-6.4 0.853333-23.509333-2.56-49.194667 17.621334-68.906667 19.669333-19.2 44.714667-16.213333 69.802666-16.810666l1.066667-14.464c0.213333-2.986667-0.597333-7.978667 0.853333-8.789334 21.461333-11.946667 10.794667-32 11.605334-48.341333 1.536-32.213333 2.048-33.493333 37.290666-26.538667v53.888c0.085333 7.253333-0.981333 13.44 8.874667 18.133334 4.906667 2.304 4.437333 15.957333 6.613333 25.429333h44.16c3.328-11.648-4.352-26.24 12.714667-31.402667 1.834667-0.554667 1.066667-10.453333 1.152-16 0.128-12.672 0.682667-25.429333-0.170667-38.058666-0.853333-11.733333 3.925333-15.488 15.018667-14.378667 6.485333 0.682667 13.056 0.128 22.314667 0.128 0 20.181333-0.128 39.210667 0 58.24 0.085333 6.272-0.341333 11.136 7.936 15.488 5.162667 2.688 4.906667 15.744 7.424 25.6h44.757333l0.981333-14.933333c0.170667-2.56-0.554667-6.4 0.768-7.381334 22.954667-16.64 9.386667-40.405333 10.837334-60.586666 1.109333-15.488 7.04-16.981333 19.456-16.64 11.52 0.256 21.461333-0.597333 19.413333 16.213333-1.450667 12.032-0.64 24.362667-0.128 36.522667 0.341333 8.106667-3.968 17.621333 9.472 22.186666 3.669333 1.237333 2.944 15.488 4.394667 24.832h46.634666c0-6.186667-0.213333-11.605333 0.128-17.066666 0.170667-2.858667 0.896-7.936 2.218667-8.192 15.274667-3.328 9.898667-15.317333 10.24-24.277334 0.597333-16.085333 0.170667-32.170667 0.170667-46.762666 2.176-1.792 2.56-2.346667 2.986666-2.389334zM311.722667 284.586667c-23.082667 0-28.714667 5.717333-28.714667 28.928v398.762666c0 24.277333 5.376 29.738667 29.525333 29.781334h398.378667c23.210667 0 28.458667-5.546667 28.501333-29.013334 0.085333-66.474667 0.042667-132.906667 0.042667-199.381333V312.746667c-0.042667-22.272-5.717333-28.117333-27.818667-28.16H311.722667z m27.477333 37.930666c115.541333 0.597333 231.082667 0.469333 346.624 0.085334 12.885333-0.042667 17.749333 2.901333 17.706667 16.810666-0.512 116.224-0.426667 232.490667-0.085334 348.714667 0.042667 11.648-2.986667 16-15.36 15.957333-117.077333-0.426667-234.112-0.426667-351.146666 0-13.056 0.085333-16.256-4.522667-16.128-16.768 0.128-14.549333 0.170667-29.141333 0.213333-43.733333 0.170667-43.776 0-87.552 0-131.328 0-49.749333 0.298667-99.498667-0.128-149.248 0-7.125333-0.085333-14.208-0.170667-21.333333-0.213333-14.165333 3.285333-19.242667 18.474667-19.2z m54.656 45.525334a28.885333 28.885333 0 0 0-28.501333 29.184 27.349333 27.349333 0 0 0 27.989333 27.690666 28.757333 28.757333 0 0 0 29.013333-28.842666 29.226667 29.226667 0 0 0-28.501333-28.032z" fill="#828796" p-id="16170"></path></svg>
            </div>

            <div class="space-y-1 text-[11px] text-gray-400">
              <div class="text-[13px] font-bold text-white">{{ cpuInfo?.Name || 'Unknown CPU' }}</div>
              <div>{{ cpuInfo?.Cores || 0 }} 核心 / {{ cpuInfo?.Threads || 0 }} 线程</div>
              <div>基础频率 {{ cpuInfo?.BaseFreqMhz ? (cpuInfo.BaseFreqMhz / 1000).toFixed(1) : 0 }} GHz</div>
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
                <span class="text-white font-mono font-medium">{{ cpuStats?.FrequencyMhz ? (cpuStats.FrequencyMhz / 1000).toFixed(2) : '0.00' }} GHz</span>
              </div>
              <div class="h-1.5 bg-white/[0.03] rounded-full overflow-hidden">
                <div class="h-full bg-[#8A2BE2]" :style="{ width: `${Math.min((cpuStats?.FrequencyMhz || 0) / (CPUData?.CpuMaxFrequency || 5000) * 100, 100)}%` }"></div>
              </div>
            </div>

            <!-- 电压 -->
            <div class="space-y-1.5">
              <div class="flex justify-between text-[11px]">
                <span class="text-gray-400">电压</span>
                <span class="text-white font-mono font-medium">{{ cpuStats?.Voltage ? cpuStats.Voltage.toFixed(3) : '0.000' }} V</span>
              </div>
              <div class="h-1.5 bg-white/[0.03] rounded-full overflow-hidden">
                <div class="h-full bg-[#8A2BE2]" :style="{ width: `${Math.min((cpuStats?.Voltage || 0) / 1.5 * 100, 100)}%` }"></div>
              </div>
            </div>

            <!-- 使用率 -->
            <div class="space-y-1.5">
              <div class="flex justify-between text-[11px]">
                <span class="text-gray-400">使用率</span>
                <span class="text-white font-mono font-medium">{{ cpuStats?.Usage || 0 }} %</span>
              </div>
              <div class="h-1.5 bg-white/[0.03] rounded-full overflow-hidden">
                <div class="h-full bg-[#3B82F6]" :style="{ width: `${cpuStats?.Usage || 0}%` }"></div>
              </div>
            </div>

            <!-- 温度 -->
            <div class="space-y-1.5">
              <div class="flex justify-between text-[11px]">
                <span class="text-gray-400">温度</span>
                <span class="text-white font-mono font-medium">{{ cpuStats?.Temperature || 0 }} °C</span>
              </div>
              <div class="h-1.5 bg-white/[0.03] rounded-full overflow-hidden">
                <div class="h-full bg-[#8A2BE2]" :style="{ width: `${Math.min(cpuStats?.Temperature || 0, 100)}%` }"></div>
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
            <!-- 动态渲染所有核心 -->
            <div class="grid grid-cols-6 gap-1.5">
              <div v-for="i in (cpuInfo?.Cores || 0)" :key="'core'+i" class="bg-purple-950/20 border border-purple-500/25 rounded-lg py-2 text-center">
                <div class="text-[8px] text-purple-400 leading-none">C</div>
                <div class="text-xs text-purple-300 font-bold font-mono mt-0.5">{{ String(i - 1).padStart(2, '0') }}</div>
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