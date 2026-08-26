# -*- coding: utf-8 -*-
"""CLI 入口实现: argparse 子命令解析 + 分发 (由 linux-port/jlctl.py 薄入口调用).

硬件通路 (ground-truth 来自 ACPI SSDT4/DSDT 反编译 + 实测):
  A. ECF2 共享内存  (/dev/mem @ 0xFE800400, 256B): 性能模式/RGB键盘/Logo灯/
     FnLock/触控板锁/显卡模式/CPU温度/风扇转速镜像 —— 即 Windows 端 WMI
     MiInterface(MIFS) 的底层落点, 绕过 0xBAEAF000 mailbox 直写
  B. EC 端口 IO     (/dev/port @ 0x4E/0x4F): 风扇手动转速/模式 —— Blding64 协议
  C. amd_pstate/sysfs: CPU 频率墙/EPP/boost/governor
  D. nvidia-smi:     GPU 遥测/锁频/功耗墙 (root)
  E. k10temp/RAPL:   SMU 遥测 (替代 LibreHardwareMonitor)

需要 root 运行 (sudo ./jlctl.py ...)。
"""
import argparse
import json

from .autofan import _af, autofan_start, autofan_stop, autofan_state
from .common import ok
from .cpu_power import (CPU0, cpu_info, epp_get, epp_set, governor_set,
                        power_freqmax_reset, power_freqmax_set, turbo_get,
                        turbo_set)
from .gpu import _nvidia_action, gpu_powerlimit_range, gpu_stats
from .hardware import cpu_freq_mhz, cpu_temp, cpu_usage, hw_init, readf
from .mifs import (PERFMODE_NAMES, fan_auto, fan_set_rpm, fan_speed_get,
                   fnlock_get, fnlock_set, gpumode_get, gpumode_set,
                   kb_brightness_get, kb_brightness_set, kb_color_get,
                   kb_color_set, kb_mode_get, kb_mode_set, logo_get, logo_set,
                   perfmode_get, perfmode_set, tplock_get, tplock_set)
from .server import serve
from .smu import _SMU_LIMITS, smu_co_all, smu_set_limit, smu_telemetry

