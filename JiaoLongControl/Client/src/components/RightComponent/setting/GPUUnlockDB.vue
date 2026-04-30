<script async setup lang="ts">
import {ref} from 'vue'
import {NvidiaGpu} from '@/utils/bridge.ts'
import {Message} from '@arco-design/web-vue'
import SettingCardComponent from '@/components/RightComponent/setting/SettingCardComponent.vue'

const loading = ref(false)
const GPU_UnlockDB = ref(false)

async function GPU_UnlockDB_handleClick() {
  loading.value = true
  const result = await NvidiaGpu.UnlockDB()
  Message.success(result.Message)
  GPU_UnlockDB.value = result.Success
  loading.value = false
}
</script>

<template>
  <setting-card-component title="解锁DB">
    <template #extra>
      <a-switch
          v-model="GPU_UnlockDB"
          :loading="loading"
          @click="GPU_UnlockDB_handleClick"
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
