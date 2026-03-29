<script setup lang="ts">
import useStore from '../stores'
import HOME from '@/components/RightComponent/HOME.vue'
import CPU from '@/components/RightComponent/CPU.vue'
import Fan from '@/components/RightComponent/Fan.vue'
import Keyboard from '@/components/RightComponent/KeyBoard.vue'
import Settings from '@/components/RightComponent/Settings.vue'
import { HomeTab } from '@/stores/HomeTab.ts'
import { computed } from 'vue'

const store = useStore()
const currentComponent = computed(() => {
	switch (store.$state.SwitchPages) {
		case HomeTab.HOME:
			return HOME
		case HomeTab.CPU:
			return CPU
		case HomeTab.Fan:
			return Fan
		case HomeTab.Keyboard:
			return Keyboard
		case HomeTab.Settings:
			return Settings
		default:
			return HOME
	}
})

function enter(el: Element, done: () => void) {
	// 进入时添加类
	el.classList.add('swap')
	el.addEventListener('animationend', () => {
		done()
	})
}

function leave(_el: Element, done: () => void) {
	done()
}
</script>

<template>
	<div class="rightSide">
		<transition class="magictime" @enter="enter" @leave="leave">
			<component :is="currentComponent" :key="store.$state.SwitchPages" />
		</transition>
	</div>
</template>

<style scoped>
.rightSide {
	width: 100%;
	height: 100%;
}
</style>