def main():
    ap = argparse.ArgumentParser(prog="jlctl", description="蛟龙16Pro 硬件控制中心 (Linux)")
    sub = ap.add_subparsers(dest="cmd")

    sub.add_parser("status")
    ps = sub.add_parser("serve"); ps.add_argument("--port", type=int, default=8800)

    ps = sub.add_parser("perfmode"); ps.add_argument("action", choices=["get", "performance", "quiet", "balance"])

    p = sub.add_parser("fan"); sp = p.add_subparsers(dest="fa", required=True)
    sp.add_parser("status"); sp.add_parser("auto"); sp.add_parser("autofan")
    ps = sp.add_parser("set"); ps.add_argument("rpm", type=int, help="目标转速 RPM (1500-5800, EC档位=RPM/100)")

    p = sub.add_parser("rgb"); sp = p.add_subparsers(dest="ra", required=True)
    ps = sp.add_parser("color"); ps.add_argument("r", type=int); ps.add_argument("g", type=int); ps.add_argument("b", type=int)
    ps = sp.add_parser("brightness"); ps.add_argument("lvl", type=int, choices=[0, 1, 2, 3])
    ps = sp.add_parser("mode"); ps.add_argument("m", type=int, choices=[0, 2])

    p = sub.add_parser("logo"); p.add_argument("state", choices=["on", "off"])
    p = sub.add_parser("fnlock"); p.add_argument("state", choices=["on", "off"])
    p = sub.add_parser("tplock"); p.add_argument("state", choices=["on", "off"])
    p = sub.add_parser("gpumode"); p.add_argument("action", choices=["get", "hybrid", "discrete"])

    p = sub.add_parser("cpu"); sp = p.add_subparsers(dest="ca", required=True)
    for nm in ("temp", "freq", "usage", "info"): sp.add_parser(nm)
    ps = sp.add_parser("freqmax"); ps.add_argument("mhz", nargs="?", type=int)
    ps = sp.add_parser("epp"); ps.add_argument("pref", nargs="?")
    ps = sp.add_parser("governor"); ps.add_argument("gov", nargs="?")
    ps = sp.add_parser("boost"); ps.add_argument("state", choices=["on", "off"])

    p = sub.add_parser("gpu"); sp = p.add_subparsers(dest="ga", required=True)
    sp.add_parser("stats"); sp.add_parser("resetclock"); sp.add_parser("powerlimitrange")
    ps = sp.add_parser("lockclock"); ps.add_argument("mhz", type=int)
    ps = sp.add_parser("powerlimit"); ps.add_argument("watts", type=int)

    p = sub.add_parser("smu"); sp = p.add_subparsers(dest="sa", required=True)
    sp.add_parser("telemetry")
    ps = sp.add_parser("co"); ps.add_argument("value", type=int)
    ps = sp.add_parser("limit"); ps.add_argument("kind", choices=list(_SMU_LIMITS.keys())); ps.add_argument("watts", type=float)

    args = ap.parse_args()
    if not args.cmd:
        ap.print_help(); return 0
    hw_init()

    def emit(res):
        print(json.dumps(res, ensure_ascii=False))
        return 0 if res["Success"] else 1

    c = args.cmd
    if c == "status":
        out = {
            "perfmode": PERFMODE_NAMES.get(perfmode_get()["Data"]["mode"]),
            "fan": fan_speed_get()["Data"],
            "cpu_temp": cpu_temp(),
            "cpu_freq_mhz": cpu_freq_mhz(),
            "cpu_usage": cpu_usage(),
            "rgb": {"mode": kb_mode_get()["Data"], **kb_color_get()["Data"],
                    "brightness": kb_brightness_get()["Data"]},
            "logo": logo_get()["Data"], "fnlock": fnlock_get()["Data"],
            "tplock": tplock_get()["Data"], "gpumode": gpumode_get()["Data"],
            "gpu": gpu_stats().get("Data") or {},
            "autofan": autofan_state()["Data"],
            "epp": epp_get()["Data"], "turbo": turbo_get()["Data"]["ac"],
        }
        print(json.dumps(out, ensure_ascii=False, indent=2)); return 0
    if c == "serve":
        serve(args.port); return 0
    if c == "perfmode":
        return emit(perfmode_get() if args.action == "get"
                    else perfmode_set({"performance": 0, "quiet": 1, "balance": 2}[args.action]))
    if c == "fan":
        if args.fa == "status": return emit(fan_speed_get())
        if args.fa == "auto": return emit(fan_auto())
        if args.fa == "autofan":
            return emit(autofan_stop() if _af["run"] else autofan_start())
        return emit(fan_set_rpm(args.rpm))
    if c == "rgb":
        if args.ra == "color": return emit(kb_color_set(args.r, args.g, args.b))
        if args.ra == "brightness": return emit(kb_brightness_set(args.lvl))
        return emit(kb_mode_set(args.m))
    if c == "logo": return emit(logo_set(args.state == "on"))
    if c == "fnlock": return emit(fnlock_set(args.state == "on"))
    if c == "tplock": return emit(tplock_set(args.state == "on"))
    if c == "gpumode":
        if args.action == "get": return emit(gpumode_get())
        return emit(gpumode_set({"hybrid": 0, "discrete": 1}[args.action]))
    if c == "cpu":
        if args.ca == "temp": return emit(ok(data=cpu_temp()))
        if args.ca == "freq": return emit(ok(data=cpu_freq_mhz()))
        if args.ca == "usage": return emit(ok(data=cpu_usage()))
        if args.ca == "info": return emit(cpu_info())
        if args.ca == "freqmax":
            return emit(power_freqmax_reset() if not args.mhz else power_freqmax_set(args.mhz))
        if args.ca == "epp": return emit(epp_get() if not args.pref else epp_set(args.pref))
        if args.ca == "governor":
            return emit(ok(data=readf(f"{CPU0}/scaling_governor")) if not args.gov else governor_set(args.gov))
        if args.ca == "boost": return emit(turbo_set(args.state == "on"))
    if c == "gpu":
        if args.ga == "stats": return emit(gpu_stats())
        if args.ga == "lockclock": return emit(_nvidia_action(["--lock-gpu-clocks=", str(args.mhz)]))
        if args.ga == "resetclock": return emit(_nvidia_action(["--lock-gpu-clocks=reset"]))
        if args.ga == "powerlimit": return emit(_nvidia_action(["--power-limit=", str(args.watts)]))
        if args.ga == "powerlimitrange": return emit(gpu_powerlimit_range())
    if c == "smu":
        if args.sa == "telemetry": return emit(smu_telemetry())
        if args.sa == "co": return emit(smu_co_all(args.value))
        if args.sa == "limit": return emit(smu_set_limit(args.watts, args.kind))
    return 0
