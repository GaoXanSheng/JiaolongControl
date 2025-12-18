// 定义 C# bridge 对象类型
interface WebViewBridge {
    Call(json: string): string;
}

declare global {
    interface Window {
        chrome: {
            webview: {
                hostObjects: {
                    sync: {
                        bridge: WebViewBridge
                    }
                }
                postMessage(message: any): void
            }
        }
    }
}

export const Bridge = {
    /**
     * 调用 C# 方法
     * @param channel 通道名称 (如 HardwareControl, RgbEventLoop)
     * @param data
     * @param args 参数列表
     */
    async invoke(channel: string, data?: any, args?: any[]) {
        if (!window.chrome?.webview?.hostObjects?.sync?.bridge) {
            console.warn('WebView2 Bridge not found. Dev mode?');
            return null;
        }

        const payload = {
            channel,
            data,
            args: args || []
        };

        try {
            // 同步调用 C# 方法 (虽然是 sync，但在 JS 侧为了防卡顿，业务逻辑最好 await)
            const responseStr = window.chrome.webview.hostObjects.sync.bridge.Call(JSON.stringify(payload));
            const response = JSON.parse(responseStr);

            // 处理 C# 返回的通用结构 { result: any, msg: string }
            if (response.msg && response.result === false) {
                console.error('Bridge Error:', response.msg);
                throw new Error(response.msg);
            }
            return response.result;
        } catch (e) {
            console.error('Bridge Invoke Failed:', e);
            throw e;
        }
    }
}