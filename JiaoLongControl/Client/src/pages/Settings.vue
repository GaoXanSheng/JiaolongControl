<script setup lang="ts">
import SettingToggle from '@/components/common/SettingToggle.vue'
import LogoLight from './Settings/components/LogoLight.vue'
import GPUDirectConnection from './Settings/components/GPUDirectConnection.vue'
import PawnIODriverMode from './Settings/components/PawnIODriverMode.vue'
import ThemeSetting from './Settings/components/ThemeSetting.vue'

// 布尔开关卡片配置: title/description + config JSON 路径, 由 SettingToggle 统一渲染
const toggleCards = [
  {
    title: '开机自启',
    description: '允许 JiaoLongControl 随 Windows 操作系统启动而自动运行，确保各类硬件优化策略与自定义风扇曲线实时生效。',
    configPath: 'App.BootMinimized',
  },
  {
    title: '自启动高级风扇控制系统',
    description: '启用后，软件将在后台实时监控硬件温度，并依据【风扇曲线】页面中用户自定义的策略来动态调整风扇转速',
    configPath: 'App.BootAdvancedFanControlSystem',
  },
  {
    title: '风扇曲线合并',
    description: '启用后，软件将在【风扇曲线】页面中将所有风扇的曲线合并为一条曲线，方便用户统一调整风扇转速',
    configPath: 'Fan.FanCurveMerge',
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
    title: 'RyzenSMU 全核降压自动应用',
    description: '在软件启动时，自动应用【Ryzen SMU】页面中保存的 Curve Optimizer 全核心负压（降压超频）设定',
    configPath: 'App.BootSetRyzenSumCurveOptimizerAll',
  },
  {
    title: '自启动键盘渐变',
    description: '启用后，开机及睡眠唤醒时自动开启键盘渐变（以启动时键盘当前颜色为锚点循环渐变）',
    configPath: 'App.BootKeyboardGradient',
  },
]
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
      <LogoLight />
      <GPUDirectConnection />

      <!-- 自启动与自动应用策略 -->
      <SettingToggle
        v-for="card in toggleCards"
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
