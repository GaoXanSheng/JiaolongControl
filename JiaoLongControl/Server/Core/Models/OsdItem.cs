namespace JiaoLongControl.Server.Core.Models
{
    /// <summary>单次 OSD 显示的内容模型（图标/强调色/两行文本/进度条）。</summary>
    public class OsdItem
    {
        /// <summary>Segoe MDL2 Assets 图标字形。</summary>
        public string IconGlyph { get; init; } = "";

        /// <summary>强调色 (十六进制，如 #3B82F6)，用于图标、光晕与进度条。</summary>
        public string AccentHex { get; init; } = "#3B82F6";

        /// <summary>主标题。</summary>
        public string Title { get; init; } = "";

        /// <summary>副标题（小字说明）。</summary>
        public string Subtitle { get; init; } = "";

        /// <summary>进度条比例 0~1；null 表示不显示进度条。</summary>
        public double? BarValue { get; init; }
    }
}
