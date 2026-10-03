using System.Text.RegularExpressions;

namespace JiaoLongControl.Server.Core.Controllers;

/// <summary>判定所需的 CPU 身份信息（纯数据）。</summary>
/// <param name="BrandName">品牌串, 如 "AMD Ryzen 9 7940HX with Radeon Graphics"</param>
/// <param name="Family">CPUID Family (十进制, 如 25); 解析失败为 null</param>
/// <param name="Model">CPUID Model (十进制, 如 99); 解析失败为 null</param>
/// <param name="IdentifierRaw">原始 Identifier 串, 如 "AMD64 Family 25 Model 99 Stepping 1"</param>
public readonly record struct CpuIdentity(
    string BrandName,
    int? Family,
    int? Model,
    string IdentifierRaw);

/// <summary>
/// SMU 平台判定器: 将 CPU 身份映射到 RyzenSmuFamily（决定 SMU 邮箱地址与指令集）。
///
/// 主信号是 CPUID Family/Model —— 品牌串是营销名, 存在 "7940HX 与 7940HS 共享 7940 子串
/// 而平台完全不同" 的冲突（曾导致 7940HX 被误判为 Phoenix, 所有 SMU 写入报忙碌超时）;
/// CPUID 绑定硅片身份, 无此问题。品牌串启发式仅作为 CPUID 未收录时的回退, 保持历史行为。
///
/// 本类必须保持纯函数、零 IO/日志依赖: RyzenSmuControllerTest 以 &lt;Compile Include&gt;
/// 直接链接本文件编译, 不携带主项目专属依赖。
/// </summary>
public static class SmuFamilyResolver
{
    private static readonly Regex IdentifierRegex =
        new(@"Family\s+(\d+)\s+Model\s+(\d+)", RegexOptions.Compiled);

    /// <summary>
    /// "数字HX" 后缀 = Dragon Range / Fire Range 全系（7940HX/7945HX3D/9955HX…）。
    /// 数字必须紧邻 HX 之前, "Ryzen AI 9 HX 370" 这类 Strix 品牌（数字在 HX 之后）不会误中。
    /// </summary>
    private static readonly Regex MobileHxRegex = new(@"\d{4}HX", RegexOptions.Compiled);

    /// <summary>从品牌串与 Windows Identifier 串构造身份。两者任一缺失都允许, Resolve 会降级处理。</summary>
    public static CpuIdentity FromBrandAndIdentifier(string brand, string identifier)
    {
        identifier ??= "";
        int? family = null;
        int? model = null;

        Match match = IdentifierRegex.Match(identifier);
        if (match.Success)
        {
            family = int.Parse(match.Groups[1].Value);
            model = int.Parse(match.Groups[2].Value);
        }

        return new CpuIdentity(brand ?? "", family, model, identifier);
    }

    /// <summary>判定 SMU 平台。evidence 输出命中信号, 供日志留痕。</summary>
    public static RyzenSmuFamily Resolve(CpuIdentity cpu, out string evidence)
    {
        // 1) CPUID 精确表: 只收录高置信度型号 (Zen4 及更早)。Zen5 精确型号未核实,
        //    有意不入表, 走品牌串回退（与历史行为同级）, 真机确认后再补。
        RyzenSmuFamily? mapped = MapCpuId(cpu.Family, cpu.Model);
        if (mapped != null)
        {
            evidence = $"CPUID F{cpu.Family}/M{cpu.Model} ({cpu.IdentifierRaw})";
            return mapped.Value;
        }

        string brand = cpu.BrandName ?? "";

        // 2) 品牌串回退链（历史规则 + HX 后缀修正）。顺序不可随意调整:
        //    FP6 规则必须在 数字HX 规则之前（5900HX/5980HX 是 Cezanne HX, 不是 Dragon Range）;
        //    数字HX 规则必须在 7940 规则之前（否则 7940HX 再次被 7940 子串吞入 Phoenix）。
        if (brand.Contains("HX 370") || brand.Contains("AI 9") || brand.Contains("AI 7") ||
            brand.Contains("365") || brand.Contains("370") || brand.Contains("Strix"))
        {
            evidence = "brand Strix 规则";
            return RyzenSmuFamily.FP7_FP8_Strix;
        }

        if (brand.Contains("5800") || brand.Contains("5900") || brand.Contains("5600") ||
            brand.Contains("4800") || brand.Contains("4600"))
        {
            evidence = "brand FP6 规则";
            return RyzenSmuFamily.FP6;
        }

        if (MobileHxRegex.IsMatch(brand))
        {
            evidence = "brand 数字HX 规则";
            return RyzenSmuFamily.AM5_V1;
        }

        if (brand.Contains("7735") || brand.Contains("6800") || brand.Contains("6900") ||
            brand.Contains("7840") || brand.Contains("7940") || brand.Contains("8840") ||
            brand.Contains("8845"))
        {
            evidence = "brand FP7/FP8 规则";
            return RyzenSmuFamily.FP7_FP8;
        }

        evidence = "无匹配, 默认 AM5_V1";
        return RyzenSmuFamily.AM5_V1;
    }

    /// <summary>CPUID (Family, Model) → SMU 平台。未收录返回 null（走品牌串回退）。</summary>
    private static RyzenSmuFamily? MapCpuId(int? family, int? model)
    {
        if (family == null || model == null)
            return null;

        return (family.Value, model.Value) switch
        {
            // Family 0x17 (23) — Zen/Zen+
            (0x17, 0x60) => RyzenSmuFamily.FP6,     // Renoir      4600H/4800H
            (0x17, 0x68) => RyzenSmuFamily.FP6,     // Lucienne    5300U/5500U (历史上漏判为默认值)
            // Family 0x19 (25) — Zen3/Zen4
            (0x19, 0x44) => RyzenSmuFamily.FP7_FP8, // Rembrandt   6600H/6800H/6900HX/6980HX/7735HS
            (0x19, 0x50) => RyzenSmuFamily.FP6,     // Cezanne     5600H/5800H/5900HX/5980HX
            (0x19, 0x61) => RyzenSmuFamily.AM5_V1,  // Raphael     桌面 AM5
            (0x19, 0x63) => RyzenSmuFamily.AM5_V1,  // Dragon Range 7745HX/7845HX/7940HX/7945HX/8940HX/8945HX
            (0x19, 0x75) => RyzenSmuFamily.FP7_FP8, // Phoenix     7840HS/7940HS
            (0x19, 0x78) => RyzenSmuFamily.FP7_FP8, // Phoenix 2   8440U/8540U
            (0x19, 0x7B) => RyzenSmuFamily.FP7_FP8, // Hawk Point  8840HS/8845HS/8945HS
            _ => null
        };
    }
}
