// 三态主题(ligh/dark/system)解析与应用。
// 解析结果写入 <html data-theme="light|dark"> 驱动 CSS 变量切换, 并缓存到
// localStorage 供 index.html 内联脚本在 Vue 启动前抢先设置, 避免浅色用户
// 启动时闪深色屏。配置文件(config.App.Theme)是唯一事实来源, localStorage
// 只是启动期的缓存。
import { computed, ref } from 'vue'
import type { ThemeMode } from '@/types/config'

const THEME_STORAGE_KEY = 'jl-theme'

export const resolvedTheme = ref<'light' | 'dark'>('dark')

// echarts(zrender) 自身解析颜色, 不支持 CSS 变量/color-mix, 图表配色在此按主题给值;
// 图表配置的 computed 引用本对象即可在主题切换时自动重绘
export const chartTheme = computed(() =>
  resolvedTheme.value === 'light'
    ? {
        legend: '#5a6478',
        axis: '#8a93a5',
        line: 'rgba(13, 14, 21, 0.08)',
        label: '#1a1b26',
      }
    : {
        legend: '#A0AEC0',
        axis: '#6B7280',
        line: 'rgba(255, 255, 255, 0.05)',
        label: '#FFFFFF',
      },
)

let mediaQuery: MediaQueryList | null = null
let currentMode: ThemeMode = 'dark'

function readStoredTheme(): 'light' | 'dark' | null {
  try {
    const v = localStorage.getItem(THEME_STORAGE_KEY)
    return v === 'light' || v === 'dark' ? v : null
  } catch {
    return null
  }
}

function systemPrefersDark(): boolean {
  return window.matchMedia?.('(prefers-color-scheme: dark)').matches ?? true
}

function resolve(mode: ThemeMode): 'light' | 'dark' {
  if (mode === 'light' || mode === 'dark') return mode
  return systemPrefersDark() ? 'dark' : 'light'
}

function applyResolved(resolved: 'light' | 'dark') {
  resolvedTheme.value = resolved
  document.documentElement.dataset.theme = resolved
  try {
    localStorage.setItem(THEME_STORAGE_KEY, resolved)
  } catch {
    /* localStorage 不可用时跳过缓存 */
  }
}

function onSystemChange() {
  if (currentMode === 'system') {
    applyResolved(resolve('system'))
  }
}

// 模块加载时先用缓存恢复一次(与 index.html 内联脚本设置的 data-theme 保持一致),
// 无缓存时跟随系统深浅色; 保证 canvas 绘图等 JS 逻辑在首帧前就能拿到正确的主题
applyResolved(readStoredTheme() ?? resolve('system'))

export function applyTheme(mode: ThemeMode) {
  currentMode = mode
  if (!mediaQuery) {
    mediaQuery = window.matchMedia?.('(prefers-color-scheme: dark)') ?? null
    mediaQuery?.addEventListener('change', onSystemChange)
  }
  const resolved = resolve(mode)
  applyResolved(resolved)
  // 通知 WPF 侧同步窗口背景 / WebView2 底色(浏览器开发环境无 webview 时静默跳过)
  window.chrome?.webview?.postMessage(`theme-changed:${resolved}`)
}
