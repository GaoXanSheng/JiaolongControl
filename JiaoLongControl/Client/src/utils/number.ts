export function applyStep(value: number, step: number, min: number, max: number): number {
  const s = step > 0 ? step : 1
  const snapped = Math.round(value / s) * s
  return Math.min(max, Math.max(min, snapped))
}
