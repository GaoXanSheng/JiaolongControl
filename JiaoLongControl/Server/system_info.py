# -*- coding: utf-8 -*-
"""SystemInfo / Config / AutoStart — 系统概览、持久化配置 (~/.config/jiaolongcontrol) 与 systemd 自启."""
import json
import os
import re
import subprocess

from .common import ok, fail
from .cpu_power import cpu_info
from .hardware import readf, nvidia_query, _first

def _mem_dmi():
    """dmidecode -t 17: 真实物理容量(32G而非MemTotal的30.x)与内存条详情"""
    try:
        out = subprocess.run(["dmidecode", "-t", "17"], capture_output=True,
                             text=True, timeout=6).stdout
        total_mb = 0; sticks = []
        for dev in out.split("Memory Device")[1:]:
            ms = re.search(r"^\s*Size:\s*(\d+)\s*(GB|MB)", dev, re.M)
            if not ms: continue
            mb = int(ms.group(1)) * (1024 if ms.group(2) == "GB" else 1)
            if mb == 0: continue
            total_mb += mb
            pn  = re.search(r"^\s*Part Number:\s*(\S.*)", dev, re.M)
            spd = re.search(r"^\s*Speed:\s*(\d+)\s*MT/s", dev, re.M)
            typ = re.search(r"^\s*Type:\s*(DDR\S*)", dev, re.M)
            man = re.search(r"^\s*Manufacturer:\s*(\S.*)", dev, re.M)
            s = (pn.group(1).strip()[:20] if pn else f"{mb//1024}GB")
            if typ: s = f"{typ.group(1)} {s}"
            if spd: s += f" @{spd.group(1)}MT/s"
            sticks.append(s.strip())
        if total_mb:
            per = total_mb // max(len(sticks), 1) // 1024
            base = f"{total_mb // 1024}GB ({len(sticks)}x{per}GB)" if sticks else f"{total_mb // 1024}GB"
            return base + (" | " + "; ".join(sticks[:2]) if sticks else "")
    except Exception:
        pass
    return None

def system_overview():
    m = re.search(r"MemTotal:\s+(\d+)", readf("/proc/meminfo", ""))
    memtot = _mem_dmi()
    if not memtot and m:
        memtot = f"{int(m.group(1)) // 1024 // 1024}GB"
    kernel = readf("/proc/sys/kernel/osrelease", "-")
    return ok(data={"CpuName": cpu_info()["Data"]["Name"],
                    "GpuName": _first(nvidia_query("name")) or "-",
                    "OsVersion": f"Linux {kernel}", "MemoryInfo": memtot or "-"})

CONF_DIR = os.path.expanduser("~/.config/jiaolongcontrol")
CONF_FILE = os.path.join(CONF_DIR, "config.json")
def _default_config():
    """与前端 types/config.ts (JiaoLongConfigType) 完全对齐的默认结构"""
    prof = dict(CpuLongPower=45, CpuShortPower=60, CpuTempWall=85, CpuMaxFrequency=5150, CpuTurbo=True)
    return {
        "Version": "linux-1.0",
        "App": {"BootMinimized": False, "BootAdvancedFanControlSystem": False,
                "BootAdvancedCPUSystem": False, "BootAdvancedGPUSystem": False,
                "BootSetRyzenSumCurveOptimizerAll": False},
        "Cpu": {"CpuProfile": "Balance", "Default": dict(prof),
                "Performance": dict(CpuLongPower=54, CpuShortPower=75, CpuTempWall=90, CpuMaxFrequency=5150, CpuTurbo=True),
                "Saving": dict(CpuLongPower=30, CpuShortPower=45, CpuTempWall=75, CpuMaxFrequency=3200, CpuTurbo=True),
                "Custom": dict(prof)},
        "Gpu": {"GpuClock": 0, "MemoryClock": 0, "PowerLimit": 0},
        "Fan": {"FanCurveMerge": True, "ManualFanSpeed": 0,
                # 温度域必须落在前端编辑器坐标内: CPU [60,100], GPU [60,87]
                "CpuFanCurve": [{"temp": 60, "speed": 1800}, {"temp": 70, "speed": 2400},
                                {"temp": 80, "speed": 3600}, {"temp": 90, "speed": 5200},
                                {"temp": 100, "speed": 6800}],
                "GpuFanCurve": [{"temp": 60, "speed": 1800}, {"temp": 70, "speed": 2600},
                                {"temp": 80, "speed": 4000}, {"temp": 87, "speed": 6800}]},
        "Smu": {"StapmLimit": 45, "StapmTime": 0, "FastLimit": 60, "SlowLimit": 54,
                "SlowTime": 0, "PptLimitRsmu": 75, "VrmCurrentMp1": 0, "VrmCurrentRsmu": 0,
                "TdcLimitMp1": 0, "TdcLimitRsmu": 0, "EdcLimitMp1": 0, "EdcLimitRsmu": 0,
                "TempLimitMp1": 0, "TempLimitRsmu": 0, "PboScalar": 0, "OcClk": 0,
                "OcVolt": 0, "CurveOptimizerAll": 0},
    }

def _deep_merge(base, extra):
    for k, v in (extra or {}).items():
        if isinstance(v, dict) and isinstance(base.get(k), dict):
            _deep_merge(base[k], v)
        else:
            base[k] = v
    return base

def config_get():
    try:
        saved = json.load(open(CONF_FILE))
        if not isinstance(saved, dict):
            saved = {}
    except Exception:
        saved = {}
    return ok(data=_deep_merge(_default_config(), saved))
def config_set(cfg):
    os.makedirs(CONF_DIR, exist_ok=True)
    json.dump(cfg, open(CONF_FILE, "w"), ensure_ascii=False, indent=2)
    return ok("保存成功")

UNIT = "jiaolongctl.service"
def autostart_enabled():
    out = subprocess.run(["systemctl", "--user", "is-enabled", UNIT], capture_output=True, text=True)
    return ok(data=out.returncode == 0)
def autostart_set(enable):
    out = subprocess.run(["systemctl", "--user", "enable" if enable else "disable", UNIT],
                         capture_output=True, text=True)
    return ok(out.stdout.strip()) if out.returncode == 0 else fail(out.stderr.strip())
