import {reactive} from 'vue'

/**
 * 固定长度的历史缓冲 (sparkline 用): push 超长丢最旧。
 * 返回响应式数组可直接作为 props 传给图表组件。
 */
export function useRingBuffer(length: number) {
  const buffer: number[] = reactive(Array<number>(length).fill(0))
  const push = (value: number) => {
    buffer.push(value)
    if (buffer.length > length) buffer.shift()
  }
  return { buffer, push }
}
