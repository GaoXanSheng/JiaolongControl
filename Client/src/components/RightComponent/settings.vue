<script setup lang="ts">
import RgbEventLoop from '@/components/RightComponent/setting/RgbEventLoop.vue'
import FanSpeedSwitch from '@/components/RightComponent/setting/FanSpeedSwitch.vue'
import GPUDirectConnection from '@/components/RightComponent/setting/GPUDirectConnection.vue'
import LogoLight from '@/components/RightComponent/setting/LogoLight.vue'
import Theme from '@/components/RightComponent/setting/theme.vue'
import PowerMode from '@/components/RightComponent/setting/PowerMode.vue'
import CpuBoost from '@/components/RightComponent/setting/CPUBoost.vue'
import InstallService from '@/components/RightComponent/setting/InstallService.vue'
</script>

<template>
	<div class="setting">
		<a-row justify="center">
			<a-col :span="16">
				<a-typography-title class="title"> Settings </a-typography-title>
			</a-col>
<!--			<rgb-event-loop></rgb-event-loop>-->
			<FanSpeedSwitch></FanSpeedSwitch>
			<GPUDirectConnection></GPUDirectConnection>
			<LogoLight></LogoLight>
			<CpuBoost></CpuBoost>
<!--			<theme></theme>-->
<!--			<power-mode></power-mode>-->
<!--			<install-service></install-service>-->
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
