<script async setup lang="ts">
import {onMounted, ref} from 'vue'
import {Fan} from '@/utils/bridge.ts'
import { Message } from '@arco-design/web-vue'
import SettingCardComponent from '@/components/RightComponent/setting/SettingCardComponent.vue'

const FanSpeedSwitch = ref(false)
onMounted(async () => {
	FanSpeedSwitch.value =  Fan.GetMaxFanSpeedSwitch()
})
const loading = ref(false)
async function FanSpeedSwitch_handleClick() {
	loading.value = true
	const res =  Fan.SetMaxFanSpeedSwitch(FanSpeedSwitch.value)
	if (res) {
    Message.success('应用成功')
	} else {
    Message.success('应用失败')
	}
  FanSpeedSwitch.value = res
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
