# -*- coding: utf-8 -*-
"""AutoFan — 后台线程自动风扇曲线, 以及 serve 启动时的开机配置应用 (apply_boot_config).

注意: mifs.fan_set/fan_auto 通过函数内延迟导入调用本模块的 autofan_stop,
避免 mifs ↔ autofan 顶层循环依赖.
"""
import threading
import time

from .common import ok
from .cpu_power import power_freqmax_set
from .ecf2 import Ecf2
from .hardware import ECF2, PEC
from .mifs import maxfanswitch_set, perfmode_set
from .smu import smu_co_all
from .system_info import config_get

_af = {"run": False, "last": None, "merge": True,
       "curve": [(45, 20), (58, 32), (66, 48), (74, 68), (80, 88), (86, 100)],
       "gpu_curve": [(45, 20), (58, 32), (66, 48), (74, 68), (80, 88), (86, 100)]}

def _af_loop():
    while _af["run"]:
        try:
            t = ECF2.rd(Ecf2.TSR6)
            def interp(pts):
                for tc, r in pts:
                    if t <= tc: return r
                return pts[-1][1] if pts else 3000
            rpm_cpu = interp(_af["curve"])
            rpm_gpu = interp(_af["gpu_curve"]) if not _af.get("merge") else rpm_cpu
            if PEC and PEC.alive():
                lvl_c = max(0, min(68, int(round(rpm_cpu / 100))))   # EC 档位 = RPM/100
                lvl_g = max(0, min(68, int(round(rpm_gpu / 100))))
                maxfanswitch_set(False)                        # 关 ACPI 策略, EC 手动优先
                PEC.write(0xC83C, lvl_c)
                PEC.write(0xC83D, lvl_g)
                PEC.write(0xB20, 0x0A)     # 绝对值手动位 (禁读改写)
            _af["last"] = (t, rpm_cpu)
        except Exception:
            pass
        time.sleep(2.0)

def _load_fan_curve_from_config():
    try:
        fan = config_get()["Data"].get("Fan", {})
        def parse(key):
            pts = [(int(p["temp"]), int(p["speed"])) for p in fan.get(key, [])
                   if isinstance(p, dict) and p.get("temp") and p.get("speed")]
            return sorted(pts) if len(pts) >= 2 else None
        c = parse("CpuFanCurve")
        if c: _af["curve"] = c
        g = parse("GpuFanCurve")
        if g: _af["gpu_curve"] = g
        _af["merge"] = bool(fan.get("FanCurveMerge", True))
    except Exception:
        pass

def autofan_start():
    if not _af["run"]:
        _load_fan_curve_from_config()
        _af["run"] = True
        threading.Thread(target=_af_loop, daemon=True).start()
    return ok("自动风扇已启动")

def autofan_stop():
    was = _af["run"]; _af["run"] = False
    return ok("自动风扇已停止" if was else "自动风扇未在运行")

def autofan_state(): return ok(data=_af["run"])

# ---- Boot 应用: 与 C# 版 App.BootXXX 语义一致, serve 启动时执行一次 ----
def apply_boot_config():
    """把持久化配置中标记为"开机自动应用"的项目真正写到硬件."""
    results = []
    try:
        cfg = config_get()["Data"]
    except Exception as e:
        return {"applied": [], "error": str(e)}
    app = cfg.get("App", {}) or {}
    smu = cfg.get("Smu", {}) or {}
    # 1) RyzenSMU 全核降压自动应用
    if app.get("BootSetRyzenSumCurveOptimizerAll"):
        co = smu.get("CurveOptimizerAll")
        if co:
            r = smu_co_all(int(co))
            results.append(("curve_optimizer", r.get("Success"), r.get("Message", "")))
    # 2) 高级风扇控制 (曲线自动调速)
    if app.get("BootAdvancedFanControlSystem"):
        r = autofan_start()
        results.append(("auto_fan", True, r["Message"]))
    # 3) CPU 参数自动应用 (性能模式 / 最大频率)
    if app.get("BootCPUAutoStart"):
        cpu = cfg.get("Cpu", {}) or {}
        mode = {"PerformanceMode": 0, "QuietMode": 1, "BalanceMode": 2}.get(cpu.get("CpuProfile"), None)
        if mode is not None:
            r = perfmode_set(mode)
            results.append(("perfmode", r.get("Success"), r.get("Message", "")))
        fm = cpu.get("CpuMaxFrequency")
        if fm:
            r = power_freqmax_set(int(fm))
            results.append(("freq_max", r.get("Success"), r.get("Message", "")))
    return {"applied": [r for r in results], "count": len(results)}
