<script setup lang="ts">
import SettingCardComponent from '@/components/common/SettingCardComponent.vue'
import { onMounted, ref } from 'vue'
import { Config } from '@/utils/bridge'

const loading = ref(false)
const BootAutoStart = ref(false)
const MinimizedAfterBooting = ref(false)

onMounted(async () => {
  BootAutoStart.value = (await Config.Boot.IsEnabled()).Success
  const appResult = await Config.GetAppConfig()
  if (appResult.Success) MinimizedAfterBooting.value = appResult.Data.BootMinimized
})

async function BootAutoStartHandleChange<T>(value: T) {
  if (typeof value != "boolean") return
  loading.value = true
  try {
    if (value) {
      await Config.Boot.Enable()
    } else {
      await Config.Boot.Disable()
    }
    BootAutoStart.value = (await Config.Boot.IsEnabled()).Success
  } finally {
    loading.value = false
  }
}

async function MinimizedAfterBootingChange<T>(value: T) {
  if (typeof value != "boolean") return
  loading.value = true
  const appResult = await Config.GetAppConfig()
  if (!appResult.Success) { loading.value = false; return }
  const config = appResult.Data
  config.BootMinimized = value
  await Config.SetAppConfig(config)
  MinimizedAfterBooting.value = value
  loading.value = false
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
