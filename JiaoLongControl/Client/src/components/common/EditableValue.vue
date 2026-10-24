<script lang="ts" setup>
import {computed, nextTick, ref} from 'vue'
import {applyStep} from '@/utils/number'

const props = withDefaults(
  defineProps<{
    modelValue: number
    min?: number
    max?: number
    step?: number
    unit?: string
    signed?: boolean
    disabled?: boolean
    scale?: number
    precision?: number
  }>(),
  {min: 0, max: 100, step: 1, unit: '', signed: false, disabled: false, scale: 1, precision: 0},
)

const emit = defineEmits<{
  (e: 'update:modelValue', value: number): void
  (e: 'commit', value: number): void
}>()

const editing = ref(false)
const draft = ref<number | null>(null)
const inputRef = ref<{focus: () => void} | null>(null)

const displayText = computed(() => {
  const scaled = props.modelValue * props.scale
  const text = props.precision > 0 ? scaled.toFixed(props.precision) : String(scaled)
  const sign = props.signed && scaled >= 0 ? '+' : ''
  return `${sign}${text}${props.unit ? ` ${props.unit}` : ''}`
})

async function startEdit() {
  if (props.disabled || editing.value) return
  draft.value = props.modelValue * props.scale
  editing.value = true
  await nextTick()
  inputRef.value?.focus()
}

function commit() {
  if (!editing.value) return
  editing.value = false
  if (typeof draft.value !== 'number') return
  const next = applyStep(draft.value / props.scale, props.step, props.min, props.max)
  if (next === props.modelValue) return
  emit('update:modelValue', next)
  emit('commit', next)
}

function cancel() {
  editing.value = false
}

function onKeydown(e: KeyboardEvent) {
  if (e.key === 'Enter') commit()
  else if (e.key === 'Escape') cancel()
}
</script>

<template>
  <a-input-number
    v-if="editing"
    ref="inputRef"
    v-model="draft"
    :max="max * scale"
    :min="min * scale"
    :precision="precision > 0 ? precision : undefined"
    :step="step * scale"
    class="!w-24 !bg-transparent !border-none !text-current p-0 text-center font-mono"
    hide-button
    size="mini"
    @blur="commit"
    @keydown="onKeydown"
  />
  <span
    v-else
    class="cursor-pointer transition-opacity hover:opacity-75"
    title="点击输入数值"
    @click="startEdit"
    >{{ displayText }}</span
  >
</template>
