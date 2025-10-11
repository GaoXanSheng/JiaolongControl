<script async setup lang="ts">
import { onMounted, ref } from 'vue'
import { HardwareMonitorInfo } from '@renderer/store/interfaces'
import CpuHeatMapData from '@renderer/components/ProSettingComponent/CpuHeatMapData.vue'
import FanSpeedData from '@renderer/components/ProSettingComponent/FanSpeedData.vue'
import GpuAmd from '@renderer/components/ProSettingComponent/GpuAmd.vue'
import GpuNvidia from '@renderer/components/ProSettingComponent/GpuNvidia.vue'
import CenterTop from '@renderer/components/ProSettingComponent/CenterTop.vue'
import WMIOperation from '@renderer/tools/WMIOperation'
import { Message } from '@arco-design/web-vue'
import useStore from '@renderer/store'
const store = useStore()

const MonitorInfo = ref<HardwareMonitorInfo>(await WMIOperation.GetHardwareMonitorInfo())
onMounted(async () => {
	setInterval(async () => {
		MonitorInfo.value = await WMIOperation.GetHardwareMonitorInfo()
	}, 3000)
	// 避免全局样式污染
	document.body.setAttribute('arco-theme', 'light')
	if (!store.$state.ServiceOption) {
		Message.info('建议安装服务后使用')
	}
})
</script>

<template>
	<div class="dashboard">
		<div class="left-top">
			<!-- 左上区域 -->
			<GpuAmd v-if="MonitorInfo.GpuAmd != null" :data="MonitorInfo"></GpuAmd>
		</div>
		<div class="center-top">
			<!-- 中上区域 -->
			<CenterTop></CenterTop>
		</div>
		<div class="right">
			<GpuNvidia :data="MonitorInfo"></GpuNvidia>
		</div>
		<div class="left-bottom">
			<!-- 左下区域 -->
			<FanSpeedData></FanSpeedData>
		</div>
		<div class="center-bottom">
			<!-- 中下区域 -->
			<CpuHeatMapData :data="MonitorInfo"></CpuHeatMapData>
		</div>
	</div>
</template>

<style lang="scss" scoped>
$background-color: #f3f4f6ff;
$panel-bg: #ffffff;

.dashboard {
	position: relative;
	width: 100vw;
	height: 100vh;
	background-color: $background-color;

	.panel {
		position: absolute;
	}

	.left-top {
		@extend .panel;
		top: 0;
		left: 0;
		width: 20vw;
		height: 80vh;
	}

	.left-bottom {
		@extend .panel;
		top: 80vh;
		left: 0;
		width: 20vw;
		height: 20vh;
	}

	.center-top {
		@extend .panel;
		top: 0;
		left: 20vw;
		width: 55vw;
		height: 75vh;
		background-color: $panel-bg;
		border-radius: 5px;
	}

	.center-bottom {
		@extend .panel;
		top: 75vh;
		left: 20vw;
		width: 55vw;
		height: 25vh;
		display: flex;
		flex-direction: column;
		justify-content: space-between;
	}

	.right {
		@extend .panel;
		top: 0;
		left: 75vw;
		width: 25vw;
		height: 100vh;
	}
}
</style>
