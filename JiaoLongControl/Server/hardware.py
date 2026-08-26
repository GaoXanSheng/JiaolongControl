# -*- coding: utf-8 -*-
"""硬件层: ECF2/PEC 单例、root 检查与 sysfs/procfs 读取助手.

依赖: ecf2 / portec; 被各功能控制器 (mifs/cpu_power/gpu/system_info/
autofan/smu) 与 bridge/CLI 使用.
"""
import os
import subprocess
import sys
import time

from .ecf2 import Ecf2
from .portec import PortEc

ECF2 = None
PEC = None

def hw_init():
    global ECF2, PEC
    if os.geteuid() != 0:
        print("需要 root: sudo 运行 (访问 /dev/mem 与 /dev/port)", file=sys.stderr)
        sys.exit(1)
    if ECF2 is None:
        ECF2 = Ecf2()
    if PEC is None:
        try:
            PEC = PortEc()
        except Exception:
            PEC = None

# ============================ sysfs helpers ============================

def readf(path, default=None):
    try:
        with open(path) as f: return f.read().strip()
    except Exception:
        return default

def writef(path, val):
    try:
        with open(path, "w") as f: f.write(str(val))
        return True
    except Exception:
        return False

_prev_stat = None
_prev_snap = None
def cpu_usage():
    global _prev_stat, _prev_snap
    def snap():
        vals = list(map(int, open("/proc/stat").readline().split()[1:]))
        idle = vals[3] + (vals[4] if len(vals) > 4 else 0)
        return idle, sum(vals)
    cur = snap()
    prev = _prev_snap
    if prev is None or cur[1] <= prev[1]:
        time.sleep(0.08)
        prev, cur = cur, snap()
    _prev_snap = cur
    dt = cur[1] - prev[1]
    return round((1 - (cur[0] - prev[0]) / dt) * 100, 1) if dt > 0 else 0.0

def cpu_freq_mhz():
    import glob
    fs = []
    for p in glob.glob("/sys/devices/system/cpu/cpu*/cpufreq/scaling_cur_freq"):
        v = readf(p)
        if v: fs.append(int(v))
    return round(sum(fs) / len(fs) / 1000) if fs else 0

def cpu_temp():
    import glob
    for h in glob.glob("/sys/class/hwmon/hwmon*"):
        if readf(f"{h}/name") == "k10temp":
            v = readf(f"{h}/temp1_input")
            if v: return int(v) / 1000.0
    return ECF2.rd(Ecf2.TSR6) if ECF2 else 0

_rapl_last = None
def rapl_power_watts():
    """AMD 平台 RAPL: /sys/class/powercap/intel-rapl:0/energy_uj 是累积能量,
    需两次采样差分算功率; 首次调用无历史, 返回 0.0."""
    global _rapl_last
    try:
        v = int(readf("/sys/class/powercap/intel-rapl:0/energy_uj", "0"))
    except Exception:
        return 0.0
    now = time.time()
    if _rapl_last is None:
        _rapl_last = (now, v); return 0.0
    dt, dv = now - _rapl_last[0], v - _rapl_last[1]
    _rapl_last = (now, v)
    if dt <= 0 or dv < 0: return 0.0
    w = dv / 1_000_000 / dt
    return round(w, 1) if 0 < w < 300 else 0.0

def nvidia_query(q):
    try:
        out = subprocess.run(["nvidia-smi", f"--query-gpu={q}", "--format=csv,noheader,nounits"],
                             capture_output=True, text=True, timeout=8)
        return out.stdout.strip() if out.returncode == 0 else None
    except Exception:
        return None

def _first(s):
    lines = (s or "").splitlines()
    return lines[0].strip() if lines else ""
