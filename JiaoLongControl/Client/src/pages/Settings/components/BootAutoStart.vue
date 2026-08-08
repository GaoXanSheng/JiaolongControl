<script setup lang="ts">
import SettingCardComponent from '@/components/common/SettingCardComponent.vue'
import {computed, onMounted, ref} from 'vue'
import {Message} from '@arco-design/web-vue'
import {Boot} from '@/utils/bridge'
import {useConfigStore} from '@/stores/config'

const configStore = useConfigStore()
const loading = ref(false)
const BootAutoStart = ref(false)
const MinimizedAfterBooting = computed(() => configStore.config?.App.BootMinimized ?? false)

onMounted(async () => {
  BootAutoStart.value = (await Boot.IsEnabled()).Success
  await configStore.fetchConfig()
})

async function BootAutoStartHandleChange<T>(value: T) {
  if (typeof value != "boolean") return
  loading.value = true
  try {
    if (value) {
      await Boot.Enable()
    } else {
      await Boot.Disable()
    }
    BootAutoStart.value = (await Boot.IsEnabled()).Success
  } finally {
    loading.value = false
  }
}

async function MinimizedAfterBootingChange<T>(value: T) {
  if (typeof value != "boolean" || !configStore.config) return
  const prev = configStore.config.App.BootMinimized
  loading.value = true
  try {
    configStore.config.App.BootMinimized = value
    const res = await configStore.saveConfig()
    if (!res?.Success) {
      configStore.config.App.BootMinimized = prev
      Message.error(res?.Message || '保存失败')
    }
  } catch (e) {
    configStore.config.App.BootMinimized = prev
    Message.error('保存失败')
    console.error(e)
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="contents space-y-3">
    <setting-card-component title="开机自启" description="允许 JiaoLongControl 随 Windows 操作系统启动而自动运行，确保各类硬件优化策略与自定义风扇曲线实时生效。">
      <template #extra>
        <a-switch
            :model-value="BootAutoStart"
            :loading="loading"
            @change="BootAutoStartHandleChange($event)"
        >
          <template #checked-icon>
            <icon-check/>
          </template>
          <template #unchecked-icon>
            <icon-close/>
          </template>
        </a-switch>
      </template>
    </setting-card-component>

    <setting-card-component v-if="BootAutoStart" title="自启时最小化" description="随开机自动启动时自动最小化到系统托盘，不弹出主界面，保持桌面清爽干净。">
      <template #extra>
        <a-switch
            :model-value="MinimizedAfterBooting"
            :loading="loading"
            @change="MinimizedAfterBootingChange($event)"
        >
          <template #checked-icon>
            <icon-check/>
          </template>
          <template #unchecked-icon>
            <icon-close/>
          </template>
        </a-switch>
      </template>
    </setting-card-component>
  </div>
</template>
<style scoped></style>
