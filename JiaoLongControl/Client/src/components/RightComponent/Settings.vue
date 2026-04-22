<script setup lang="ts">
import LogoLight from '@/components/RightComponent/setting/LogoLight.vue'
import BootAutoStart from "@/components/RightComponent/setting/BootAutoStart.vue";
import PowerMode from "@/components/RightComponent/setting/PowerMode.vue";
import AdvancedFanControlSystem from "@/components/RightComponent/setting/AdvancedFanControlSystem.vue";
import BootCPUAutoStart from "@/components/RightComponent/setting/BootCPUAutoStart.vue";
import BootGPUAutoStart from "@/components/RightComponent/setting/BootGPUAutoStart.vue";
import BootCurveOptimizerAllAutoStart from "@/components/RightComponent/setting/BootCurveOptimizerAllAutoStart.vue";
</script>

<template>
  <div class="setting">
    <a-row justify="center">
      <a-col :span="16">
        <a-typography-title class="title">设置</a-typography-title>
      </a-col>
      <BootAutoStart><</BootAutoStart>
      <LogoLight></LogoLight>
      <PowerMode></PowerMode>
      <AdvancedFanControlSystem></AdvancedFanControlSystem>
      <BootCPUAutoStart></BootCPUAutoStart>
      <BootGPUAutoStart></BootGPUAutoStart>
      <BootCurveOptimizerAllAutoStart></BootCurveOptimizerAllAutoStart>
    </a-row>
  </div>
</template>

<style lang="scss" scoped>
.setting {
  padding-top: 20px;
}
</style>
