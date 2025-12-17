<script async setup lang="ts">
import { ref } from 'vue'
import WMIOperation from '@/utils/WMIOperation.ts'
import { Message } from '@arco-design/web-vue'
import SettingCardComponent from '@/components/RightComponent/setting/SettingCardComponent.vue'
const loading = ref(false)
const LogoLight = ref((await WMIOperation.LogoLight.Get()) == 0)
async function LogoLight_handleClick() {
	loading.value = true
	const result = await WMIOperation.LogoLight.Set(LogoLight.value ? 1 : 0)
	if (result == '拒绝访问') {
		LogoLight.value = false
		Message.error(result)
	} else {
		Message.success('应用成功')
	}
	loading.value = false
}
</script>

<template>
	<setting-card-component title="Logo灯">
		<template #extra>
			<a-switch v-model="LogoLight" :loading="loading" @click="LogoLight_handleClick">
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
