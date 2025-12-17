<script async setup lang="ts">
import SettingCardComponent from '@/components/RightComponent/setting/SettingCardComponent.vue'
import useStore from '@/stores'
import { ref } from 'vue'

const store = useStore()
const loading = ref(false)
async function handleClick() {

}
</script>

<template>
	<setting-card-component title="InstallService">
		<template #extra>
			<a-switch v-model="store.$state.ServiceOption" :loading="loading" @click="handleClick">
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
