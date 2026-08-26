# -*- coding: utf-8 -*-
"""控制器: NVIDIA (nvidia-smi 封装) — GPU 遥测/锁频/功耗墙."""
import subprocess

from .common import ok, fail
from .hardware import nvidia_query, _first

def gpu_stats():
    q = ("name,driver_version,memory.total,utilization.gpu,utilization.memory,"
         "clocks.gr,clocks.mem,temperature.gpu,power.draw,power.limit")
    s = nvidia_query(q)
    if not s: return fail("nvidia-smi 不可用")
    v = [x.strip() for x in s.split(",")]
    return ok(data={"name": v[0], "driver": v[1], "mem_total": v[2],
                    "util_gpu": v[3], "util_mem": v[4], "clock_core": v[5],
                    "clock_mem": v[6], "temp": v[7], "power_draw": v[8],
                    "power_limit": v[9]})

def _nvidia_action(args):
    try:
        out = subprocess.run(["nvidia-smi"] + args, capture_output=True, text=True, timeout=15)
        lines = [l.strip() for l in (out.stdout or out.stderr).strip().splitlines() if l.strip()]
        msg = lines[-1] if lines else ""
        return ok(msg) if out.returncode == 0 else fail(msg)
    except Exception as e:
        return fail(str(e))

def gpu_powerlimit_range():
    s = nvidia_query("power.min_limit,power.max_limit")
    if not s: return fail("nvidia-smi 不可用")
    mn, mx = [x.strip() for x in _first(s).split(",")]
    return ok(data={"Min": float(mn), "Max": float(mx)})
