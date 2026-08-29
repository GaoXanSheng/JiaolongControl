<script async setup lang="ts">
import { onMounted, ref } from 'vue'
import { LogoLight, ResultState } from '@/utils/bridge.ts'
import { Message } from '@arco-design/web-vue'
import SettingCardComponent from '@/components/common/SettingCardComponent.vue'

const loading = ref(false)
const logolight = ref(false)
onMounted(async () => {
  logolight.value = (await LogoLight.Get()).Success
})

async function LogoLight_handleClick() {
  loading.value = true
  const result = logolight.value
    ? await LogoLight.Set(ResultState.ON)
    : await LogoLight.Set(ResultState.OFF)
  if (result) {
    Message.success('应用成功')
  } else {
    Message.success('应用失败')
    logolight.value = result
  }
  loading.value = false
}
</script>

<template>
  <setting-card-component
    title="Logo A壳徽标灯"
    description="控制笔记本电脑 A 面（顶盖）个性化 Logo 氛围指示灯的开启与关闭"
  >
    <template #extra>
      <a-switch v-model="logolight" :loading="loading" @click="LogoLight_handleClick">
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
