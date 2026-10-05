using JiaoLongControl.Server.Core.Controllers;

namespace RyzenSmuControllerTest.SelfTest;

/// <summary>
/// SmuFamilyResolver 判定表测试用例。
/// 每行 = (品牌串, CPUID Family, CPUID Model, 期望 family, 备注)；
/// Family/Model 为 null 表示 Identifier 缺失，走纯品牌串回退链。
/// </summary>
internal static class SmuFamilyTestCases
{
    public static readonly (string Brand, int? Family, int? Model, RyzenSmuFamily Expected, string Note)[] All =
    [
        ("AMD Ryzen 9 7940HX with Radeon Graphics", 25, 99, RyzenSmuFamily.AM5_V1, "CPUID 主信号 (Dragon Range)"),
        ("AMD Ryzen 9 7940HX with Radeon Graphics", null, null, RyzenSmuFamily.AM5_V1, "品牌串回退 (数字HX 规则)"),
        ("AMD Ryzen 9 7945HX with Radeon Graphics", 25, 99, RyzenSmuFamily.AM5_V1, "回归: 不变"),
        ("AMD Ryzen 9 7945HX with Radeon Graphics", null, null, RyzenSmuFamily.AM5_V1, "回归: 不变"),
        ("AMD Ryzen 7 7845HX with Radeon Graphics", null, null, RyzenSmuFamily.AM5_V1, "回归: 不变"),
        ("AMD Ryzen 7 7745HX with Radeon Graphics", null, null, RyzenSmuFamily.AM5_V1, "回归: 不变"),
        ("AMD Ryzen 9 8940HX with Radeon Graphics", 25, 99, RyzenSmuFamily.AM5_V1, "Dragon Range Refresh"),
        ("AMD Ryzen 9 8945HX with Radeon Graphics", null, null, RyzenSmuFamily.AM5_V1, "回归: 原靠默认兜底碰巧正确"),
        ("AMD Ryzen 9 7945HX3D with Radeon Graphics", null, null, RyzenSmuFamily.AM5_V1, "HX3D 后缀须命中数字HX"),
        // Phoenix / Hawk Point: 与 7940HX 共享 7940 子串但平台不同, 不得被误吞
        ("AMD Ryzen 9 7940HS with Radeon Graphics", 25, 117, RyzenSmuFamily.FP7_FP8, "回归: 不变 (Phoenix)"),
        ("AMD Ryzen 9 7940HS with Radeon Graphics", null, null, RyzenSmuFamily.FP7_FP8, "回退链 7940 规则"),
        ("AMD Ryzen 7 7840HS with Radeon Graphics", null, null, RyzenSmuFamily.FP7_FP8, "回归: 不变"),
        ("AMD Ryzen 7 8845HS with Radeon Graphics", 25, 123, RyzenSmuFamily.FP7_FP8, "Hawk Point"),
        ("AMD Ryzen 7 8840HS with Radeon Graphics", null, null, RyzenSmuFamily.FP7_FP8, "回归: 不变"),
        // Strix Point (Zen5, CPUID 未入表 → 品牌串回退, 与历史行为同级)
        ("AMD Ryzen AI 9 HX 370 with Radeon Graphics", null, null, RyzenSmuFamily.FP7_FP8_Strix, "HX 370 不得被数字HX 误吞"),
        ("AMD Ryzen AI 9 HX 375 with Radeon Graphics", null, null, RyzenSmuFamily.FP7_FP8_Strix, "AI 9 规则"),
        ("AMD Ryzen AI 9 365 with Radeon Graphics", null, null, RyzenSmuFamily.FP7_FP8_Strix, "回归: 不变"),
        ("AMD Ryzen AI 7 350 with Radeon Graphics", null, null, RyzenSmuFamily.FP7_FP8_Strix, "Krackan Point"),
        // Cezanne / Rembrandt / 老平台
        ("AMD Ryzen 9 5900HX with Radeon Graphics", 25, 80, RyzenSmuFamily.FP6, "Cezanne HX: 数字HX 不得先于 FP6 规则"),
        ("AMD Ryzen 9 5900HX with Radeon Graphics", null, null, RyzenSmuFamily.FP6, "回归: 不变"),
        ("AMD Ryzen 7 5800H with Radeon Graphics", 25, 80, RyzenSmuFamily.FP6, "Cezanne"),
        ("AMD Ryzen 7 4800H with Radeon Graphics", 23, 96, RyzenSmuFamily.FP6, "Renoir"),
        ("AMD Ryzen 7 6800H with Radeon Graphics", 25, 68, RyzenSmuFamily.FP7_FP8, "Rembrandt"),
        ("AMD Ryzen 7 7735HS with Radeon Graphics", null, null, RyzenSmuFamily.FP7_FP8, "回归: 不变"),
        ("AMD Ryzen 5 5500U with Radeon Graphics", 23, 104, RyzenSmuFamily.FP6, "Lucienne: 原漏判为默认 AM5_V1, 顺带修复"),
        ("AMD Ryzen 9 7950X 16-Core Processor", 25, 97, RyzenSmuFamily.AM5_V1, "桌面 Raphael"),
        ("Some Future CPU", null, null, RyzenSmuFamily.AM5_V1, "未知 → 默认兜底 (与历史行为一致)"),
    ];
}
