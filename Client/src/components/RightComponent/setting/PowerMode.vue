<script setup lang="ts">
import { Message } from '@arco-design/web-vue'
import bridge, {PerformanceMode, SystemPerMode} from '@/utils/bridge.ts'
import SettingCardComponent from '@/components/RightComponent/setting/SettingCardComponent.vue'
import { ref } from 'vue'
const loading = ref(false)
async function PowerMode_handleClick(PowerMode: SystemPerMode) {
	loading.value = true
	const result = await bridge.PerformanceMode.Set(PowerMode)
	if (result) {
    Message.success('应用成功')
	} else {
    Message.error('应用失败')
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
						<a-doption @click="PowerMode_handleClick(SystemPerMode.BalanceMode)"
							>办公模式
						</a-doption>
						<a-doption @click="PowerMode_handleClick(SystemPerMode.PerformanceMode)"
							>性能模式
						</a-doption>
						<a-doption @click="PowerMode_handleClick(SystemPerMode.QuietMode)"
							>狂飙模式
						</a-doption>
					</template>
				</a-dropdown>
			</a-space>
		</template>
	</setting-card-component>
</template>

<style scoped></style>
