namespace JiaoLongControl.Server.Core.Utils;

using Microsoft.Win32;
using System.Windows.Media;

/// <summary>
/// WPF 侧界面主题取色。配置三态(light/dark/system)解析为是否浅色,
/// 窗口底色与前端 CSS 变量 --color-bg-grad-bottom 保持一致,
/// 避免启动或 WebView 重建时与页面底色不一致造成闪色。
/// </summary>
public static class UiTheme
{
    private static readonly Color DarkBackground = Color.FromRgb(0x07, 0x0B, 0x1C);
    private static readonly Color LightBackground = Color.FromRgb(0xE9, 0xED, 0xF5);
    private static readonly Color DarkText = Color.FromRgb(0xFF, 0xFF, 0xFF);
    private static readonly Color LightText = Color.FromRgb(0x1A, 0x1B, 0x26);

    /// <summary>解析配置主题为是否浅色; system 跟随 Windows 个人化深浅色(默认深色)</summary>
    public static bool IsLight(string? theme)
    {
        return theme switch
        {
            "light" => true,
            "dark" => false,
            "system" => IsSystemLight(),
            _ => false
        };
    }

    private static bool IsSystemLight()
    {
        try
        {
            // AppsUseLightTheme: 1=浅色 0=深色; 键不存在时按 Windows 默认(浅色)处理
            var value = Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "AppsUseLightTheme", 1);
            return value is int i && i == 1;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>窗口/WebView 底色</summary>
    public static Color Background(bool isLight) => isLight ? LightBackground : DarkBackground;

    /// <summary>加载/错误遮罩上的文字颜色</summary>
    public static Color OverlayText(bool isLight) => isLight ? LightText : DarkText;
}
