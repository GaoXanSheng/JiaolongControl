<script setup lang="ts">
import SettingCardComponent from '@/components/RightComponent/setting/SettingCardComponent.vue'
import {onMounted, ref} from 'vue'
import {Config} from '@/utils/bridge'

const loading = ref(false)
const BootAutoStart = ref(false)
const MinimizedAfterBooting = ref(false)

onMounted(async () => {
  BootAutoStart.value = await Config.Boot.IsEnabled()
  MinimizedAfterBooting.value = (await Config.GetConfig()).MinimizedAfterBooting
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
    BootAutoStart.value = await Config.Boot.IsEnabled()
  } finally {
    loading.value = false
  }
}

async function MinimizedAfterBootingChange<T>(value: T) {
  if (typeof value != "boolean") return
  loading.value = true
  const config = await Config.GetConfig()
  config.MinimizedAfterBooting = value
  await Config.SetConfig(config)
  MinimizedAfterBooting.value = value
  loading.value = false
}
</script>

<template>
  <setting-card-component title="开机自启">
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
  <setting-card-component v-if="BootAutoStart" title="自启时最小化">
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
</template>

<style scoped></style>
