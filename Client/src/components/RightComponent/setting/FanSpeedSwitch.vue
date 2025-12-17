<script async setup lang="ts">
import { ref } from 'vue'
import WMIOperation from '@/utils/WMIOperation.ts'
import { Message } from '@arco-design/web-vue'
import SettingCardComponent from '@/components/RightComponent/setting/SettingCardComponent.vue'

const FanSpeedSwitch = ref((await WMIOperation.Fan.GetMaxFanSpeedSwitch()) == 1)
const loading = ref(false)
async function FanSpeedSwitch_handleClick() {
	loading.value = true
	const msg: string | boolean = await WMIOperation.Fan.SetMaxFanSpeedSwitch(FanSpeedSwitch.value)
	if (msg == '拒绝访问') {
		Message.error(msg)
		FanSpeedSwitch.value = false
	} else {
		Message.success('应用成功')
	}
	loading.value = false
}
// 调用该方法切换风扇最大转速开关？ 反正WMI中是这样写的方法
</script>

<template>
	<setting-card-component title="风扇控速开关">
		<template #extra>
			<a-switch v-model="FanSpeedSwitch" :loading="loading" @click="FanSpeedSwitch_handleClick">
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
