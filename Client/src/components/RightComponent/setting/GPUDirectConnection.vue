<script async setup lang="ts">
import { ref } from 'vue'
import WMIOperation from '@/utils/WMIOperation.ts'
import { Message } from '@arco-design/web-vue'
import SettingCardComponent from '@/components/RightComponent/setting/SettingCardComponent.vue'
const loading = ref(false)
const GPUDirectConnection = ref((await WMIOperation.GPUMode.Get()) == 0)
async function GPUDirectConnection_handleClick() {
	loading.value = true
	const result = await WMIOperation.GPUMode.Set(GPUDirectConnection.value ? 0 : 1)
	if (result == '拒绝访问') {
		GPUDirectConnection.value = false
		Message.error(result)
	} else {
		Message.success('应用成功')
		Message.info('独显直连应用后需重启')
	}
	loading.value = false
}
</script>

<template>
	<setting-card-component title="独显直连">
		<template #extra>
			<a-switch
				v-model="GPUDirectConnection"
				:loading="loading"
				@click="GPUDirectConnection_handleClick"
			>
				<template #checked-icon>
					<icon-check />
				</template>
				<template #unchecked-icon>
					<icon-close />
				</template>
			</a-switch>
		</template>
	</setting-card-component>
</template>

<style scoped></style>
