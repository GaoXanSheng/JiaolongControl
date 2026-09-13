using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using JiaoLongControl.Server.Core.Models;
using JiaoLongControl.Server.Core.Native;
using log4net;

namespace JiaoLongControl.Server
{
    /// <summary>
    /// 灵动岛风格 OSD 覆盖窗口: 全圆角胶囊 + 状态强调色光晕。
    /// 进场 = 光晕浮现 → 胶囊滑出展开 → 内容级联渐显 → 光晕呼吸驻留;
    /// 退场 = 内容收拢 → 胶囊压缩为细长光条 → 光效消散。
    /// 点击穿透、不抢焦点、始终置顶; 按显示器 DPI 物理像素定位以兼容系统缩放。
    /// </summary>
    public partial class OsdWindow : Window
    {
        // 布局基准 (DIP): 窗口 440x140, 胶囊 336x40, 胶囊相对窗口居中偏移
        private const double WinW = 440, WinH = 140, PillW = 336, PillH = 40;
        private const double OffsetX = (WinW - PillW) / 2, OffsetY = (WinH - PillH) / 2;
        private const int ExitMs = 620;

        private static readonly ILog Logger = LogManager.GetLogger(typeof(OsdWindow));

        private readonly DispatcherTimer _hideTimer;
        private double _barWidth = 84;
        private DispatcherTimer? _exitTimer;
        private bool _exiting;
        private double _uiScale = 1;

        public OsdWindow()
        {
            InitializeComponent();
            _hideTimer = new DispatcherTimer();
            _hideTimer.Tick += (_, _) =>
            {
                _hideTimer.Stop();
                PlayExit();
            };
            SourceInitialized += (_, _) => ApplyOverlayStyle();
        }

        private void ApplyOverlayStyle()
        {
            try
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                var style = (int)User32.GetWindowLongPtrW(hwnd, User32.GWL_EXSTYLE);
                User32.SetWindowLongPtrW(hwnd, User32.GWL_EXSTYLE, new IntPtr(style
                    | User32.WS_EX_TRANSPARENT
                    | User32.WS_EX_TOOLWINDOW
                    | User32.WS_EX_NOACTIVATE
                    | User32.WS_EX_TOPMOST));
            }
            catch (Exception ex)
            {
                Logger.Warn($"OSD 窗口样式设置失败: {ex.Message}");
            }
        }

