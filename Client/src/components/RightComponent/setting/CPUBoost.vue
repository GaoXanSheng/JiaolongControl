<script async setup lang="ts">
import SettingCardComponent from '@/components/RightComponent/setting/SettingCardComponent.vue'
import { ref } from 'vue'
import WMIOperation from '@/utils/WMIOperation.ts'
const cpuboost = ref(await WMIOperation.CPU.GetCustomMode())
const loading = ref(false)
async function CpuBoost_handleClick() {
	loading.value = true
	await WMIOperation.CPU.OpenCustomMode(cpuboost.value)
	loading.value = false
}
</script>

<template>
	<setting-card-component title="CPU增压">
		<template #extra>
			<a-switch v-model="cpuboost" :loading="loading" @click="CpuBoost_handleClick">
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
