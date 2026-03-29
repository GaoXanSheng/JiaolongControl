<script async setup lang="ts">
import {onMounted, ref} from 'vue'
import {GPU, GPUMode} from '@/utils/bridge.ts'
import {Message} from '@arco-design/web-vue'
import SettingCardComponent from '@/components/RightComponent/setting/SettingCardComponent.vue'

const loading = ref(false)
const GPUDirectConnection = ref(false)
onMounted(async () => {
  GPUDirectConnection.value = (await GPU.Get()).Data == GPUMode.DiscreteMode
})

async function GPUDirectConnection_handleClick() {
  loading.value = true
  const result = await GPU.Set(GPUDirectConnection.value ? GPUMode.DiscreteMode : GPUMode.HybridMode)
  Message.success(result.Message)
  Message.info('独显直连应用后需重启')
  GPUDirectConnection.value = result.Success
  loading.value = false
}
</script>

<template>
  <setting-card-component title="独显直连">
    <template #extra>
      <a-switch
          v-model="GPUDirectConnection"
          :loading="loading"
          @click="GPUDirectConnection_handleClick"
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
