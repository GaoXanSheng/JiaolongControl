const ORIGIN = performance.now()

export function timing(label: string, since?: number) {
  const now = performance.now()
  const message = `[timing] ${label}: ${(now - (since ?? ORIGIN)).toFixed(0)}ms`
  console.info(message)
  try {
    window.chrome?.webview?.postMessage(message)
  } catch {
    // 非 WebView2 环境仅输出到控制台
  }
  return now
}
