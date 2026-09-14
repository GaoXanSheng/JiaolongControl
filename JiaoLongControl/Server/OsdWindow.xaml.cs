using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
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
    /// 悬停柔光 + 悬停挂起自动隐藏, 鼠标离开后按显示时长重新计时, 到点才触发离场。
    /// 点击穿透、不抢焦点、始终置顶; 按显示器 DPI 物理像素定位以兼容系统缩放。
    /// 交互型 OSD (音量/键盘背光/媒体) 显示期间动态解除点击穿透:
    /// 胶囊内的滑条/分段/按钮可直接操作, 胶囊外空区仍穿透, 且全程不抢焦点。
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

        // ===== 交互状态 =====

        // 主题缓存: 背光分段激活色 / 未激活色 / 音量条基准高度
        private Color _accentColor = Color.FromRgb(0x3B, 0x82, 0xF6);

        // 入场/出场动画的总时长 (= 配置的出入场时长, 默认 480ms), 各阶段节奏按基准编排等比缩放
        private double _animMs = 480;
        private double _barWidth = 84;
        private double _dip = 1;

        // 驻留时长 (= 配置的显示时长): 悬停离开后按此时长重新计时, 到点才播离场动画
        private double _dwellMs = 2000;
        private DispatcherTimer? _exitTimer;
        private bool _exiting;
        private SolidColorBrush _faintBrush = new(Color.FromArgb(0x24, 0xFF, 0xFF, 0xFF));
        private bool _hoverInside;
        private double _lightOpacity;
        private double _lightOpacityTarget;

        // 悬停柔光状态: 光斑位置为胶囊归一化坐标, 轮询中做指数平滑
        private Point _lightPos = new(0.5, 0.5);
        private Point _lightTarget = new(0.5, 0.5);

        // ===== 标题平移: 媒体 OSD 长曲名超出可视区时整体前移, 末字完整显示后停止 =====

        // 入场/恢复动画期间的延迟测量计时器 (过早测量可视区宽度不正确)
        private DispatcherTimer? _marqueeDelayTimer;

        // 按住/拖动交互控件期间不触发退场 (光标可能暂时离开胶囊判定区)
        private bool _pressing;
        private double _uiScale = 1;
        private double _volumeBarH = 4;
        private bool _volumeDragging;

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

        /// <summary>当前驻留的 OSD 类型 (供控制器判断是否需要原位刷新)。</summary>
        public string CurrentKind { get; private set; } = "";

        /// <summary>
        /// 窗口可见且正驻留指定类型的 OSD, 且未在退场。
        /// 供控制器区分"原位快刷"与"首次防抖显示" (退场中需走恢复流程, 不算驻留)。
        /// </summary>
        public bool IsDwellingKind(string kind) => IsVisible && !_exiting && CurrentKind == kind;

        /// <summary>音量滑条被拖动/点击到某比例 (0~1, UI 线程回调)。</summary>
        public event Action<double>? VolumeSelected;

        /// <summary>静音小按钮被点击 (由控制器读取当前状态并取反)。</summary>
        public event Action? MuteToggled;

        /// <summary>背光分段被点击 (0~3 档)。</summary>
        public event Action<byte>? BrightnessSelected;

        /// <summary>媒体按钮被点击: prev / playpause / next。</summary>
        public event Action<string>? MediaCommand;

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

        /// <summary>动态切换点击穿透: 交互型 OSD 显示期间接收鼠标, 其余时间保持穿透。</summary>
        private void SetClickThrough(bool clickThrough)
        {
            try
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd == IntPtr.Zero) return;
                var style = (int)User32.GetWindowLongPtrW(hwnd, User32.GWL_EXSTYLE);
                var updated = clickThrough
                    ? style | User32.WS_EX_TRANSPARENT
                    : style & ~User32.WS_EX_TRANSPARENT;
                if (updated != style)
                    User32.SetWindowLongPtrW(hwnd, User32.GWL_EXSTYLE, new IntPtr(updated));
            }
            catch (Exception ex)
            {
                Logger.Warn($"OSD 点击穿透切换失败: {ex.Message}");
            }
        }

        /// <summary>更新内容并按当前状态选择动画 (需在窗口所属 Dispatcher 线程调用)。</summary>
        public void ShowOsd(OsdItem item, OsdSection cfg, bool lightTheme)
        {
            try
            {
                _hideTimer.Stop();
                _exitTimer?.Stop();
                var wasExiting = _exiting;
                _exiting = false;
                _animMs = Math.Clamp(cfg.AnimationMs, 200, 5000);

                ApplyTheme(lightTheme, item.AccentHex, Math.Clamp(cfg.Opacity, 30, 100) / 100.0);

                CurrentKind = item.Kind;
                IconGlyph.Text = item.IconGlyph;
                TitleText.Text = item.Title;
                SubtitleText.Text = item.Subtitle;
                StopTitleMarquee(); // 新内容先复位标题平移, 避免带着上一首的偏移/动画显示

                // PNG 图标优先 (强调色遮罩渲染), 加载失败回退字形
                var iconSource = TryLoadIconImage(item.IconImage);
                IconImageBrush.ImageSource = iconSource;
                IconImage.Visibility = iconSource != null ? Visibility.Visible : Visibility.Collapsed;
                IconGlyph.Visibility = iconSource != null ? Visibility.Collapsed : Visibility.Visible;

                var scale = Math.Clamp(cfg.Scale, 100, 200) / 100.0;
                ApplyLayoutScale(scale);
                MoveToTarget(cfg.Position, cfg.CustomX, cfg.CustomY, _uiScale);

                // 交互控件切换 (依赖缩放后的基准尺寸, 故放在 ApplyLayoutScale 之后)
                var interactive = item.Kind is "volume" or "keyboard" or "media";
                VolumePanel.Visibility = item.Kind == "volume" ? Visibility.Visible : Visibility.Collapsed;
                BrightnessPanel.Visibility = item.Kind == "keyboard" ? Visibility.Visible : Visibility.Collapsed;
                MediaPanel.Visibility = item.Kind == "media" ? Visibility.Visible : Visibility.Collapsed;
                switch (item.Kind)
                {
                    case "volume":
                        VolumeFill.Width = _barWidth * Math.Clamp(item.BarValue ?? 0, 0, 1);
                        MuteGlyph.Text = item.IsMuted ? "\uE74F" : "\uE767";
                        break;
                    case "keyboard":
                        UpdateSegments(item.BrightnessLevel);
                        break;
                    case "media":
                        PlayPauseGlyph.Text = item.IsPlaying ? "\uE769" : "\uE768";
                        break;
                }

                int marqueeDelayMs;
                if (!IsVisible)
                {
                    // 从隐藏状态首次显示: 播放完整进场动画
                    Show();
                    ReassertTopmost();
                    PlayEntry();
                    // 胶囊宽度由入场动画从圆形展开到满宽, 必须等展开结束再测可视区
                    marqueeDelayMs = (int)_animMs + 150;
                }
                else if (wasExiting)
                {
                    // 退场动画进行中再次触发: 从当前形态快速恢复驻留
                    ReassertTopmost();
                    RecoverFromExit();
                    marqueeDelayMs = 350; // 恢复动画约 200ms
                }
                else
                {
                    // 已处于驻留状态: 仅刷新内容与计时, 不重播进场动画 (避免闪烁)
                    ReassertTopmost();
                    marqueeDelayMs = 0;
                }

                // 穿透切换放在 Show 之后: 首次显示时窗口句柄此时才存在
                SetClickThrough(!interactive);
                StartHoverTracking();
                ScheduleTitleMarquee(marqueeDelayMs);

                // 驻留计时 = 入场时长 + 显示时长: 显示时长为完全展开后的纯驻留时间,
                // 到点后播放与入场等长的出场动画 (悬停挂起, 离开后按显示时长重新计时)
                _dwellMs = Math.Max(500, cfg.DurationMs);
                _hideTimer.Interval = TimeSpan.FromMilliseconds(_animMs + _dwellMs);
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
            IconImage.Width = 18 * scale;
            IconImage.Height = 18 * scale;
            TitleText.FontSize = 12 * scale;
            TitleCanvas.Height = Math.Ceiling(TitleText.FontSize * 1.5); // Canvas 不自动撑开, 需显式行高
            SubtitleText.FontSize = 10 * scale;
            SubtitleText.Margin = new Thickness(0, 2 * scale, 0, 0);

            // 音量交互: 滑条命中区 + 静音按钮
            VolumeSliderHit.Width = 84 * scale;
            VolumeSliderHit.Height = Math.Max(16, 22 * scale);
            VolumeBar.Height = _volumeBarH = 4 * scale;
            VolumeTrack.CornerRadius = new CornerRadius(2 * scale);
            VolumeFill.CornerRadius = new CornerRadius(2 * scale);
            MuteButton.Width = 22 * scale;
            MuteButton.Height = 22 * scale;
            MuteButton.CornerRadius = new CornerRadius(11 * scale);
            MuteButton.Margin = new Thickness(6 * scale, 0, 0, 0);
            MuteGlyph.FontSize = 12 * scale;

            // 背光分段
            var segs = new[] { Seg0, Seg1, Seg2, Seg3 };
            var segFills = new[] { Seg0Fill, Seg1Fill, Seg2Fill, Seg3Fill };
            for (var i = 0; i < segs.Length; i++)
            {
                segs[i].Width = 18 * scale;
                segs[i].Height = 16 * scale;
                segs[i].Margin = i == 0 ? default : new Thickness(3 * scale, 0, 0, 0);
                segFills[i].Height = 8 * scale;
                segFills[i].CornerRadius = new CornerRadius(2 * scale);
            }

            // 媒体按钮
            var mediaBtns = new[] { MediaPrev, MediaPlayPause, MediaNext };
            var mediaGlyphs = new[] { MediaPrevGlyph, PlayPauseGlyph, MediaNextGlyph };
            for (var i = 0; i < mediaBtns.Length; i++)
            {
                mediaBtns[i].Width = 22 * scale;
                mediaBtns[i].Height = 22 * scale;
                mediaBtns[i].CornerRadius = new CornerRadius(11 * scale);
                mediaBtns[i].Margin = i == 0 ? default : new Thickness(3 * scale, 0, 0, 0);
                mediaGlyphs[i].FontSize = 12 * scale;
            }
        }

        private void ApplyTheme(bool light, string accentHex, double panelOpacity)
        {
            var a = TryParseColor(accentHex, Color.FromRgb(0x3B, 0x82, 0xF6));
            _accentColor = a;

            HaloStop.Color = Color.FromArgb(0x59, a.R, a.G, a.B);
            IconGlowStop.Color = Color.FromArgb(0x66, a.R, a.G, a.B);
            IconGlyph.Foreground = new SolidColorBrush(a);
            IconImage.Fill = new SolidColorBrush(a); // PNG 剪影经遮罩染成强调色

            // 交互控件前景与底色
            var glyphFg = new SolidColorBrush(light
                ? Color.FromArgb(0xE6, 0x17, 0x18, 0x1C)
                : Color.FromArgb(0xF2, 0xFF, 0xFF, 0xFF));
            MuteGlyph.Foreground = glyphFg;
            MediaPrevGlyph.Foreground = glyphFg;
            PlayPauseGlyph.Foreground = glyphFg;
            MediaNextGlyph.Foreground = glyphFg;
            _faintBrush = new SolidColorBrush(light
                ? Color.FromArgb(0x1A, 0x00, 0x00, 0x00)
                : Color.FromArgb(0x24, 0xFF, 0xFF, 0xFF));
            VolumeTrack.Background = _faintBrush;
            VolumeFill.Background = new LinearGradientBrush(a, Darken(a, 0.72), 90);

            if (light)
            {
                // 浅色面板上白光不可见, 柔光改用强调色染色 (彩色光源照射感)
                HoverGlowStop.Color = Color.FromArgb(0x3C, a.R, a.G, a.B);
                PanelTop.Color = Color.FromArgb(0xFA, 0xF9, 0xFA, 0xFC);
                PanelBottom.Color = Color.FromArgb(0xF0, 0xEF, 0xF2, 0xF7);
                PanelBorder.Color = Color.FromArgb(0x14, 0x00, 0x00, 0x00);
                TitleText.Foreground = new SolidColorBrush(Color.FromArgb(0xE6, 0x17, 0x18, 0x1C));
                SubtitleText.Foreground = new SolidColorBrush(Color.FromArgb(0x73, 0x17, 0x18, 0x1C));
            }
            else
            {
                HoverGlowStop.Color = Color.FromArgb(0x45, 0xFF, 0xFF, 0xFF);
                PanelTop.Color = Color.FromArgb(0xF2, 0x17, 0x17, 0x1C);
                PanelBottom.Color = Color.FromArgb(0xE9, 0x0E, 0x0E, 0x12);
                PanelBorder.Color = Color.FromArgb(0x24, 0xFF, 0xFF, 0xFF);
                TitleText.Foreground = new SolidColorBrush(Color.FromArgb(0xF2, 0xFF, 0xFF, 0xFF));
                SubtitleText.Foreground = new SolidColorBrush(Color.FromArgb(0x8C, 0xFF, 0xFF, 0xFF));
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
                    SetClickThrough(true); // 隐藏后恢复穿透, 避免空窗口残留挡鼠标
                    _marqueeDelayTimer?.Stop();
                    StopTitleMarquee(); // 停掉跑马灯, 避免隐藏后空转动画
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

        private void MoveToTarget(string position, int customX, int customY, double uiScale)
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
                case "BottomCenter":
                    pillLeft = mi.rcWork.Left + ((mi.rcWork.Right - mi.rcWork.Left) - pillPxW) / 2;
                    pillTop = mi.rcWork.Bottom - margin - pillPxH;
                    break;
                case "Custom":
                    // 自定义位置 = 胶囊在主屏工作区"可移动行程"的百分比 (0% 贴左/上边缘, 100% 贴右/下边缘),
                    // 与界面缩放无关; 与设置页预览区的映射保持一致
                    var travelW = Math.Max(0, mi.rcWork.Right - mi.rcWork.Left - pillPxW);
                    var travelH = Math.Max(0, mi.rcWork.Bottom - mi.rcWork.Top - pillPxH);
                    pillLeft = mi.rcWork.Left + travelW * Math.Clamp(customX, 0, 100) / 100.0;
                    pillTop = mi.rcWork.Top + travelH * Math.Clamp(customY, 0, 100) / 100.0;
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

        // ===== 悬停柔光: 光标轮询驱动 =====

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

        /// <summary>悬停离开后按配置的显示时长重新计时, 到点由计时器触发离场动画。</summary>
        private void RestartDwellTimer()
        {
            _hideTimer.Stop();
            _hideTimer.Interval = TimeSpan.FromMilliseconds(_dwellMs);
            _hideTimer.Start();
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

            // 悬停期间挂起自动隐藏; 按住交互控件时即使光标暂离也不退场;
            // 光标离开后按显示时长重新计时, 到点才触发离场动画
            if (inside != _hoverInside)
            {
                _hoverInside = inside;
                if (inside)
                {
                    _hideTimer.Stop();
                }
                else if (!_pressing)
                {
                    RestartDwellTimer();
                }
            }
            else if (inside && _hideTimer.IsEnabled)
            {
                // 悬停中刷新内容 (如连续按键) 会重启计时: 持续兜底挂起, 避免悬停中被收回
                _hideTimer.Stop();
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

        // ===== 交互: 音量滑条 / 背光分段 / 媒体按钮 =====

        private void VolumeSliderHit_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _pressing = true;
            _volumeDragging = true;
            _hideTimer.Stop();
            VolumeSliderHit.CaptureMouse();
            ApplyVolumeFromPosition(e.GetPosition(VolumeSliderHit).X);
            e.Handled = true;
        }

        private void VolumeSliderHit_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_volumeDragging) return;
            ApplyVolumeFromPosition(e.GetPosition(VolumeSliderHit).X);
        }

        private void VolumeSliderHit_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_volumeDragging) return;
            _volumeDragging = false;
            _pressing = false;
            if (VolumeSliderHit.IsMouseCaptured) VolumeSliderHit.ReleaseMouseCapture();
            VolumeBar.Height = _volumeBarH;
            e.Handled = true;
            if (_hoverInside)
            {
                _hideTimer.Stop(); // 仍在胶囊上: 交回悬停挂起逻辑
            }
            else
            {
                RestartDwellTimer(); // 已离开胶囊: 按显示时长重新计时后退场
            }
        }

        private void VolumeSliderHit_MouseEnter(object sender, MouseEventArgs e)
        {
            if (!_volumeDragging) VolumeBar.Height = _volumeBarH * 1.5; // 悬停增粗提示可拖
        }

        private void VolumeSliderHit_MouseLeave(object sender, MouseEventArgs e)
        {
            if (!_volumeDragging) VolumeBar.Height = _volumeBarH;
        }

        private void ApplyVolumeFromPosition(double x)
        {
            var ratio = Math.Clamp(x / Math.Max(1, VolumeSliderHit.ActualWidth), 0, 1);
            VolumeFill.Width = _barWidth * ratio;
            SubtitleText.Text = $"{Math.Round(ratio * 100)}%";
            VolumeSelected?.Invoke(ratio);
        }

        private void MuteButton_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            MuteToggled?.Invoke();
        }

        private void Segment_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border seg
                && byte.TryParse(seg.Tag as string, out var level))
            {
                UpdateSegments(level); // 乐观刷新, EC 写入结果经 OnKeyboardBrightnessChanged 回流
                BrightnessSelected?.Invoke(level);
                e.Handled = true;
            }
        }

        private void MediaPrev_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            MediaCommand?.Invoke("prev");
        }

        private void MediaNext_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            MediaCommand?.Invoke("next");
        }

        private void MediaPlayPause_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // 乐观切换图标, 真实播放状态由 SMTC 事件经 UpdatePlayState 回同步
            PlayPauseGlyph.Text = PlayPauseGlyph.Text == "\uE769" ? "\uE768" : "\uE769";
            MediaCommand?.Invoke("playpause");
        }

        /// <summary>按档位刷新背光分段 (激活段强调色, 未激活段半透明)。</summary>
        private void UpdateSegments(byte level)
        {
            var fills = new[] { Seg0Fill, Seg1Fill, Seg2Fill, Seg3Fill };
            for (var i = 0; i < fills.Length; i++)
                fills[i].Background = i <= level ? new SolidColorBrush(_accentColor) : _faintBrush;
        }

        /// <summary>SMTC 事件回同步播放/暂停按钮图标 (同曲去重不重弹时保持按钮状态正确)。
        /// SMTC 事件可能来自线程池线程, 统一调度到 UI 线程。</summary>
        public void UpdatePlayState(bool playing)
        {
            if (CurrentKind != "media") return;
            Dispatcher.BeginInvoke(() =>
                PlayPauseGlyph.Text = playing ? "\uE769" : "\uE768");
        }

        /// <summary>性能模式遥测读取完成后原位补充副标题 (EC/NvAPI 读取在后台线程, 需调度)。</summary>
        public void UpdatePerfTelemetry(string subtitle)
        {
            if (CurrentKind != "perf") return;
            Dispatcher.BeginInvoke(() => SubtitleText.Text = subtitle);
        }

        /// <summary>等布局/展开动画稳定后测量标题: 仅媒体 OSD 且溢出时启用平移。
        /// delayMs=0 表示布局已稳定, 排一次 Loaded 优先级即可。</summary>
        private void ScheduleTitleMarquee(int delayMs)
        {
            _marqueeDelayTimer?.Stop();
            if (delayMs > 0)
            {
                _marqueeDelayTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(delayMs) };
                _marqueeDelayTimer.Tick += (_, _) =>
                {
                    _marqueeDelayTimer?.Stop();
                    UpdateTitleMarquee();
                };
                _marqueeDelayTimer.Start();
            }
            else
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, UpdateTitleMarquee);
            }
        }

        /// <summary>初始化时检测歌名是否超过显示区域: 超过则整体向前平移,
        /// 直到最后一个字符完整显示后停止 (FillBehavior.HoldEnd 钉在终点)。
        /// TitleText 在 Canvas 中不受宽度约束, ActualWidth 即自然全宽, 无测量偏差。</summary>
        private void UpdateTitleMarquee()
        {
            StopTitleMarquee();
            if (!IsVisible || CurrentKind != "media") return;

            // 胶囊宽度仍被入场/恢复动画驱动时可视区尚未定型 (展开中偏小, 末端 BackEase 过冲偏大):
            // 溢出判定会失真 — 短曲名被误判溢出而滚动, 长曲名平移目标偏短。
            // 驻留中切歌若撞上上一轮动画未结束 (SMTC 切歌会连发多条刷新), 在此等待重试,
            // 宽度回到满宽基准值 (动画 FillBehavior.Stop 后回落到 ApplyLayoutScale 设置的值) 再测量
            if (Math.Abs(Pill.Width - PillW * _uiScale) > 0.5)
            {
                ScheduleTitleMarquee(100);
                return;
            }

            // 已显示状态下切歌时, 新文本的布局可能尚未跑完, 强制同步完成,
            // 否则 ActualWidth 会是上一首的旧宽度 → 误判"未超宽"而放弃平移 (概率性不滚动)
            TitleClip.UpdateLayout();
            var viewport = TitleClip.ActualWidth;
            var textWidth = TitleText.ActualWidth;
            if (viewport <= 1 || textWidth <= 1)
            {
                ScheduleTitleMarquee(120); // 布局尚未产出有效尺寸 (极小概率): 稍后重试
                return;
            }

            var overflow = textWidth - viewport;
            if (overflow <= 2) return; // 未超过显示区域: 静止完整显示

            // 平移终点: 最后一个字符完整进入可视区; 12px 余量抵消 Display 模式逐字取整的累计变宽
            var target = -(overflow + 12);
            var scrollMs = Math.Max(1200, (int)((overflow + 12) / 30.0 * 1000));
            var anim = new DoubleAnimation(0, target, TimeSpan.FromMilliseconds(scrollMs))
            {
                FillBehavior = FillBehavior.HoldEnd, // 播完停在终点, 不回弹不循环
            };
            TitleTranslate.BeginAnimation(TranslateTransform.XProperty, anim);

            // 驻留覆盖: 平移完成 + 结尾停留可读, 之后按正常节奏退场 (悬停仍会挂起)
            _hideTimer.Stop();
            _hideTimer.Interval = TimeSpan.FromMilliseconds(scrollMs + 900 + Math.Max(800, _dwellMs * 0.5));
            _hideTimer.Start();
        }

        /// <summary>停止平移并复位到自然位置。</summary>
        private void StopTitleMarquee()
        {
            TitleTranslate.BeginAnimation(TranslateTransform.XProperty, null);
            TitleTranslate.X = 0;
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

        /// <summary>
        /// 加载 OsdItem 指定的 PNG 图标 (支持 pack URI 与磁盘路径), 失败返回 null 回退字形。
        /// 素材为黑色剪影 (部分导出时整体半透明), 读取后做透明度归一化 (最高不透明度拉满,
        /// 保留抗锯齿边缘); 渲染时由 OpacityMask 以强调色着色, 不直接显示黑色原图。
        /// </summary>
        private static ImageSource? TryLoadIconImage(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            try
            {
                var uri = path.StartsWith("pack:", StringComparison.OrdinalIgnoreCase)
                    ? new Uri(path, UriKind.Absolute)
                    : Path.IsPathRooted(path)
                        ? new Uri(path)
                        : new Uri(Path.Combine(AppContext.BaseDirectory, path));
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad; // 读完即释放文件句柄
                bmp.UriSource = uri;
                bmp.EndInit();

                // 透明度归一化: 转非预乘 Bgra32 后按最高 alpha 等比拉满 (maxAlpha 已达 255 则跳过)
                var converted = new FormatConvertedBitmap(bmp, PixelFormats.Bgra32, null, 0);
                var normalized = new WriteableBitmap(converted);
                var stride = normalized.PixelWidth * 4;
                var pixels = new byte[stride * normalized.PixelHeight];
                normalized.CopyPixels(pixels, stride, 0);
                byte maxAlpha = 0;
                for (var i = 3; i < pixels.Length; i += 4)
                    if (pixels[i] > maxAlpha)
                        maxAlpha = pixels[i];
                if (maxAlpha is > 8 and < 250)
                {
                    for (var i = 3; i < pixels.Length; i += 4)
                        pixels[i] = (byte)Math.Min(255, pixels[i] * 255 / maxAlpha);
                    normalized.WritePixels(
                        new Int32Rect(0, 0, normalized.PixelWidth, normalized.PixelHeight),
                        pixels, stride, 0);
                }

                normalized.Freeze(); // 跨线程安全, 且免去每帧命中测试开销
                return normalized;
            }
            catch (Exception ex)
            {
                Logger.Warn($"OSD 图标图片加载失败 ({path}): {ex.Message}");
                return null;
            }
        }

        private static Color Darken(Color c, double factor) =>
            Color.FromRgb((byte)(c.R * factor), (byte)(c.G * factor), (byte)(c.B * factor));
    }
}
