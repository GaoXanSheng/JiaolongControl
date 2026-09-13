namespace JiaoLongControl.Server.Core.Models
{
    /// <summary>单次 OSD 显示的内容模型（图标/强调色/两行文本/进度条/交互类型）。</summary>
    public class OsdItem
    {
        /// <summary>OSD 类型: volume / keyboard / media / lock / perf / fnlock / touchpad；
        /// volume、keyboard、media 为交互型 (音量滑条 / 背光分段 / 媒体控制按钮)。</summary>
        public string Kind { get; init; } = "";

        /// <summary>Segoe MDL2 Assets 图标字形。</summary>
        public string IconGlyph { get; init; } = "";

        /// <summary>
        /// 图标图片 (pack URI, 如 pack://application:,,,/Assets/OSD/Lock.png)。
        /// 加载成功时替代 IconGlyph 字形显示, 加载失败回退为字形。
        /// </summary>
        public string? IconImage { get; init; }

        /// <summary>强调色 (十六进制，如 #3B82F6)，用于图标、光晕与进度条。</summary>
        public string AccentHex { get; init; } = "#3B82F6";

        /// <summary>主标题。</summary>
        public string Title { get; init; } = "";

        /// <summary>副标题（小字说明）。</summary>
        public string Subtitle { get; init; } = "";

        /// <summary>进度条比例 0~1；null 表示不显示进度条。</summary>
        public double? BarValue { get; init; }

        /// <summary>键盘背光档位 0~3 (Kind=keyboard 时用于渲染可点击分段)。</summary>
        public byte BrightnessLevel { get; init; }

        /// <summary>媒体播放状态 (Kind=media 时决定播放/暂停按钮图标)。</summary>
        public bool IsPlaying { get; init; } = true;

        /// <summary>静音状态 (Kind=volume 时决定静音按钮图标)。</summary>
        public bool IsMuted { get; init; }
    }
}
