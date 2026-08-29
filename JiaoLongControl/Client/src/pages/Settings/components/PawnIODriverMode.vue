<script setup lang="ts">
import { ref } from 'vue'
import { Message } from '@arco-design/web-vue'
import SettingCardComponent from '@/components/common/SettingCardComponent.vue'
import { SystemInfo } from '@/utils/bridge'

const PAWNIO_URL = 'https://pawnio.eu/'
const loading = ref(false)

async function openWebsite() {
  loading.value = true
  try {
    const res = await SystemInfo.OpenUrl(PAWNIO_URL)
    if (res.Success) {
      Message.success('已打开 PawnIO 官网')
    } else {
      Message.error(res.Message || '打开失败')
    }
  } catch (e) {
    Message.error('打开失败')
    console.error(e)
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <setting-card-component
    title="PawnIO 驱动"
    description="Ryzen SMU 功能需要 PawnIO 内核驱动。本软件不再附带驱动，请从官网下载安装 PawnIO 后重启应用；安装完成后本软件会自动连接系统 PawnIO 服务。"
  >
    <template #extra>
      <a-button type="primary" :loading="loading" @click="openWebsite"> 前往官网下载 </a-button>
    </template>
  </setting-card-component>
</template>

<style scoped></style>
