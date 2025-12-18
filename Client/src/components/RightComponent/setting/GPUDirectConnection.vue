<script async setup lang="ts">
import {onMounted, ref} from 'vue'
import {GPU, GPUMode} from '@/utils/bridge.ts'
import {Message} from '@arco-design/web-vue'
import SettingCardComponent from '@/components/RightComponent/setting/SettingCardComponent.vue'

const loading = ref(false)
const GPUDirectConnection = ref(false)
onMounted(async () => {
  GPUDirectConnection.value = (await GPU.Get()) == GPUMode.DiscreteMode
})

async function GPUDirectConnection_handleClick() {
  loading.value = true
  const result = await GPU.Set(GPUDirectConnection.value ? GPUMode.DiscreteMode : GPUMode.HybridMode)
  if (result) {
    Message.success('应用成功')
    Message.info('独显直连应用后需重启')
  } else {
    Message.success('应用失败')
  }
  GPUDirectConnection.value = result
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
