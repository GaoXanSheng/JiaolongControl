<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { Message } from '@arco-design/web-vue'
import SettingCardComponent from '@/components/common/SettingCardComponent.vue'
import { Boot } from '@/utils/bridge'
import { useConfigStore } from '@/stores/config'

const configStore = useConfigStore()

// 开机自启 = 注册 Windows 计划任务 (AutoStartController, 携带 --boot 参数启动)
const autoStart = ref(false)
const autoStartLoading = ref(false)

// 自启时最小化 = App.BootMinimized 配置, 仅当自启开启时显示
const minimized = computed(() => configStore.config?.App.BootMinimized ?? false)
const minimizedLoading = ref(false)

onMounted(async () => {
  try {
    const res = await Boot.IsEnabled()
    autoStart.value = res.Data
  } catch (e) {
    autoStart.value = false
    Message.error(`获取开机自启状态失败：${(e as Error)?.message ?? e}`)
  }
  await configStore.fetchConfig()
})

async function onAutoStartChange(next: string | number | boolean) {
  if (typeof next !== 'boolean') return
  autoStartLoading.value = true
  try {
    const res = next ? await Boot.Enable() : await Boot.Disable()
    if (!res.Success) {
      Message.error(res.Message || (next ? '启用开机自启失败' : '关闭开机自启失败'))
      return
    }
    autoStart.value = next
  } catch (e) {
    Message.error(`${next ? '启用' : '关闭'}开机自启失败：${(e as Error)?.message ?? e}`)
  } finally {
    autoStartLoading.value = false
  }
}

async function onMinimizedChange(next: string | number | boolean) {
  if (typeof next !== 'boolean' || !configStore.config) return
  const prev = configStore.config.App.BootMinimized
  minimizedLoading.value = true
  try {
    configStore.config.App.BootMinimized = next
    const res = await configStore.saveConfig()
    if (!res?.Success) {
      configStore.config.App.BootMinimized = prev
      Message.error(res?.Message || '保存失败')
    }
  } catch (e) {
    configStore.config.App.BootMinimized = prev
    Message.error(`保存失败：${(e as Error)?.message ?? e}`)
  } finally {
    minimizedLoading.value = false
  }
}
</script>

<template>
  <div class="contents space-y-3">
    <setting-card-component
      title="开机自启"
      description="允许 JiaoLongControl 随 Windows 操作系统启动而自动运行，确保各类硬件优化策略与自定义风扇曲线实时生效。"
    >
      <template #extra>
        <a-switch
          :model-value="autoStart"
          :loading="autoStartLoading"
          @change="onAutoStartChange($event)"
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

    <setting-card-component
      v-if="autoStart"
      title="自启时最小化"
      description="随开机自动启动时自动最小化到系统托盘，不弹出主界面，保持桌面清爽干净。"
    >
      <template #extra>
        <a-switch
          :model-value="minimized"
          :loading="minimizedLoading"
          @change="onMinimizedChange($event)"
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
  </div>
</template>

<style scoped></style>
