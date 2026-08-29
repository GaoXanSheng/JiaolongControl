import { describe, expect, it } from 'vitest'
import { call, toByte, type CommandResult } from '@/utils/bridge'

describe('toByte', () => {
  it('接受整数并原样返回', () => {
    expect(toByte(0)).toBe(0)
    expect(toByte(100)).toBe(100)
  })

  it('拒绝非整数', () => {
    expect(() => toByte(1.5)).toThrow('必须是整数')
    expect(() => toByte(NaN)).toThrow('必须是整数')
  })
})

describe('call', () => {
  it('通过 toJson 反序列化 WebView2 hostObject 响应', async () => {
    const promise = {
      toJson: () => Promise.resolve(JSON.stringify({ Success: true, Message: 'ok', Data: 42 })),
    } as unknown as Parameters<typeof call<number>>[0]

    const result: CommandResult<number> = await call<number>(promise)
    expect(result.Success).toBe(true)
    expect(result.Data).toBe(42)
  })

  it('透传失败响应', async () => {
    const promise = {
      toJson: () =>
        Promise.resolve(JSON.stringify({ Success: false, Message: 'nvidia-smi 不可用' })),
    } as unknown as Parameters<typeof call>[0]

    const result = await call(promise)
    expect(result.Success).toBe(false)
    expect(result.Message).toBe('nvidia-smi 不可用')
  })
})
