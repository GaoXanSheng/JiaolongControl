import {describe, expect, it} from 'vitest'
import {applyStep} from '@/utils/number'

describe('applyStep', () => {
  it('对齐步进', () => {
    expect(applyStep(1234, 100, 0, 300000)).toBe(1200)
    expect(applyStep(1260, 100, 0, 300000)).toBe(1300)
  })

  it('钳制到范围', () => {
    expect(applyStep(-5, 1, 0, 100)).toBe(0)
    expect(applyStep(999, 1, 0, 100)).toBe(100)
  })

  it('MHz 与 GHz 换算的浮点误差', () => {
    expect(applyStep(3.6 / 0.001, 100, 1000, 5400)).toBe(3600)
  })

  it('负值与负步进', () => {
    expect(applyStep(-21, 1, -50, 50)).toBe(-21)
    expect(applyStep(7, 0, 0, 100)).toBe(7)
  })
})
