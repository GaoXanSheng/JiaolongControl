<script setup lang="ts">
import LogoLight from './Settings/components/LogoLight.vue'
import BootAutoStart from "./Settings/components/BootAutoStart.vue";
import AdvancedFanControlSystem from "./Settings/components/AdvancedFanControlSystem.vue";
import BootCPUAutoStart from "./Settings/components/BootCPUAutoStart.vue";
import BootGPUAutoStart from "./Settings/components/BootGPUAutoStart.vue";
import BootCurveOptimizerAllAutoStart from "./Settings/components/BootCurveOptimizerAllAutoStart.vue";
import FanCurveMerge from "./Settings/components/FanCurveMerge.vue";
import GPUDirectConnection from "./Settings/components/GPUDirectConnection.vue";
import PawnIODriverMode from "./Settings/components/PawnIODriverMode.vue";
</script>   

<template>
  <div class="p-6 h-full overflow-y-auto space-y-5 text-white no-scrollbar pb-20">
    <!-- Header -->
    <div class="max-w-[1000px] mx-auto">
      <h1 class="text-2xl font-bold tracking-wider">系统设置</h1>
      <p class="text-[13px] text-gray-500 mt-1.5">管理 JiaoLongControl 的全局参数、自启动行为及显示偏好。</p>
    </div>

    <!-- Setting Grid -->
    <div class="max-w-[1000px] mx-auto grid grid-cols-1 gap-4 pt-4">
      
      <!-- 通用设置 -->
      <BootAutoStart />
      <LogoLight />
      
      <!-- 高级硬件控制 -->
      <GPUDirectConnection />
      <AdvancedFanControlSystem />
      <FanCurveMerge />
      <PawnIODriverMode />

      <!-- 自动应用策略 -->
      <BootCPUAutoStart />
      <BootGPUAutoStart />
      <BootCurveOptimizerAllAutoStart />

    </div>
  </div>
</template>

<style scoped>
/* Scoped styles can remain empty if all styling is handled by Tailwind classes */
</style>
