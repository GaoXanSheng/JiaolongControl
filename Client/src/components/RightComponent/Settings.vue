<script setup lang="ts">
import GPUDirectConnection from '@/components/RightComponent/setting/GPUDirectConnection.vue'
import LogoLight from '@/components/RightComponent/setting/LogoLight.vue'
import BootAutoStart from "@/components/RightComponent/setting/BootAutoStart.vue";
import PowerMode from "@/components/RightComponent/setting/PowerMode.vue";
import AdvancedFanControlSystem from "@/components/RightComponent/setting/AdvancedFanControlSystem.vue";
</script>

<template>
  <div class="setting">
    <a-row justify="center">
      <a-col :span="16">
        <a-typography-title class="title"> Settings</a-typography-title>
      </a-col>
      <BootAutoStart><</BootAutoStart>
      <LogoLight></LogoLight>
      <GPUDirectConnection></GPUDirectConnection>
      <PowerMode></PowerMode>
      <AdvancedFanControlSystem></AdvancedFanControlSystem>
    </a-row>
  </div>
</template>

<style lang="scss" scoped>
.setting {
  padding-top: 20px;

  .title {
    text-align: left;
  }
}
</style>
