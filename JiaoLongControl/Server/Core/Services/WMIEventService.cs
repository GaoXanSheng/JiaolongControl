using System.Management;
using JiaoLongControl.Server.Core.Models;
using log4net;

namespace JiaoLongControl.Server.Core.Services
{
    /// <summary>EC 热键事件参数。</summary>
    public class WmiHotKeyEvent : EventArgs
    {
        public byte EventType { get; init; }
        public EventName EventName { get; init; }
        public int Value { get; init; }
    }

    /// <summary>
    /// 订阅 root\WMI 的 HID_EVENT20 事件类 —— 官方 EC 热键事件通道。
    /// EventDetail 为字节数组: [0]=EventType(HotKey=1), [1]=EventName, [2]=值
    /// (CPU/GPU 风扇速度为 [2]&lt;&lt;8|[3] 双字节, 其余为单字节)。
    /// 订阅失败 (机型无此事件类等) 由调用方退回轮询兜底。
    /// </summary>
    public class WMIEventService : IDisposable
    {
        private const string Wql = "SELECT * FROM HID_EVENT20";

        private static readonly ILog Logger = LogManager.GetLogger(typeof(WMIEventService));

        private ManagementEventWatcher? _watcher;

        public void Dispose() => Stop();

        /// <summary>事件到达 (WMI 线程池线程回调)。</summary>
        public event Action<WmiHotKeyEvent>? EventArrived;

        /// <summary>启动订阅; 成功返回 true。</summary>
        public bool Start()
        {
            try
            {
                _watcher = new ManagementEventWatcher("root\\WMI", Wql);
                _watcher.EventArrived += OnEventArrived;
                _watcher.Start();
                Logger.Info("HID_EVENT20 事件订阅成功");
                return true;
            }
            catch (ManagementException ex)
            {
                Logger.Warn($"HID_EVENT20 事件订阅失败: {ex.Message}");
                Stop();
                return false;
            }
            catch (Exception ex)
            {
                Logger.Warn($"HID_EVENT20 事件订阅异常: {ex.Message}");
                Stop();
                return false;
            }
        }

        private void OnEventArrived(object sender, EventArrivedEventArgs e)
        {
            try
            {
                if (e.NewEvent?.Properties["EventDetail"]?.Value is not byte[] detail || detail.Length < 3)
                    return;

                var evt = new WmiHotKeyEvent
                {
                    EventType = detail[0],
                    EventName = (EventName)detail[1],
                    Value = detail[1] == (byte)EventName.CPUFanSpeed || detail[1] == (byte)EventName.GPUFanSpeed
                        ? (detail[2] << 8) + (detail.Length > 3 ? detail[3] : 0)
                        : detail[2],
                };
                EventArrived?.Invoke(evt);
            }
            catch (Exception ex)
            {
                Logger.Warn($"HID_EVENT20 事件解析失败: {ex.Message}");
            }
        }

        public void Stop()
        {
            var watcher = _watcher;
            _watcher = null;
            if (watcher == null) return;
            try
            {
                watcher.Stop();
                watcher.EventArrived -= OnEventArrived;
                watcher.Dispose();
            }
            catch (Exception ex)
            {
                Logger.Warn($"HID_EVENT20 事件订阅停止异常: {ex.Message}");
            }
        }
    }
}
