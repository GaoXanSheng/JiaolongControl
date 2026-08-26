/**
 * SVG 迷你曲线 (sparkline) 生成工具: 历史数值数组 → 折线/面积 path 字符串.
 * 用于 GPU/RyzenSmu 等页面的实时监控小图表, 取代各组件重复的曲线构造逻辑.
 */
export interface SparklineOptions {
  /** 视口宽度 (px), 默认 160 */
  width?: number
  /** 视口高度 (px), 默认 40 */
  height?: number
  /** y 轴数值上限; 默认取数据最大值 */
  max?: number
  /** 三次贝塞尔平滑, 默认 true */
  smooth?: boolean
  /** 追加闭合面积路径, 默认 false */
  area?: boolean
}

export interface SparklineResult {
  line: string
  area?: string
}

export function buildSparkline(
  values: number[],
  options: SparklineOptions = {},
): SparklineResult {
  const { width = 160, height = 40, max, smooth = true, area = false } = options
  if (values.length < 2) {
    return { line: `M 0 ${height}`, ...(area ? { area: `M 0 ${height} L ${width} ${height} L 0 ${height} Z` } : {}) }
  }
  const yMax = max ?? Math.max(...values)
  const points = values.map((v, i) => ({
    x: (i / (values.length - 1)) * width,
    y: height - (Math.max(0, Math.min(v, yMax)) / yMax) * height,
  }))
  const line = points
    .map((p, i) => {
      if (i === 0) return `M ${p.x},${p.y}`
      if (smooth) {
        const prev = points[i - 1]!
        const cx = prev.x + (p.x - prev.x) / 2
        return `C ${cx},${prev.y} ${cx},${p.y} ${p.x},${p.y}`
      }
      return `L ${p.x},${p.y}`
    })
    .join(' ')
  return area ? { line, area: `${line} L ${width},${height} L 0,${height} Z` } : { line }
}
