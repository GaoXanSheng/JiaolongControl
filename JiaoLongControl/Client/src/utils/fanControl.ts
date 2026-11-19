import {AutoFanControl} from '@/utils/bridge'

/** 自动风扇运行中则先停止 (读取失败按未运行处理), 手动风扇页与曲线编辑器共用 */
export async function stopAutoFanIfRunning() {
  const isRunningRes = await AutoFanControl.IsRunning()
  if (isRunningRes.Success && isRunningRes.Data) {
    await AutoFanControl.Stop()
  }
}
