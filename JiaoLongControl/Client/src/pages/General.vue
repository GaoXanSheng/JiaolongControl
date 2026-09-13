<script lang="ts" setup>
import {computed, onMounted} from 'vue'
import {Message} from '@arco-design/web-vue'
import {type CommandResult, Osd} from '@/utils/bridge'
import {useLocksStore} from '@/stores/locks'
import SettingToggle from '@/components/common/SettingToggle.vue'
import LogoLight from './Settings/components/LogoLight.vue'
import GPUDirectConnection from './Settings/components/GPUDirectConnection.vue'

// ===== 锁定控制: 直接切换四项锁定状态 =====
// 状态来自 locks store: C# 检测到变化 (原生热键/EC 事件/轮询) 会实时推送, 按钮即时响应
type LockKey = 'CapsLock' | 'NumLock' | 'FnLock' | 'TouchpadLock'
const locksStore = useLocksStore()
const locks = computed(() => locksStore.locks)
const lockItems: Array<{ key: LockKey; label: string; hint: string }> = [
  { key: 'CapsLock', label: '大写键锁定', hint: '模拟系统按键切换' },
  { key: 'NumLock', label: '数字键锁定', hint: '模拟系统按键切换' },
  { key: 'FnLock', label: '功能键锁定', hint: '经 EC 写入切换' },
  { key: 'TouchpadLock', label: '触摸板锁定', hint: '经 EC 写入切换' },
]

async function refreshLocks() {
  try {
    const res = await Osd.GetLockStates()
    if (res.Success && res.Data) {
      locksStore.apply(res.Data)
    }
  } catch {
    /* 忽略 */
  }
}

function lockStateText(key: LockKey) {
  const v = locks.value?.[key]
  if (v === null || v === undefined) return '未知'
  return v ? '已开启' : '已关闭'
}

async function toggleLock(key: LockKey) {
  const cur = locks.value?.[key]
  if (cur === null || cur === undefined) {
    Message.warning('该锁定状态读取失败, 无法切换')
    return
  }
  const setters: Record<LockKey, (v: boolean) => Promise<CommandResult<void>>> = {
    CapsLock: Osd.SetCapsLock,
    NumLock: Osd.SetNumLock,
    FnLock: Osd.SetFnLock,
    TouchpadLock: Osd.SetTouchpadLock,
  }
  try {
    const res = await setters[key](!cur)
    if (!res.Success) Message.error(res.Message)
  } catch {
    Message.error('设置失败')
  }
  // 状态刷新由 C# 的 lock-states-changed 推送完成 (模拟按键约 120ms 防抖后推送)
}

onMounted(() => {
  refreshLocks()
})
</script>

<template>
  <div class="h-full overflow-y-auto text-ink p-6 no-scrollbar">
    <div class="max-w-[1300px] mx-auto">
      <!-- 头部标题: 通栏显示, 下方左右两列顶部齐平 -->
      <div>
        <h1 class="text-2xl font-bold tracking-wide">常规设置</h1>
        <p class="text-[13px] text-gray-500 mt-1">设备快捷控制与常用开关</p>
      </div>

      <div class="flex flex-col lg:flex-row gap-6 pt-6">
        <!-- ==================== 左列: 锁定控制与设备开关 ==================== -->
        <div class="flex-1 space-y-6">
          <div
            class="bg-panel/60 backdrop-blur-md border border-ink/[0.05] rounded-xl p-5 shadow-lg space-y-4"
          >
            <h3
              class="text-xs font-black text-emerald-400 uppercase tracking-widest border-l-4 border-emerald-500 pl-2.5"
            >
              锁定控制
            </h3>
            <div class="grid grid-cols-2 gap-2">
              <button
                v-for="item in lockItems"
                :key="item.key"
                :class="
                  locks?.[item.key]
                    ? '!bg-emerald-600/20 !text-emerald-400 !border-emerald-500/30 hover:!bg-emerald-600/30'
                    : '!bg-ink/[0.04] !text-muted !border-ink/[0.08] hover:!text-ink'
                "
                class="px-3 py-2.5 rounded-lg text-xs font-semibold border transition-all cursor-pointer text-left"
                @click="toggleLock(item.key)"
              >
                <div>{{ item.label }}</div>
                <div class="text-[10px] opacity-70 font-normal mt-0.5">
                  {{ item.hint }} · {{ lockStateText(item.key) }}
                </div>
              </button>
            </div>
            <p class="text-[10px] text-gray-600 leading-relaxed">
              点击切换状态并同步弹出 OSD 提示；功能键/触摸板锁定经 EC 写入，若机型不支持则显示未知
            </p>
          </div>

          <LogoLight />
          <GPUDirectConnection />
          <SettingToggle
            config-path="Fan.FanCurveMerge"
            description="启用后，软件将在【风扇曲线】页面中将所有风扇的曲线合并为一条曲线，方便用户统一调整风扇转速"
            title="风扇曲线合并"
          />
        </div>

        <!-- ==================== 右列: 说明 ==================== -->
        <div class="w-full lg:w-[380px] shrink-0 space-y-6">
          <div
            class="bg-panel/60 backdrop-blur-md border border-ink/[0.05] rounded-xl p-5 shadow-lg"
          >
            <h2 class="text-[13px] font-semibold text-gray-300 mb-3">名词解释</h2>
            <div class="text-[11px] text-gray-500 leading-relaxed space-y-2">
              <p>
                <strong>大写/数字键锁定</strong>:
                系统级键盘切换状态，通过模拟按键实现，与物理键盘指示灯保持同步。
              </p>
              <p>
                <strong>功能键锁定 (Fn Lock)</strong>: 切换 F1-F12 与媒体功能键的默认行为，经 EC
                直接写入。
              </p>
              <p>
                <strong>触摸板锁定</strong>: 启用或禁用触摸板，经 EC 直接写入。
              </p>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
