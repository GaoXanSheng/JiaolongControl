<script setup lang="ts">
import { Message } from '@arco-design/web-vue'
import  {PerformanceMode, SystemPerMode} from '@/utils/bridge.ts'
import SettingCardComponent from '@/components/RightComponent/setting/SettingCardComponent.vue'
import { ref, onMounted } from 'vue'

const loading = ref(false)
const currentMode = ref<SystemPerMode | null>(null);

async function fetchCurrentMode() {
  try {
    const res = await PerformanceMode.Get();
    if (res.Success) {
      currentMode.value = res.Data as SystemPerMode;
    }
  } catch (e) {
    console.error("Failed to fetch current performance mode:", e);
  }
}

async function PowerMode_handleClick(mode: SystemPerMode) {
	loading.value = true
	try {
    const result = await PerformanceMode.Set(mode)
    if(result.Success) {
      currentMode.value = mode;
      Message.success(result.Message)
    } else {
      Message.error(result.Message || '设置失败');
    }
  } finally {
    loading.value = false
  }
}

onMounted(fetchCurrentMode);
</script>

<template>
	<setting-card-component title="系统性能模式切换" description="快速选择系统预设的全局电源调度与风扇策略方案（如性能、狂飙、办公模式），即时满足不同场景下的性能需求。">
		<template #extra>
			<a-space size="large">
				<a-dropdown>
					<a-button :loading="loading">选择策略</a-button>
					<template #content>
						<a-doption 
              :class="{ 'active-option': currentMode === SystemPerMode.BalanceMode }"
              @click="PowerMode_handleClick(SystemPerMode.BalanceMode)"
						>性能模式
						</a-doption>
						<a-doption 
              :class="{ 'active-option': currentMode === SystemPerMode.PerformanceMode }"
              @click="PowerMode_handleClick(SystemPerMode.PerformanceMode)"
						>狂飙模式
						</a-doption>
						<a-doption 
              :class="{ 'active-option': currentMode === SystemPerMode.QuietMode }"
              @click="PowerMode_handleClick(SystemPerMode.QuietMode)"
						>办公模式
						</a-doption>
					</template>
				</a-dropdown>
			</a-space>
		</template>
	</setting-card-component>
</template>

<style scoped>
.active-option {
  background-color: rgba(138, 43, 226, 0.15) !important;
  color: #a855f7 !important;
  font-weight: 600;
}
</style>
