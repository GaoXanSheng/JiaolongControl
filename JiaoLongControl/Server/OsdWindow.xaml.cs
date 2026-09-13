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
    /// 进场 = 收拢的小圆长成正圆 → 两边水平展开成胶囊 + 光晕浮现 → 内容级联渐显 → 呼吸驻留;
    /// 退场 = 内容收拢 → 两边向中心收缩成一个圆 → 圆体收拢消散;
    /// 出入场动画各占 AnimationMs, 完全展开后驻留 DurationMs (展开 → 驻留 → 收圆),
    /// 打断时从当前形态恢复;
    /// 悬停柔光 + 悬停挂起自动隐藏, 鼠标离开才触发离场。
    /// 点击穿透、不抢焦点、始终置顶; 按显示器 DPI 物理像素定位以兼容系统缩放。
    /// </summary>
    public partial class OsdWindow : Window
    {
        // 布局基准 (DIP): 窗口 440x140, 胶囊 336x40, 胶囊相对窗口居中偏移
        private const double WinW = 440, WinH = 140, PillW = 336, PillH = 40;

        private const double OffsetX = (WinW - PillW) / 2, OffsetY = (WinH - PillH) / 2;

        // 退场基准编排总时长: 实际时长 = 显示时长, 各关键帧按 显示时长/620 等比缩放
        private const int ExitMs = 620;

        private static readonly ILog Logger = LogManager.GetLogger(typeof(OsdWindow));
        private readonly DispatcherTimer _cursorTimer;

        private readonly DispatcherTimer _hideTimer;

        // 入场/出场动画的总时长 (= 配置的显示时长), 各阶段节奏按基准编排等比缩放
        private double _animMs = 2000;
        private double _barWidth = 84;
        private double _dip = 1;
        private DispatcherTimer? _exitTimer;
        private bool _exiting;
        private bool _hoverInside;
        private double _lightOpacity;
        private double _lightOpacityTarget;

        // 悬停柔光状态: 光斑位置为胶囊归一化坐标, 轮询中做指数平滑
        private Point _lightPos = new(0.5, 0.5);
        private Point _lightTarget = new(0.5, 0.5);
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
            _cursorTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _cursorTimer.Tick += (_, _) => UpdateHoverLight();
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
        public void ShowOsd(OsdItem item, string position, int opacityPercent, int durationMs, int animMs, int scalePercent, bool lightTheme)
        {
            try
            {
                _hideTimer.Stop();
                _exitTimer?.Stop();
                var wasExiting = _exiting;
                _exiting = false;
                _animMs = Math.Clamp(animMs, 200, 5000);

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

                StartHoverTracking();

                // 驻留计时 = 入场时长 + 显示时长: 显示时长为完全展开后的纯驻留时间,
                // 到点后播放与入场等长的出场动画 (悬停挂起, 离开即收)
                _hideTimer.Interval = TimeSpan.FromMilliseconds(_animMs + Math.Max(500, durationMs));
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
            HoverGlow.CornerRadius = new CornerRadius(PillH * scale / 2);
            // 圆形光源: 绝对半径 = 胶囊高度一半 (居中时恰好完整容纳于胶囊内)
            HoverGlowBrush.RadiusX = PillH * scale / 2;
            HoverGlowBrush.RadiusY = PillH * scale / 2;
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
                // 浅色面板上白光不可见, 柔光改用强调色染色 (彩色光源照射感)
                HoverGlowStop.Color = Color.FromArgb(0x3C, a.R, a.G, a.B);
                PanelTop.Color = Color.FromArgb(0xFA, 0xF9, 0xFA, 0xFC);
                PanelBottom.Color = Color.FromArgb(0xF0, 0xEF, 0xF2, 0xF7);
                PanelBorder.Color = Color.FromArgb(0x14, 0x00, 0x00, 0x00);
                TitleText.Foreground = new SolidColorBrush(Color.FromArgb(0xE6, 0x17, 0x18, 0x1C));
                SubtitleText.Foreground = new SolidColorBrush(Color.FromArgb(0x73, 0x17, 0x18, 0x1C));
                BarTrack.Background = new SolidColorBrush(Color.FromArgb(0x1A, 0x00, 0x00, 0x00));
            }
            else
            {
                HoverGlowStop.Color = Color.FromArgb(0x45, 0xFF, 0xFF, 0xFF);
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
            // 基准编排总时长 480ms, 整体等比拉伸到显示时长
            int D(int baseMs) => Math.Max(1, (int)Math.Round(baseMs * _animMs / 480.0));

            // 1. 光晕浮现
            Animate(Halo, OpacityProperty, 0, 0.9, D(200), 0, easeOut, completed: BeginBreathing);
            Animate(HaloScale, ScaleTransform.ScaleXProperty, 0.6, 1.0, D(220), 0, easeOut);
            Animate(HaloScale, ScaleTransform.ScaleYProperty, 0.6, 1.0, D(220), 0, easeOut);

            // 2. 一个圆向两边水平展开 (与退场对称: 先从收拢的小圆长成正圆, 再展开成胶囊;
            //    与退场同理动画 Width 而非缩放变换 — 非等比缩放会把圆角拉扁, 无法保持正圆)
            // 宽高在前段 D(160) 同速等比生长 (0.78→1.0, 严格保持正圆不变形),
            // 之后宽度单独向两边展开横跨到满时长 (D(480) 即 _animMs),
            // EaseInOut + 末端轻过冲: 动作均匀铺满全程
            var circleW = PillH * _uiScale;
            var back = new BackEase { Amplitude = 0.25, EasingMode = EasingMode.EaseInOut };

            var widthAnim = new DoubleAnimationUsingKeyFrames();
            widthAnim.KeyFrames.Add(new EasingDoubleKeyFrame(circleW * 0.78, TimeSpan.Zero));
            widthAnim.KeyFrames.Add(new EasingDoubleKeyFrame(circleW, TimeSpan.FromMilliseconds(D(160)), easeOut));
            widthAnim.KeyFrames.Add(new EasingDoubleKeyFrame(PillW * _uiScale, TimeSpan.FromMilliseconds(D(480)), back));
            Pill.BeginAnimation(WidthProperty, widthAnim);

            var heightAnim = new DoubleAnimationUsingKeyFrames();
            heightAnim.KeyFrames.Add(new EasingDoubleKeyFrame(circleW * 0.78, TimeSpan.Zero));
            heightAnim.KeyFrames.Add(new EasingDoubleKeyFrame(circleW, TimeSpan.FromMilliseconds(D(160)), easeOut));
            Pill.BeginAnimation(HeightProperty, heightAnim);

            Animate(PillTranslate, TranslateTransform.YProperty, -14, 0, D(280), 0, easeOut);
            Animate(Pill, OpacityProperty, 0, 1, D(120), 0, easeOut);

            // 3. 内容级联渐显 (关键帧写法: 延迟期内保持透明。若仅用 BeginTime 延迟,
            //    动画未激活的间隙会显示基准值 1, 内容会在初始小圆里提前闪现)
            void FadeIn(UIElement el, int delayBase, int durBase) =>
                el.BeginAnimation(OpacityProperty, new DoubleAnimationUsingKeyFrames
                {
                    KeyFrames =
                    {
                        new DiscreteDoubleKeyFrame(0, TimeSpan.Zero),
                        new DiscreteDoubleKeyFrame(0, TimeSpan.FromMilliseconds(D(delayBase))),
                        new EasingDoubleKeyFrame(1, TimeSpan.FromMilliseconds(D(delayBase) + D(durBase)), easeOut),
                    },
                });
            FadeIn(IconBox, 200, 160);
            FadeIn(TitleText, 240, 160);
            FadeIn(SubtitleText, 280, 160);
            FadeIn(BarPanel, 320, 160);
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
            // 基准编排总时长 620ms (ExitMs), 整体等比拉伸到显示时长, 与入场对等
            int D(int baseMs) => Math.Max(1, (int)Math.Round(baseMs * _animMs / 620.0));

            // 1. 收起内容 (整个图标容器含 IconGlow 光晕一并隐藏; holdEnd 保持隐藏直到
            //    窗口隐藏/被打断恢复, 否则 FillBehavior.Stop 会在动画结束后弹回可见,
            //    收缩成圆时留下内容残影)
            Animate(IconBox, OpacityProperty, null, 0, D(140), holdEnd: true);
            Animate(TitleText, OpacityProperty, null, 0, D(140), holdEnd: true);
            Animate(SubtitleText, OpacityProperty, null, 0, D(140), holdEnd: true);
            Animate(BarPanel, OpacityProperty, null, 0, D(140), holdEnd: true);

            // 2. 两边向中心收缩成一个圆: 动画 Width 而非缩放变换 — 非等比缩放会把
            //    圆角一起拉扁 (缩到 40x40 时四角是椭圆弧), 圆角固定为高度一半不动,
            //    宽度收到与高度相等时恰为正圆; 居中对齐保证两边对称内收。
            //    收缩动作铺满全程前段 (D(480)/D(620)), 与入场展开对等
            var circleW = PillH * _uiScale;
            var widthAnim = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.HoldEnd };
            widthAnim.KeyFrames.Add(new EasingDoubleKeyFrame(circleW, TimeSpan.FromMilliseconds(D(480)),
                new CubicEase { EasingMode = EasingMode.EaseInOut }));
            widthAnim.KeyFrames.Add(new EasingDoubleKeyFrame(circleW * 0.78, TimeSpan.FromMilliseconds(D(560))));
            Pill.BeginAnimation(WidthProperty, widthAnim);

            // 圆形成后与宽度同步轻微收拢, 保持正圆形态消隐
            var heightAnim = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.HoldEnd };
            heightAnim.KeyFrames.Add(new EasingDoubleKeyFrame(circleW, TimeSpan.FromMilliseconds(D(480))));
            heightAnim.KeyFrames.Add(new EasingDoubleKeyFrame(circleW * 0.78, TimeSpan.FromMilliseconds(D(560))));
            Pill.BeginAnimation(HeightProperty, heightAnim);

            var translateY = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.HoldEnd };
            translateY.KeyFrames.Add(new LinearDoubleKeyFrame(-4, TimeSpan.FromMilliseconds(D(140))));
            translateY.KeyFrames.Add(new LinearDoubleKeyFrame(-4, TimeSpan.FromMilliseconds(D(460))));
            translateY.KeyFrames.Add(new LinearDoubleKeyFrame(-10, TimeSpan.FromMilliseconds(D(560))));
            PillTranslate.BeginAnimation(TranslateTransform.YProperty, translateY);

            // 光晕先增亮再回落消散
            var halo = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.HoldEnd };
            halo.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, TimeSpan.FromMilliseconds(D(140))));
            halo.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, TimeSpan.FromMilliseconds(D(320))));
            halo.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, TimeSpan.FromMilliseconds(D(600))));
            Halo.BeginAnimation(OpacityProperty, halo);
            Animate(HaloScale, ScaleTransform.ScaleXProperty, null, 1.15, D(280), D(140), easeIn);
            Animate(HaloScale, ScaleTransform.ScaleYProperty, null, 1.15, D(280), D(140), easeIn);

            // 3. 圆体淡出 (圆形成后开始消隐, 与光晕消散同步收尾)
            Animate(Pill, OpacityProperty, null, 0, D(140), D(460), easeIn, holdEnd: true);

            // 4. 完全消失 (持有为字段, 退场被打断时可在 ShowOsd 中取消, 避免旧回调误触 Hide)
            _exitTimer?.Stop();
            var done = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(D(ExitMs)) };
            _exitTimer = done;
            done.Tick += (_, _) =>
            {
                done.Stop();
                if (_exiting)
                {
                    StopHoverTracking();
                    Hide();
                }
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
            IconBox.Opacity = 1;
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
            var curWidth = Pill.Width;
            var curHeight = Pill.Height;
            var curScaleX = PillScale.ScaleX;
            var curScaleY = PillScale.ScaleY;
            var curY = PillTranslate.Y;
            var curHalo = Halo.Opacity;
            var curHaloScale = HaloScale.ScaleX;
            var curIcon = IconBox.Opacity;
            var curTitle = TitleText.Opacity;
            var curSubtitle = SubtitleText.Opacity;
            var curBar = BarPanel.Opacity;

            StopAnimations();

            // 基准值直接落到驻留目标值 (而非捕获的退场中间值): Animate 默认
            // FillBehavior.Stop, 动画结束后属性回落到基准值, 恢复才不会弹回退场形态
            var basePillOpacity = Pill.Opacity; // ApplyTheme 设置的配置不透明度
            PillScale.ScaleX = 1;
            PillScale.ScaleY = 1;
            PillTranslate.Y = 0;
            Halo.Opacity = 0.9;
            HaloScale.ScaleX = 1;
            HaloScale.ScaleY = 1;
            IconBox.Opacity = 1;
            IconGlyph.Opacity = 1;
            TitleText.Opacity = 1;
            SubtitleText.Opacity = 1;
            BarPanel.Opacity = 1;

            var easeOut = new CubicEase { EasingMode = EasingMode.EaseOut };
            Animate(Pill, OpacityProperty, curPillOpacity, basePillOpacity, 200, 0, easeOut);
            Animate(Pill, WidthProperty, curWidth, PillW * _uiScale, 200, 0, easeOut);
            Animate(Pill, HeightProperty, curHeight, PillH * _uiScale, 200, 0, easeOut);
            Animate(PillScale, ScaleTransform.ScaleXProperty, curScaleX, 1, 200, 0, easeOut);
            Animate(PillScale, ScaleTransform.ScaleYProperty, curScaleY, 1, 200, 0, easeOut);
            Animate(PillTranslate, TranslateTransform.YProperty, curY, 0, 200, 0, easeOut);
            Animate(Halo, OpacityProperty, curHalo, 0.9, 200, 0, easeOut, completed: BeginBreathing);
            Animate(HaloScale, ScaleTransform.ScaleXProperty, curHaloScale, 1, 200, 0, easeOut);
            Animate(HaloScale, ScaleTransform.ScaleYProperty, curHaloScale, 1, 200, 0, easeOut);
            Animate(IconBox, OpacityProperty, curIcon, 1, 200, 0, easeOut);
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
            Pill.BeginAnimation(WidthProperty, null);
            Pill.BeginAnimation(HeightProperty, null);
            PillScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            PillScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            PillTranslate.BeginAnimation(TranslateTransform.YProperty, null);
            IconBox.BeginAnimation(OpacityProperty, null);
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
            _dip = dip; // 供悬停柔光做光标物理像素 → 窗口 DIP 换算
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

        // ===== 悬停柔光: 光标轮询驱动 (窗口 WS_EX_TRANSPARENT 收不到鼠标消息) =====

        private void StartHoverTracking()
        {
            if (_cursorTimer.IsEnabled) return; // 已在运行: 保留光斑状态, 驻留刷新时不闪烁
            _lightPos = new Point(0.5, 0.5);
            _lightTarget = _lightPos;
            _lightOpacity = 0;
            _lightOpacityTarget = 0;
            HoverGlow.Opacity = 0;
            _cursorTimer.Start();
        }

        private void StopHoverTracking()
        {
            _cursorTimer.Stop();
            _hoverInside = false;
            _lightOpacity = 0;
            _lightOpacityTarget = 0;
            HoverGlow.Opacity = 0;
        }

        private void UpdateHoverLight()
        {
            if (!IsVisible)
            {
                StopHoverTracking();
                return;
            }

            // 光标物理像素 → 窗口 DIP → 胶囊归一化坐标 (与 MoveToTarget 同用主显示器 DPI)
            var inside = false;
            var nx = 0.5;
            var ny = 0.5;
            if (_dip > 0 && User32.GetCursorPos(out var pt))
            {
                var mx = pt.X / _dip - Left;
                var my = pt.Y / _dip - Top;
                var pillX = OffsetX * _uiScale;
                var pillY = OffsetY * _uiScale;
                var pillW = PillW * _uiScale;
                var pillH = PillH * _uiScale;
                const double margin = 8; // DIP: 光斑贴到胶囊边缘外一点仍可见
                inside = mx >= pillX - margin && mx <= pillX + pillW + margin
                      && my >= pillY - margin && my <= pillY + pillH + margin;
                if (inside)
                {
                    nx = Math.Clamp((mx - pillX) / pillW, 0, 1);
                    ny = Math.Clamp((my - pillY) / pillH, 0, 1);
                }
            }

            _lightTarget = inside ? new Point(nx, ny) : _lightTarget;
            _lightOpacityTarget = inside ? 1 : 0;

            // 悬停期间挂起自动隐藏, 鼠标离开时才触发离场动画
            if (inside != _hoverInside)
            {
                _hoverInside = inside;
                if (inside)
                {
                    _hideTimer.Stop();
                }
                else
                {
                    PlayExit();
                }
            }

            // 指数平滑: 位置轻微滞后产生柔光跟随感, 不透明度缓入缓出
            _lightPos = new Point(
                _lightPos.X + (_lightTarget.X - _lightPos.X) * 0.35,
                _lightPos.Y + (_lightTarget.Y - _lightPos.Y) * 0.35);
            _lightOpacity += (_lightOpacityTarget - _lightOpacity) * 0.2;

            // Absolute 映射: 光源中心用胶囊内 DIP 像素坐标, 保证是正圆
            var gx = _lightPos.X * HoverGlow.ActualWidth;
            var gy = _lightPos.Y * HoverGlow.ActualHeight;
            HoverGlowBrush.GradientOrigin = new Point(gx, gy);
            HoverGlowBrush.Center = new Point(gx, gy);
            HoverGlow.Opacity = _lightOpacity;
        }

        protected override void OnClosed(EventArgs e)
        {
            StopHoverTracking();
            base.OnClosed(e);
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
