<script async setup lang="ts">
import RightSide from '@renderer/components/rightSide.vue'
import useStore, { HomeCardType } from '@renderer/store'
import { HomeTab } from '@renderer/store/HomeTab'
import information from '@renderer/assets/Information.png'
import electron from '@renderer/tools/electron'

const store = useStore()

async function onClickMenuItem(key: HomeTab) {
	store.$state.ServiceOption = await electron.ipcRenderer.invoke('ServiceOption', {
		type: 'status'
	})
	if (key != HomeTab.OpenProSettings) {
		store.$state.SwitchPages = key
		return
	}
	if (HomeTab.OpenProSettings) {
		return await electron.ipcRenderer.invoke('OpenProSettings')
	}
}
</script>

<template>
	<Suspense>
		<a-layout class="layout">
			<a-layout-sider collapsible breakpoint="xl">
				<a-menu :style="{ width: '200px', height: '100%' }" @menu-item-click="onClickMenuItem">
					<a-menu-item v-for="item in HomeCardType" :key="item.eum">
						<template #icon>
							<a-image
								class="theme"
								width="16"
								shape="square"
								:src="item.icon"
								:preview-visible="false"
							></a-image>
						</template>
						{{ item.title }}
					</a-menu-item>
					<a-menu-item :key="HomeTab.OpenProSettings">
						<template #icon>
							<a-image
								class="theme"
								width="16"
								shape="square"
								:src="information"
								:preview-visible="false"
							></a-image>
						</template>
						OpenProSettings
					</a-menu-item>
				</a-menu>
				<template #trigger="{ collapsed }">
					<IconCaretRight v-if="collapsed"></IconCaretRight>
					<IconCaretLeft v-else></IconCaretLeft>
				</template>
			</a-layout-sider>
			<a-layout>
				<a-layout class="content">
					<a-layout-content>
						<RightSide></RightSide>
					</a-layout-content>
				</a-layout>
			</a-layout>
		</a-layout>
	</Suspense>
</template>

<style lang="scss" scoped>
.layout {
	width: 100vw;
	height: 100vh;
	background: var(--color-fill-2);

	:deep(.arco-layout-content) {
		display: flex;
		flex-direction: column;
		justify-content: center;
		color: var(--color-white);
		font-size: 16px;
		font-stretch: condensed;
		text-align: center;
		font-weight: 400;
		background: var(--color-bg-3);
	}
}

body[arco-theme='dark'] {
	.content {
		padding-left: 0;
	}
	.theme {
		filter: invert(100%);
	}
}

body[arco-theme='light'] {
	.theme {
		filter: invert(0);
	}
	.content {
		padding-left: 2px;
	}
}
</style>
