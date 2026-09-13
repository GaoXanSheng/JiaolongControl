<script setup lang="ts">
import {computed} from 'vue'
import SettingToggle from '@/components/common/SettingToggle.vue'
import PawnIODriverMode from './Settings/components/PawnIODriverMode.vue'
import ThemeSetting from './Settings/components/ThemeSetting.vue'
import BootAutoStart from './Settings/components/BootAutoStart.vue'
import {useConfigStore} from '@/stores/config'
import type {JiaoLongConfigType} from '@/types/config'

const configStore = useConfigStore()

// 布尔开关卡片配置: title/description + config JSON 路径, 由 SettingToggle 统一渲染
// visibleWhen 返回 false 时该卡片不渲染
interface ToggleCard {
  title: string
  description: string
  configPath: string
  visibleWhen?: (config: JiaoLongConfigType) => boolean
}

const toggleCards: ToggleCard[] = [
  {
    title: '自启动高级风扇控制系统',
    description: '启用后，软件将在后台实时监控硬件温度，并依据【风扇曲线】页面中用户自定义的策略来动态调整风扇转速',
    configPath: 'App.BootAdvancedFanControlSystem',
  },
  {
    title: 'CPU 参数自动应用',
    description: '在软件启动时，自动载入并应用【CPU】设置页面中保存的功耗、频率、温度墙等高级参数',
    configPath: 'App.BootAdvancedCPUSystem',
  },
  {
    title: 'GPU 参数自动应用',
    description: '在软件启动时，自动载入并应用【GPU】设置页面中保存的核心与显存超频、电压曲线、功耗目标等参数',
    configPath: 'App.BootAdvancedGPUSystem',
  },
  {
    title: 'RyzenSMU 降压自动应用',
    description: '在软件启动时，自动应用【Ryzen SMU】页面中保存的 Curve Optimizer 降压设定（全核或分核，由下方选项决定）',
    configPath: 'App.BootSetRyzenSumCurveOptimizerAll',
  },
  {
    title: 'RyzenSMU 分核降压模式',
    description: '启用后，启动时逐核心应用【Ryzen SMU】页面中保存的分核心 Curve Optimizer 数值，代替全核偏移',
    configPath: 'App.BootSetRyzenSmuCurveOptimizerPerCore',
    visibleWhen: (config) => config.App.BootSetRyzenSumCurveOptimizerAll,
  },
  {
    title: '自启动键盘渐变',
    description: '启用后，开机及睡眠唤醒时自动开启键盘渐变（以启动时键盘当前颜色为锚点循环渐变）',
    configPath: 'App.BootKeyboardGradient',
  },
]

// config 未加载完成时先隐藏带 visibleWhen 条件的卡片
const visibleCards = computed(() => {
  const config = configStore.config
  return toggleCards.filter((card) => !card.visibleWhen || !config || card.visibleWhen(config))
})
</script>

<template>
  <div class="p-6 h-full overflow-y-auto space-y-5 text-ink no-scrollbar pb-20">
    <!-- Header -->
    <div class="max-w-[1000px] mx-auto">
      <h1 class="text-2xl font-bold tracking-wider">系统设置</h1>
      <p class="text-[13px] text-gray-500 mt-1.5">
        管理 JiaoLongControl 的全局参数、自启动行为及显示偏好。
      </p>
    </div>

    <!-- Setting Grid -->
    <div class="max-w-[1000px] mx-auto grid grid-cols-1 gap-4 pt-4">
      <!-- 通用设置 -->
      <ThemeSetting />

      <!-- 自启动与自动应用策略 -->
      <BootAutoStart />
      <SettingToggle
        v-for="card in visibleCards"
        :key="card.configPath"
        :title="card.title"
        :description="card.description"
        :config-path="card.configPath"
      />

      <PawnIODriverMode />
    </div>
  </div>
</template>

<style scoped>
/* Scoped styles can remain empty if all styling is handled by Tailwind classes */
</style>
