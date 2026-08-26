# -*- coding: utf-8 -*-
"""控制器: CPU / Power (cpufreq/sysfs) — 频率墙/Turbo/EPP/governor."""
import re
import subprocess

from .common import ok, fail
from .hardware import readf, writef

def cpu_info():
    txt = readf("/proc/cpuinfo", "")
    name = ""
    phys = set(); threads = 0
    for blk in txt.split("\n\n"):
        d = {}
        for line in blk.splitlines():
            if ":" in line:
                k, _, v = line.partition(":")
                d[k.strip()] = v.strip()
        if d.get("model name") and not name: name = d["model name"]
        if "physical id" in d and "core id" in d: phys.add((d["physical id"], d["core id"]))
        threads += 1
    m = re.search(r"\((\d+) cores?\)", name) or re.search(r"(\d+)-Core", name)
    cores = int(m.group(1)) if m else (len(phys) or threads // 2)
    bm = re.search(r"@ (\d+\.\d+)GHz", name)
    base = int(float(bm.group(1)) * 1000) if bm else 0
    if not base:  # 7745HX 等型号串不含 @ 频率, 从 DMI "Current Speed" 取
        try:
            out = subprocess.run(["dmidecode", "-t", "4"], capture_output=True,
                                 text=True, timeout=5).stdout
            m2 = re.search(r"Current Speed:\s*(\d+)\s*MHz", out)
            if m2: base = int(m2.group(1))
        except Exception:
            pass
    return ok(data={"Name": name, "Cores": cores, "Threads": threads, "BaseFreqMhz": base})

CPU_MAX_MHZ = 5151
def power_freqmax_get():
    v = int(readf("/sys/devices/system/cpu/cpu0/cpufreq/scaling_max_freq", "0"))
    return ok(data={"ac": v // 1000, "dc": v // 1000})

def power_freqmax_set(mhz):
    import glob
    n = sum(1 for p in glob.glob("/sys/devices/system/cpu/cpu*/cpufreq/scaling_max_freq")
            if writef(p, int(mhz) * 1000))
    return ok("设置成功", {"cpus": n}) if n else fail("写入失败(需要root)")

def power_freqmax_reset():
    import glob
    n = 0
    for p in glob.glob("/sys/devices/system/cpu/cpu*/cpufreq/cpuinfo_max_freq"):
        mx = readf(p)
        if mx and writef(p.replace("cpuinfo_max_freq", "scaling_max_freq"), mx): n += 1
    return ok("已复位", {"cpus": n}) if n else fail("复位失败")

BOOST = "/sys/devices/system/cpu/cpufreq/boost"
def turbo_get():
    v = readf(BOOST, "1")
    return ok(data={"ac": v == "1", "dc": v == "1"})
def turbo_set(on):
    return ok("设置成功") if writef(BOOST, 1 if on else 0) else fail("写入失败(需要root)")

CPU0 = "/sys/devices/system/cpu/cpu0/cpufreq"
def epp_get():
    return ok(data=readf(f"{CPU0}/energy_performance_preference", "?"))
def epp_set(pref):
    avail = (readf(f"{CPU0}/energy_performance_available_preferences", "") or "").split()
    if pref not in avail: return fail(f"可用: {avail}")
    import glob
    n = sum(1 for p in glob.glob("/sys/devices/system/cpu/cpu*/cpufreq/energy_performance_preference")
            if writef(p, pref))
    return ok("设置成功") if n else fail("写入失败")
def governor_set(g):
    avail = (readf(f"{CPU0}/scaling_available_governors", "") or "").split()
    if g not in avail: return fail(f"可用: {avail}")
    import glob
    n = sum(1 for p in glob.glob("/sys/devices/system/cpu/cpu*/cpufreq/scaling_governor")
            if writef(p, g))
    return ok("设置成功") if n else fail("写入失败")
