<script setup lang="ts">
import useStore from '@/stores'
import { ref } from 'vue'
import SettingCardComponent from '@/components/RightComponent/setting/SettingCardComponent.vue'
import { Message } from '@arco-design/web-vue'

const store = useStore()
const theme = ref(store.$state.theme != 'light')
const loading = ref(false)
function setTheme() {
	loading.value = true
	if (theme.value) {
		store.$state.theme = 'dark'
	} else {
		store.$state.theme = 'light'
	}
	document.body.setAttribute('arco-theme', store.$state.theme)
	loading.value = false
}
</script>

<template>
	<setting-card-component title="暗色主题">
		<template #extra>
			<a-switch v-model="theme" :loading="loading" @click="setTheme">
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