        /// <summary>更新内容并按当前状态选择动画 (需在窗口所属 Dispatcher 线程调用)。</summary>
        public void ShowOsd(OsdItem item, string position, int opacityPercent, int durationMs, int scalePercent, bool lightTheme)
        {
            try
            {
                _hideTimer.Stop();
                _exitTimer?.Stop();
                var wasExiting = _exiting;
                _exiting = false;

                ApplyTheme(lightTheme, item.AccentHex, Math.Clamp(opacityPercent, 30, 100) / 100.0);

                IconGlyph.Text = item.IconGlyph;
                TitleText.Text = item.Title;
                SubtitleText.Text = item.Subtitle;
                BarPanel.Visibility = item.BarValue.HasValue ? Visibility.Visible : Visibility.Collapsed;
                if (item.BarValue.HasValue)
                {
                    BarFill.Width = _barWidth * Math.Clamp(item.BarValue.Value, 0, 1);
                }

                var scale = Math.Clamp(scalePercent, 100, 200) / 100.0;
                ApplyLayoutScale(scale);
                MoveToTarget(position, _uiScale);

                if (!IsVisible)
                {
                    // 从隐藏状态首次显示: 播放完整进场动画
                    Show();
                    ReassertTopmost();
                    PlayEntry();
                }
                else if (wasExiting)
                {
                    // 退场动画进行中再次触发: 从当前形态快速恢复驻留
                    ReassertTopmost();
                    RecoverFromExit();
                }
                else
                {
                    // 已处于驻留状态: 仅刷新内容与计时, 不重播进场动画 (避免闪烁)
                    ReassertTopmost();
                }

                _hideTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(500, durationMs));
                _hideTimer.Start();
            }
            catch (Exception ex)
            {
                Logger.Warn($"OSD 显示失败: {ex.Message}");
            }
        }

        // ===== 主题 =====

        /// <summary>
        /// 按用户缩放比例直接设置窗口/面板尺寸与字号 (不做几何变换):
        /// WPF 对带变换的文本会禁用 ClearType, 直接排版才能保证文字清晰。
        /// </summary>
        private void ApplyLayoutScale(double scale)
        {
            _uiScale = scale;
            _barWidth = 84 * scale;

            Width = WinW * scale;
            Height = WinH * scale;
            Halo.Width = 400 * scale;
            Halo.Height = 120 * scale;
            Pill.Width = PillW * scale;
            Pill.Height = PillH * scale;
            Pill.CornerRadius = new CornerRadius(PillH * scale / 2);
            ContentGrid.Margin = new Thickness(14 * scale, 0, 14 * scale, 0);
            IconBox.Width = 26 * scale;
            IconBox.Height = 26 * scale;
            IconBox.Margin = new Thickness(0, 0, 10 * scale, 0);
            IconGlyph.FontSize = 15 * scale;
            TitleText.FontSize = 12 * scale;
            SubtitleText.FontSize = 10 * scale;
            SubtitleText.Margin = new Thickness(0, 2 * scale, 0, 0);
            BarPanel.Width = _barWidth;
            BarPanel.Height = Math.Max(3, 4 * scale);
            BarPanel.Margin = new Thickness(10 * scale, 0, 0, 0);
            BarTrack.CornerRadius = new CornerRadius(2 * scale);
            BarFill.CornerRadius = new CornerRadius(2 * scale);
        }

        private void ApplyTheme(bool light, string accentHex, double panelOpacity)
        {
            var a = TryParseColor(accentHex, Color.FromRgb(0x3B, 0x82, 0xF6));

            HaloStop.Color = Color.FromArgb(0x59, a.R, a.G, a.B);
            IconGlowStop.Color = Color.FromArgb(0x66, a.R, a.G, a.B);
            IconGlyph.Foreground = new SolidColorBrush(a);
            BarFill.Background = new LinearGradientBrush(a, Darken(a, 0.72), 90);

            if (light)
            {
                PanelTop.Color = Color.FromArgb(0xFA, 0xF9, 0xFA, 0xFC);
                PanelBottom.Color = Color.FromArgb(0xF0, 0xEF, 0xF2, 0xF7);
                PanelBorder.Color = Color.FromArgb(0x14, 0x00, 0x00, 0x00);
                TitleText.Foreground = new SolidColorBrush(Color.FromArgb(0xE6, 0x17, 0x18, 0x1C));
                SubtitleText.Foreground = new SolidColorBrush(Color.FromArgb(0x73, 0x17, 0x18, 0x1C));
                BarTrack.Background = new SolidColorBrush(Color.FromArgb(0x1A, 0x00, 0x00, 0x00));
            }
            else
            {
                PanelTop.Color = Color.FromArgb(0xF2, 0x17, 0x17, 0x1C);
                PanelBottom.Color = Color.FromArgb(0xE9, 0x0E, 0x0E, 0x12);
                PanelBorder.Color = Color.FromArgb(0x24, 0xFF, 0xFF, 0xFF);
                TitleText.Foreground = new SolidColorBrush(Color.FromArgb(0xF2, 0xFF, 0xFF, 0xFF));
                SubtitleText.Foreground = new SolidColorBrush(Color.FromArgb(0x8C, 0xFF, 0xFF, 0xFF));
                BarTrack.Background = new SolidColorBrush(Color.FromArgb(0x24, 0xFF, 0xFF, 0xFF));
            }

            // 配置不透明度作为胶囊基础值, 动画 (FillBehavior.Stop) 结束后回落到此值
            Pill.Opacity = panelOpacity;
        }

        // ===== 进场 / 呼吸 / 退场 =====

        private void PlayEntry()
        {
            ResetVisual();
            var easeOut = new CubicEase { EasingMode = EasingMode.EaseOut };

            // 1. 光晕浮现
            Animate(Halo, OpacityProperty, 0, 0.9, 200, 0, easeOut, completed: BeginBreathing);
            Animate(HaloScale, ScaleTransform.ScaleXProperty, 0.6, 1.0, 220, 0, easeOut);
            Animate(HaloScale, ScaleTransform.ScaleYProperty, 0.6, 1.0, 220, 0, easeOut);

            // 2. 形态展开 (轻微过冲)
            var back = new BackEase { Amplitude = 0.35, EasingMode = EasingMode.EaseOut };
            Animate(PillScale, ScaleTransform.ScaleYProperty, 0.15, 1.0, 260, 0, back);
            Animate(PillScale, ScaleTransform.ScaleXProperty, 0.92, 1.0, 260, 0, easeOut);
            Animate(PillTranslate, TranslateTransform.YProperty, -14, 0, 260, 0, easeOut);
            Animate(Pill, OpacityProperty, 0, 1, 140, 0, easeOut);

            // 3. 内容级联渐显
            Animate(IconGlyph, OpacityProperty, 0, 1, 160, 140, easeOut);
            Animate(TitleText, OpacityProperty, 0, 1, 160, 180, easeOut);
            Animate(SubtitleText, OpacityProperty, 0, 1, 160, 220, easeOut);
            Animate(BarPanel, OpacityProperty, 0, 1, 160, 260, easeOut);
        }

        private void BeginBreathing()
        {
            if (_exiting || !IsVisible) return;
            var breath = new DoubleAnimation(0.9, 0.65, TimeSpan.FromMilliseconds(1300))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
            };
            Halo.BeginAnimation(OpacityProperty, breath);
        }

        private void PlayExit()
        {
            if (_exiting || !IsVisible) return;
            _exiting = true;
            StopAnimations();
            var easeIn = new CubicEase { EasingMode = EasingMode.EaseIn };

            // 1. 收起内容
            Animate(IconGlyph, OpacityProperty, null, 0, 140);
            Animate(TitleText, OpacityProperty, null, 0, 140);
            Animate(SubtitleText, OpacityProperty, null, 0, 140);
            Animate(BarPanel, OpacityProperty, null, 0, 140);

            // 2. 形态压缩为细长光条 (Y 压缩基准为 0, ResetVisual 已复位)
            var scaleY = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.HoldEnd };
            scaleY.KeyFrames.Add(new LinearDoubleKeyFrame(0.08, TimeSpan.FromMilliseconds(380)));
            PillScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleY);

            var translateY = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.HoldEnd };
            translateY.KeyFrames.Add(new LinearDoubleKeyFrame(-4, TimeSpan.FromMilliseconds(140)));
            translateY.KeyFrames.Add(new LinearDoubleKeyFrame(-4, TimeSpan.FromMilliseconds(320)));
            translateY.KeyFrames.Add(new LinearDoubleKeyFrame(-10, TimeSpan.FromMilliseconds(560)));
            PillTranslate.BeginAnimation(TranslateTransform.YProperty, translateY);

            // 光晕先增亮再回落消散
            var halo = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.HoldEnd };
            halo.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, TimeSpan.FromMilliseconds(140)));
            halo.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, TimeSpan.FromMilliseconds(320)));
            halo.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, TimeSpan.FromMilliseconds(600)));
            Halo.BeginAnimation(OpacityProperty, halo);
            Animate(HaloScale, ScaleTransform.ScaleXProperty, null, 1.15, 280, 140, easeIn);
            Animate(HaloScale, ScaleTransform.ScaleYProperty, null, 1.15, 280, 140, easeIn);

            // 3. 整体淡出
            Animate(Pill, OpacityProperty, null, 0, 240, 320, easeIn, holdEnd: true);

            // 4. 完全消失 (持有为字段, 退场被打断时可在 ShowOsd 中取消, 避免旧回调误触 Hide)
            _exitTimer?.Stop();
            var done = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ExitMs) };
            _exitTimer = done;
            done.Tick += (_, _) =>
            {
                done.Stop();
                if (_exiting) Hide();
            };
            done.Start();
        }

        private void ResetVisual()
        {
            StopAnimations();
            PillScale.ScaleX = 1;
            PillScale.ScaleY = 1;
            PillTranslate.Y = 0;
            HaloScale.ScaleX = 1;
            HaloScale.ScaleY = 1;
            Halo.Opacity = 0;
            IconGlyph.Opacity = 1;
            TitleText.Opacity = 1;
            SubtitleText.Opacity = 1;
            BarPanel.Opacity = 1;
        }

        /// <summary>
        /// 退场动画进行中再次触发: 捕获当前形态后平滑过渡回驻留状态 (约 200ms),
        /// 不重播完整进场动画。
        /// </summary>
        private void RecoverFromExit()
        {
            // 先捕获当前动画值, 清掉旧动画后再以此作为起始值, 避免跳变
            var curPillOpacity = Pill.Opacity;
            var curScaleY = PillScale.ScaleY;
            var curY = PillTranslate.Y;
            var curHalo = Halo.Opacity;
            var curHaloScale = HaloScale.ScaleX;
            var curIcon = IconGlyph.Opacity;
            var curTitle = TitleText.Opacity;
            var curSubtitle = SubtitleText.Opacity;
            var curBar = BarPanel.Opacity;

            StopAnimations();

            // 基准值直接落到驻留目标值 (而非捕获的退场中间值): Animate 默认
            // FillBehavior.Stop, 动画结束后属性回落到基准值, 恢复才不会弹回退场形态
            var basePillOpacity = Pill.Opacity; // ApplyTheme 设置的配置不透明度
            PillScale.ScaleY = 1;
            PillTranslate.Y = 0;
            Halo.Opacity = 0.9;
            HaloScale.ScaleX = 1;
            HaloScale.ScaleY = 1;
            IconGlyph.Opacity = 1;
            TitleText.Opacity = 1;
            SubtitleText.Opacity = 1;
            BarPanel.Opacity = 1;

            var easeOut = new CubicEase { EasingMode = EasingMode.EaseOut };
            Animate(Pill, OpacityProperty, curPillOpacity, basePillOpacity, 200, 0, easeOut);
            Animate(PillScale, ScaleTransform.ScaleYProperty, curScaleY, 1, 200, 0, easeOut);
            Animate(PillTranslate, TranslateTransform.YProperty, curY, 0, 200, 0, easeOut);
            Animate(Halo, OpacityProperty, curHalo, 0.9, 200, 0, easeOut, completed: BeginBreathing);
            Animate(HaloScale, ScaleTransform.ScaleXProperty, curHaloScale, 1, 200, 0, easeOut);
            Animate(HaloScale, ScaleTransform.ScaleYProperty, curHaloScale, 1, 200, 0, easeOut);
            Animate(IconGlyph, OpacityProperty, curIcon, 1, 200, 0, easeOut);
            Animate(TitleText, OpacityProperty, curTitle, 1, 200, 0, easeOut);
            Animate(SubtitleText, OpacityProperty, curSubtitle, 1, 200, 0, easeOut);
            Animate(BarPanel, OpacityProperty, curBar, 1, 200, 0, easeOut);
        }

        private void StopAnimations()
        {
            Halo.BeginAnimation(OpacityProperty, null);
            HaloScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            HaloScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            Pill.BeginAnimation(OpacityProperty, null);
            PillScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            PillScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            PillTranslate.BeginAnimation(TranslateTransform.YProperty, null);
            IconGlyph.BeginAnimation(OpacityProperty, null);
            TitleText.BeginAnimation(OpacityProperty, null);
            SubtitleText.BeginAnimation(OpacityProperty, null);
            BarPanel.BeginAnimation(OpacityProperty, null);
        }

        private static void Animate(
            IAnimatable target,
            DependencyProperty prop,
            double? from,
            double to,
            int durationMs,
            int delayMs = 0,
            IEasingFunction? ease = null,
            bool holdEnd = false,
            Action? completed = null)
        {
            var anim = new DoubleAnimation
            {
                To = to,
                Duration = TimeSpan.FromMilliseconds(durationMs),
                BeginTime = TimeSpan.FromMilliseconds(delayMs),
                EasingFunction = ease,
                FillBehavior = holdEnd ? FillBehavior.HoldEnd : FillBehavior.Stop,
            };
            if (from.HasValue) anim.From = from.Value;
            if (completed != null) anim.Completed += (_, _) => completed();
            target.BeginAnimation(prop, anim);
        }

        // ===== 定位与置顶 =====

        private void MoveToTarget(string position, double uiScale)
        {
            var hMon = User32.MonitorFromPoint(new User32.POINT { X = 0, Y = 0 }, User32.MONITOR_DEFAULTTOPRIMARY);
            var mi = new User32.MONITORINFO { cbSize = Marshal.SizeOf<User32.MONITORINFO>() };
            if (!User32.GetMonitorInfoW(hMon, ref mi)) return;
            if (User32.GetDpiForMonitor(hMon, User32.MDT_EFFECTIVE_DPI, out var dpi, out _) != 0) return;

            var dip = dpi / 96.0;
            var pillPxW = PillW * dip * uiScale;
            var pillPxH = PillH * dip * uiScale;    
            var margin = 28 * dip;

            double pillLeft, pillTop;
            switch (position)
            {       
                case "TopRight":
                    pillLeft = mi.rcWork.Right - margin - pillPxW;
                    pillTop = mi.rcWork.Top + margin;
                    break;
                case "BottomCenter":
                    pillLeft = mi.rcWork.Left + ((mi.rcWork.Right - mi.rcWork.Left) - pillPxW) / 2;
                    pillTop = mi.rcWork.Bottom - margin - pillPxH;
                    break;
                default: // TopCenter
                    pillLeft = mi.rcWork.Left + ((mi.rcWork.Right - mi.rcWork.Left) - pillPxW) / 2;
                    pillTop = mi.rcWork.Top + margin;
                    break;
            }

            // 窗口左上角 = 胶囊位置 - 胶囊在窗口内的居中偏移; 物理像素 → 该显示器 DIP
            Left = (pillLeft - OffsetX * dip * uiScale) / dip;
            Top = (pillTop - OffsetY * dip * uiScale) / dip;
        }

        private void ReassertTopmost()
        {
            try
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd != IntPtr.Zero)
                    User32.SetWindowPos(hwnd, User32.HWND_TOPMOST, 0, 0, 0, 0,
                        User32.SWP_NOMOVE | User32.SWP_NOSIZE | User32.SWP_NOACTIVATE);
            }
            catch
            {
                // 忽略置顶失败
            }
        }

        private static Color TryParseColor(string hex, Color fallback)
        {
            try
            {
                return (Color)ColorConverter.ConvertFromString(hex);
            }
            catch
            {
                return fallback;
            }
        }

        private static Color Darken(Color c, double factor) =>
            Color.FromRgb((byte)(c.R * factor), (byte)(c.G * factor), (byte)(c.B * factor));
    }
}
