using System.Runtime.InteropServices;

namespace JiaoLongControl.Server.Core.Native
{
    /// <summary>单个显示器在某一时刻的快照: 有效缩放与物理像素工作区。</summary>
    internal readonly struct DisplayMetrics
    {
        public DisplayMetrics(double scaleX, double scaleY, User32.RECT workArea)
        {
            ScaleX = scaleX;
            ScaleY = scaleY;
            WorkArea = workArea;
        }

        /// <summary>有效缩放 (96 DPI = 1.0), WPF 的 DIP 与物理像素按此互换。</summary>
        public double ScaleX { get; }

        public double ScaleY { get; }

        /// <summary>工作区 (任务栏除外), 物理像素, 虚拟屏坐标 (副屏可为负)。</summary>
        public User32.RECT WorkArea { get; }
    }

    /// <summary>
    /// 统一的显示器信息入口: 按显示器取有效缩放与物理工作区, DPI 经 GetDpiForMonitor 取得。
    /// (主窗口尺寸钳制用 SystemParameters.WorkArea, OSD 定位固定走主屏, 各自语义单一。)
    /// </summary>
    internal static class DisplayInterop
    {
        /// <summary>
        /// 主显示器的有效缩放与物理工作区。供 OSD 定位使用: 首次显示前窗口句柄尚未创建,
        /// 而 OSD 固定驻留主屏, 按虚拟屏 (0,0) 落点取主屏即可。
        /// </summary>
        public static bool TryGetForPrimary(out DisplayMetrics metrics)
        {
            metrics = default;
            var hMon = User32.MonitorFromPoint(
                new User32.POINT { X = 0, Y = 0 }, User32.MONITOR_DEFAULTTOPRIMARY);
            return TryGetFromMonitor(hMon, out metrics);
        }

        private static bool TryGetFromMonitor(IntPtr hMon, out DisplayMetrics metrics)
        {
            metrics = default;
            if (hMon == IntPtr.Zero)
                return false;

            var mi = new User32.MONITORINFO { cbSize = Marshal.SizeOf<User32.MONITORINFO>() };
            if (!User32.GetMonitorInfoW(hMon, ref mi))
                return false;

            if (User32.GetDpiForMonitor(hMon, User32.MDT_EFFECTIVE_DPI, out uint dpiX, out uint dpiY) == 0
                && dpiX > 0 && dpiY > 0)
            {
                metrics = new DisplayMetrics(dpiX / 96.0, dpiY / 96.0, mi.rcWork);
                return true;
            }

            return false;
        }
    }
}
