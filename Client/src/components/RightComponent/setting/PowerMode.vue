<script setup lang="ts">
import { Message } from '@arco-design/web-vue'
import WMIOperation, { PerformaceMode } from '@/utils/WMIOperation.ts'
import SettingCardComponent from '@/components/RightComponent/setting/SettingCardComponent.vue'
import { ref } from 'vue'
const loading = ref(false)
async function PowerMode_handleClick(PowerMode: PerformaceMode) {
	loading.value = true
	const result = await WMIOperation.PerformaceMode.Set(PowerMode)
	if (result == '拒绝访问') {
		Message.error(result)
	} else {
		Message.success('应用成功')
	}
	loading.value = false
}
</script>

<template>
	<setting-card-component title="模式切换">
		<template #extra>
			<a-space size="large">
				<a-dropdown>
					<a-button :loading="loading">选择</a-button>
					<template #content>
						<a-doption @click="PowerMode_handleClick(PerformaceMode.OfficeMode)"
							>办公模式
						</a-doption>
						<a-doption @click="PowerMode_handleClick(PerformaceMode.GamingMode)"
							>性能模式
						</a-doption>
						<a-doption @click="PowerMode_handleClick(PerformaceMode.RampageMode)"
							>狂飙模式
						</a-doption>
					</template>
				</a-dropdown>
			</a-space>
		</template>
	</setting-card-component>
</template>

<style scoped></style>
