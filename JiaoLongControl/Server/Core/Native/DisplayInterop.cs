using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

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
    /// 全应用统一的 DPI 判定入口: 以「窗口实际所在显示器」为准取有效缩放与物理工作区,
    /// DPI 依次经 GetDpiForWindow → GetDpiForMonitor → VisualTreeHelper 取得。
    /// 所有窗口的尺寸换算、钳制、定位都必须经此, 不再各自查询 (此前主窗口用 VisualTreeHelper、
    /// OSD 用 GetDpiForMonitor 两套来源并存, 语义不一且难以统一修正)。
    /// </summary>
    internal static class DisplayInterop
    {
        /// <summary>
        /// 窗口所在显示器的有效缩放与物理工作区。句柄已创建后基本必然成功;
        /// 失败 (句柄未创建等) 返回 false, 调用方应保持现状而不是猜测。
        /// </summary>
        public static bool TryGetForWindow(Window window, out DisplayMetrics metrics)
        {
            metrics = default;
            if (window == null)
                return false;

            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero)
                return false;

            var hMon = User32.MonitorFromWindow(hwnd, User32.MONITOR_DEFAULTTONEAREST);
            return TryGetFromMonitor(hMon, hwnd, window, out metrics);
        }

        /// <summary>
        /// 主显示器的有效缩放与物理工作区。供 OSD 定位使用: 首次显示前窗口句柄尚未创建,
        /// 无法按窗口取, 而 OSD 固定驻留主屏, 按虚拟屏 (0,0) 落点取主屏即可。
        /// </summary>
        public static bool TryGetForPrimary(out DisplayMetrics metrics)
        {
            metrics = default;
            var hMon = User32.MonitorFromPoint(
                new User32.POINT { X = 0, Y = 0 }, User32.MONITOR_DEFAULTTOPRIMARY);
            return TryGetFromMonitor(hMon, IntPtr.Zero, null, out metrics);
        }

        private static bool TryGetFromMonitor(
            IntPtr hMon, IntPtr hwnd, Window? window, out DisplayMetrics metrics)
        {
            metrics = default;
            if (hMon == IntPtr.Zero)
                return false;

            var mi = new User32.MONITORINFO { cbSize = Marshal.SizeOf<User32.MONITORINFO>() };
            if (!User32.GetMonitorInfoW(hMon, ref mi))
                return false;

            // DPI 判定优先级: 窗口级 (与 WPF 布局一致) → 显示器级 → WPF 视觉树兜底。
            // 三者在 PerMonitorV2 下应一致, 层层兜底只为异常环境不猜数。
            if (hwnd != IntPtr.Zero)
            {
                uint dpi = User32.GetDpiForWindow(hwnd);
                if (dpi != 0)
                {
                    metrics = new DisplayMetrics(dpi / 96.0, dpi / 96.0, mi.rcWork);
                    return true;
                }
            }

            if (User32.GetDpiForMonitor(hMon, User32.MDT_EFFECTIVE_DPI, out uint dpiX, out uint dpiY) == 0
                && dpiX > 0 && dpiY > 0)
            {
                metrics = new DisplayMetrics(dpiX / 96.0, dpiY / 96.0, mi.rcWork);
                return true;
            }

            if (window != null)
            {
                DpiScale wpf = VisualTreeHelper.GetDpi(window);
                metrics = new DisplayMetrics(wpf.DpiScaleX, wpf.DpiScaleY, mi.rcWork);
                return wpf.DpiScaleX > 0;
            }

            return false;
        }
    }
}
