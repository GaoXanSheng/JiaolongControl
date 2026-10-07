using JiaoLongControl.Server.Core.Controllers;
using RyzenSmuControllerTest.Infrastructure;

namespace RyzenSmuControllerTest.SelfTest;

internal static class SmuFamilySelfTest
{
    public static int Run()
    {
        var cases = SmuFamilyTestCases.All;

        ConsoleLog.Log("");
        ConsoleLog.Log(">>> SmuFamilyResolver 自检 (无需硬件) ...");
        int failed = 0;
        foreach (var (brand, family, model, expected, note) in cases)
        {
            string identifier = family == null || model == null
                ? ""
                : $"AMD64 Family {family} Model {model} Stepping 1";
            CpuIdentity cpu = SmuFamilyResolver.FromBrandAndIdentifier(brand, identifier);
            RyzenSmuFamily actual = SmuFamilyResolver.Resolve(cpu, out string evidence);

            bool ok = actual == expected;
            if (!ok) failed++;
            string mark = ok ? "PASS" : "FAIL";
            ConsoleLog.Log($"  [{mark}] {brand,-46} → {actual} (期望 {expected})  [{note}]  信号: {evidence}");
        }

        ConsoleLog.Log("");
        ConsoleLog.Log($"  自检结果: {cases.Length - failed}/{cases.Length} 通过");
        if (failed > 0)
            ConsoleLog.Error($"  自检失败 {failed} 例");
        ConsoleLog.Log("");
        return failed == 0 ? 0 : 1;
    }
}
