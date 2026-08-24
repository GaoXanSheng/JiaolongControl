#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
JiaolongControl Linux Port — CLI + WebUI 后端
机器: MECHREVO Jiaolong MRID6 (Ryzen 7 7745HX + RTX 4060 Laptop)

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
import os
import re
import subprocess
import struct
import sys
import threading
import time

# ============================ CommandResult ============================

def ok(msg="获取成功", data=None):
    return {"Success": True, "Message": msg, **({"Data": data} if data is not None else {})}

def fail(msg="设置失败", data=None):
    return {"Success": False, "Message": msg, **({"Data": data} if data is not None else {})}

# ============================ A. ECF2 (/dev/mem) ============================

class Ecf2:
    """EC 共享内存窗口, 与 ACPI OperationRegion(ECF2, SystemMemory, 0xFE800400, 0xFF)
    完全一致. 字段偏移由 DSDT H_EC Field 定义逐位解析得出."""
    BASE = 0xFE800400

    # byte offsets
    TSR6   = 0x1C   # CPU 温度 (°C)
    ECWR   = 0x60   # bit7 = AC 接入
    KBNL   = 0x9A   # 键盘亮度 0..3
    F1HI   = 0x9B   # 风扇1转速高位
    F1LO   = 0x9C
    F2HI   = 0x9D
    F2LO   = 0x9E
    TFLG   = 0xD0   # 性能模式触发标志: 先写 0x55 再写 ITSM (ACPI 原序)
    GFLG   = 0xD1
    GPMD   = 0xD2   # 显卡模式
    ITSM   = 0xE4   # 性能模式 0=Performance 1=Quiet 2=Balance
    LEDM   = 0xE8   # 键盘灯模式 0=off 2=RGB固定
    RGBR   = 0xE9
    RGBG   = 0xEA
    RGBB   = 0xEB
    # bit fields: (byte_off, bit)
    FNHK_B = (32, 3)    # FnLock
    TOCP_B = (37, 0)    # 触控板
    FWDE_B = (226, 0)   # Logo 灯 (Ambientlight)
    FAAP_B = (226, 1)   # ACPI 风扇策略开关 (MaxFanSpeedSwitch)
    CMEN_B = (240, 0)   # CPUPower 自定义模式开关
    CSPL   = 0xF5       # 当前 SPL 镜像
    FPPT   = 0xF6       # 当前 SPPT 镜像
    CTCL   = 0xF7       # 当前温度墙镜像

    def __init__(self):
        self.f = open("/dev/mem", "r+b", buffering=0)

    def rd(self, off):
        self.f.seek(self.BASE + off)
        return self.f.read(1)[0]

    def wr(self, off, val):
        self.f.seek(self.BASE + off)
        self.f.write(bytes([val & 0xFF]))

    def rdbit(self, ob):
        return (self.rd(ob[0]) >> ob[1]) & 1

    def wrbit(self, ob, val):
        b = self.rd(ob[0])
        b = (b | (1 << ob[1])) if val else (b & ~(1 << ob[1]))
        self.wr(ob[0], b)

# ============================ B. EC 端口 IO ============================

class PortEc:
    """/dev/port 访问 EC RAM (Blding64 协议).
    读时序末尾必须 0x4E←0x2F 切回数据模式后再 inb(0x4F), 否则读到命令回显."""
    A = 0x4E
    D = 0x4F

    def __init__(self):
        self.f = open("/dev/port", "r+b", buffering=0)

    def _out(self, port, val):
        self.f.seek(port); self.f.write(bytes([val & 0xFF]))

    def _in(self, port):
        self.f.seek(port); return self.f.read(1)[0]

    def read(self, idx):
        hi, lo = (idx >> 8) & 0xFF, idx & 0xFF
        o = self._out
        o(self.A, 0x2E); o(self.D, 0x11); o(self.A, 0x2F); o(self.D, hi)
        o(self.A, 0x2E); o(self.D, 0x10); o(self.A, 0x2F); o(self.D, lo)
        o(self.A, 0x2E); o(self.D, 0x12); o(self.A, 0x2F)
        return self._in(self.D)

    def write(self, idx, val):
        hi, lo = (idx >> 8) & 0xFF, idx & 0xFF
        o = self._out
        o(self.A, 0x2E); o(self.D, 0x11); o(self.A, 0x2F); o(self.D, hi)
        o(self.A, 0x2E); o(self.D, 0x10); o(self.A, 0x2F); o(self.D, lo)
        o(self.A, 0x2E); o(self.D, 0x12); o(self.A, 0x2F)
        o(self.D, val & 0xFF)

    def alive(self):
        try: return self.read(0x2000) == 0x55
        except Exception: return False

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

# ============================ 控制器: MIFS 等价 (ECF2) ============================

PERFMODE_NAMES = {0: "Performance", 1: "Quiet", 2: "Balance"}

def perfmode_get():
    return ok(data={"mode": ECF2.rd(Ecf2.ITSM), "names": PERFMODE_NAMES})

def perfmode_set(mode):
    mode = int(mode)
    if mode not in (0, 1, 2):
        return fail(f"无效模式 {mode} (0=Performance 1=Quiet 2=Balance)")
    ECF2.wr(Ecf2.TFLG, 0x55)          # ACPI 原序: 先触发标志再写模式
    ECF2.wr(Ecf2.ITSM, mode)
    return ok("设置成功", {"mode": mode})

def fan_speed_get():
    f1 = (ECF2.rd(Ecf2.F1HI) << 8) | ECF2.rd(Ecf2.F1LO)
    f2 = (ECF2.rd(Ecf2.F2HI) << 8) | ECF2.rd(Ecf2.F2LO)
    def norm(v):                       # 兼容 直接RPM / 百转单位 两种编码
        return v * 100 if 0 < v < 100 else v
    return ok(data={"CPUFanSpeed": norm(f1), "GPUFanSpeed": norm(f2),
                    "raw": {"cpu": f1, "gpu": f2}})

def maxfanswitch_get():
    return ok(data=bool(ECF2.rdbit(Ecf2.FAAP_B)))

def maxfanswitch_set(v):
    ECF2.wrbit(Ecf2.FAAP_B, 1 if v else 0)
    return ok("设置成功")

def fan_set(level_from_frontend):
    """手动定速. 注意: WebUI 链路 bridge.ts 已做 toByte(fanSpeed/100),
    所以此处收到的是 EC 档位 (RPM/100), 不可再次除以 100!
    CLI 的 fan set <RPM> 入口请先用 fan_set_rpm(). CPU+GPU 同步."""
    lvl = max(1, min(68, int(round(int(level_from_frontend)))))   # 0 档会停转, 最小钳到 1
    autofan_stop()
    if PEC is None or not PEC.alive():
        return fail("EC 端口不可用")
    maxfanswitch_set(False)                 # 与 C# 同款: 先关 ACPI 风扇策略
    PEC.write(0xC83C, lvl)
    PEC.write(0xC83D, lvl)
    PEC.write(0xB20, 0x0A)                  # 绝对值手动位(CPU+GPU), 禁读改写(B20是动态状态寄存器)
    return ok("设置成功", {"level": lvl, "rpm": lvl * 100})

def fan_set_rpm(rpm):
    """CLI 入口: 输入目标转速 RPM, 内部换算为档位."""
    return fan_set(int(round(int(rpm) / 100)))

def fan_auto():
    if PEC is None:
        return fail("EC 端口不可用")
    autofan_stop()
    PEC.write(0xC83C, 0); PEC.write(0xC83D, 0)
    PEC.write(0xB20, 0x00)                  # 清手动位 → EC 固件自动调速
    return ok("已恢复自动调速")

def kb_mode_get():  return ok(data=int(ECF2.rd(Ecf2.LEDM)))
def kb_mode_set(m):
    m = int(m)
    if m not in (0, 2): return fail("无效模式 (0=Off 2=RGB)")
    ECF2.wr(Ecf2.LEDM, m); return ok("设置成功")

def kb_color_get():
    return ok(data={"red": ECF2.rd(Ecf2.RGBR), "green": ECF2.rd(Ecf2.RGBG),
                    "blue": ECF2.rd(Ecf2.RGBB)})

def kb_color_set(r, g, b):
    r, g, b = int(r), int(g), int(b)
    if not all(0 <= v <= 255 for v in (r, g, b)): return fail("颜色须在 0-255")
    if ECF2.rd(Ecf2.LEDM) != 2: ECF2.wr(Ecf2.LEDM, 2)
    ECF2.wr(Ecf2.RGBR, r); ECF2.wr(Ecf2.RGBG, g); ECF2.wr(Ecf2.RGBB, b)
    return ok("设置成功")

def kb_brightness_get(): return ok(data=int(ECF2.rd(Ecf2.KBNL)))
def kb_brightness_set(v):
    v = int(v)
    if not 0 <= v <= 3: return fail("亮度级别 0-3")
    ECF2.wr(Ecf2.KBNL, v); return ok("设置成功")

def logo_get():  return ok(data=bool(ECF2.rdbit(Ecf2.FWDE_B)))
def logo_set(on): ECF2.wrbit(Ecf2.FWDE_B, bool(on)); return ok("设置成功")

def fnlock_get():  return ok(data=bool(ECF2.rdbit(Ecf2.FNHK_B)))
def fnlock_set(on): ECF2.wrbit(Ecf2.FNHK_B, bool(on)); return ok("设置成功")

def tplock_get():  return ok(data=bool(ECF2.rdbit(Ecf2.TOCP_B)))
def tplock_set(on): ECF2.wrbit(Ecf2.TOCP_B, bool(on)); return ok("设置成功")

def gpumode_get():  return ok(data=int(ECF2.rd(Ecf2.GPMD)))
def gpumode_set(mode):
    mode = int(mode)
    if mode not in (0, 1): return fail("无效模式 (0=混合 1=独显直连)")
    ECF2.wr(Ecf2.GPMD, mode)
    return ok("已写入, 重启后生效", {"mode": mode})

# ============================ 控制器: CPU / Power ============================

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

# ============================ 控制器: NVIDIA ============================

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

# ============================ SystemInfo / Config / AutoStart ============================

def system_overview():
    m = re.search(r"MemTotal:\s+(\d+)", readf("/proc/meminfo", ""))
    memtot = f"{int(m.group(1)) // 1024 // 1024}GB" if m else "-"
    kernel = readf("/proc/sys/kernel/osrelease", "-")
    return ok(data={"CpuName": cpu_info()["Data"]["Name"],
                    "GpuName": _first(nvidia_query("name")) or "-",
                    "OsVersion": f"Linux {kernel}", "MemoryInfo": memtot})

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
                "CpuFanCurve": [{"temp": 40, "speed": 1500}, {"temp": 55, "speed": 2200},
                                {"temp": 65, "speed": 3000}, {"temp": 75, "speed": 4200},
                                {"temp": 85, "speed": 5500}, {"temp": 92, "speed": 6800}],
                "GpuFanCurve": [{"temp": 40, "speed": 1500}, {"temp": 55, "speed": 2200},
                                {"temp": 65, "speed": 3000}, {"temp": 75, "speed": 4200},
                                {"temp": 85, "speed": 5500}, {"temp": 92, "speed": 6800}]},
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

# ============================ AutoFan ============================

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

# ============================ RyzenSmu ============================

def smu_telemetry():
    freq = cpu_freq_mhz()
    usage = cpu_usage()
    # TDC/EDC: Linux 无标准电流遥测接口, 返回 None(前端显示 —)而非误导性 0
    return ok(data={"Ppt": rapl_power_watts(), "Tdc": None, "Edc": None,
                    "Temp": cpu_temp(), "FreqMhz": freq, "Usage": usage})

SMU_SYS = "/sys/kernel/ryzen_smu_drv"
def _smu_exec(is_mp1, cmd, args=None):
    """ryzen_smu sysfs 二进制接口: smu_args=<6×u32 LE>, cmd寄存器=<u32 LE>.
    与已验证的 jiaolong-cpu-undervolt 脚本完全一致; 文本 echo 方式会挂起."""
    if not os.path.isdir(SMU_SYS): return None, "ryzen_smu 未加载"
    try:
        vals = [int(v) & 0xFFFFFFFF for v in (args or [])][:6]
        vals += [0] * (6 - len(vals))
        with open(f"{SMU_SYS}/smu_args", "wb", buffering=0) as f:
            f.write(struct.pack("<6I", *vals))
        cf = f"{SMU_SYS}/{'mp1_smu_cmd' if is_mp1 else 'rsmu_cmd'}"
        with open(cf, "wb", buffering=0) as f:
            f.write(struct.pack("<I", int(cmd)))
        time.sleep(0.05)
        with open(cf, "rb") as f:
            rsp = struct.unpack("<I", f.read(4))[0]
        return rsp, None
    except Exception as e:
        return None, str(e)

_SMU_LIMITS = {"stapm": (False, 0x56), "ppt_rsmu": (False, 0x56),
               "fast": (True, 0x57), "slow": (True, 0x58),
               "temp_rsmu": (False, 0x59)}
def smu_set_limit(watts, which):
    is_mp1, cmd = _SMU_LIMITS[which]
    rsp, err = _smu_exec(is_mp1, cmd, [int(float(watts) * 1000)])
    if err: return fail(err)
    return ok("设置成功", {"rsp": hex(rsp)}) if rsp == 1 else fail(f"SMU 响应 {hex(rsp) if rsp is not None else 'None'}")

def smu_co_all(value):
    """全核 Curve Optimizer: RSMU cmd 0x07, arg0=有符号值u32表示 (已在降压服务验证)"""
    v = int(value) & 0xFFFFFFFF
    rsp, err = _smu_exec(False, 0x07, [v])
    if err: return fail(err)
    return ok("CO 已应用", {"rsp": hex(rsp)}) if rsp == 1 else fail(f"SMU 响应 {rsp}")

# ============================ Bridge 分发表 ============================

H = {}
def h(group, method):
    def deco(fn):
        H[(group, method)] = fn
        return fn
    return deco

h("CPU","GetCPUThermometer")(lambda a: ok(data=cpu_temp()))
h("CPU","GetCpuUsage")(lambda a: ok(data=cpu_usage()))
h("CPU","GetCpuFrequency")(lambda a: ok(data=cpu_freq_mhz()))
h("CPU","GetCpuInfo")(lambda a: cpu_info())
h("CPU","GetPhysicalCoreCount")(lambda a: ok(data=cpu_info()["Data"]["Cores"]))
h("CPU","GetCpuVoltage")(lambda a: fail("Linux 暂无 CPU 电压传感器 (SVI2 遥测未实现)"))
h("CPU","GetCustomMode")(lambda a: ok(data=config_get()["Data"].get("customMode", False)))
h("CPU","SetCustomMode")(lambda a: (config_set({**config_get()["Data"], "customMode": bool(a[0])}), ok("设置成功"))[1])
h("CPU","SetCpuShortPower")(lambda a: smu_set_limit(a[0], "fast"))
h("CPU","SetCpuLongPower")(lambda a: smu_set_limit(a[0], "stapm"))
h("CPU","SetCPUTempWall")(lambda a: smu_set_limit(a[0], "temp_rsmu"))

h("Fan","GetFanSpeed")(lambda a: fan_speed_get())
h("Fan","SetFanSpeed")(lambda a: fan_set(a[0]))
h("Fan","RemoveFanSpeed")(lambda a: fan_auto())
h("Fan","GetMaxFanSpeedSwitch")(lambda a: maxfanswitch_get())
h("Fan","SetMaxFanSpeedSwitch")(lambda a: maxfanswitch_set(bool(a[0])))

h("GPU","Get")(lambda a: gpumode_get())
h("GPU","Set")(lambda a: gpumode_set(a[0]))
h("LogoLight","Get")(lambda a: logo_get())
h("LogoLight","Set")(lambda a: logo_set(bool(a[0])))

h("Keyboard","GetColor")(lambda a: kb_color_get())
h("Keyboard","SetColor")(lambda a: kb_color_set(*a[:3]))
h("Keyboard","GetMode")(lambda a: kb_mode_get())
h("Keyboard","SetMode")(lambda a: kb_mode_set(a[0]))
h("Keyboard","GetLightBrightness")(lambda a: kb_brightness_get())
h("Keyboard","SetLightBrightness")(lambda a: kb_brightness_set(a[0]))

h("PerformanceMode","Get")(lambda a: perfmode_get())
h("PerformanceMode","Set")(lambda a: perfmode_set(a[0]))

_NG = "NvidiaGpu"
h(_NG,"GetGpuName")(lambda a: ok(data=_first(nvidia_query("name"))))
h(_NG,"GetGpuDriverVersion")(lambda a: ok(data=_first(nvidia_query("driver_version"))))
h(_NG,"GetGpuDriverDate")(lambda a: ok(data="-"))
h(_NG,"GetGpuMemoryTotal")(lambda a: ok(data=_first(nvidia_query("memory.total"))))
h(_NG,"GetGpuBusWidth")(lambda a: ok(data=_first(nvidia_query("pcie.link.width.current"))))
h(_NG,"GetGpuUtilization")(lambda a: ok(data=int(float(_first(nvidia_query("utilization.gpu")).replace("%","").strip() or 0))))
h(_NG,"GetGpuMemoryUtilization")(lambda a: ok(data=int(float(_first(nvidia_query("utilization.memory")).replace("%","").strip() or 0))))
h(_NG,"GetGpuCoreClock")(lambda a: ok(data=int(float((_first(nvidia_query("clocks.gr")) or "0").replace("MHz","").strip()))))
h(_NG,"GetGpuMemoryClock")(lambda a: ok(data=int(float((_first(nvidia_query("clocks.mem")) or "0").replace("MHz","").strip()))))
h(_NG,"GetGpuTemperature")(lambda a: ok(data=int(float((_first(nvidia_query("temperature.gpu")) or "0")))))
h(_NG,"GetGpuFanSpeed")(lambda a: ok(data=(lambda f2: f2 * 100 if 0 < f2 < 100 else f2)(
    (ECF2.rd(Ecf2.F2HI) << 8) | ECF2.rd(Ecf2.F2LO))))   # C# 原版返回单值 int, 前端按数字消费
h(_NG,"GetGpuCoreClockRange")(lambda a: ok(data={"Min": 210, "Max": float((_first(nvidia_query("clocks.max.gr")) or "0").replace("MHz","").strip() or 2595)}))
h(_NG,"GetGpuMemoryClockRange")(lambda a: ok(data={"Min": 1000, "Max": float((_first(nvidia_query("clocks.max.mem")) or "0").replace("MHz","").strip() or 8000)}))
h(_NG,"GetGpuPowerLimitRange")(lambda a: gpu_powerlimit_range())
h(_NG,"LockGpuClock")(lambda a: _nvidia_action(["--lock-gpu-clocks=", str(int(a[0]))]))
h(_NG,"LockGpuClockRange")(lambda a: _nvidia_action(["--lock-gpu-clocks=", f"{int(a[0])},{int(a[1])}" if len(a) > 1 else str(int(a[0]))]))
h(_NG,"ResetGpuClock")(lambda a: _nvidia_action(["--lock-gpu-clocks=reset"]))
h(_NG,"LockMemoryClock")(lambda a: fail("显存锁频在本机 Optimus 下不支持"))
h(_NG,"ResetMemoryClock")(lambda a: ok())
h(_NG,"SetPowerLimit")(lambda a: _nvidia_action(["--power-limit=", str(int(a[0]))]))

h("Power","SetCPUMaxFrequency")(lambda a: power_freqmax_set(a[0]))
h("Power","ResetCPUMaxFrequency")(lambda a: power_freqmax_reset())
h("Power","SetCPUMaxState")(lambda a: power_freqmax_set(int(int(a[0]) * CPU_MAX_MHZ / 100)))
h("Power","DisableTurbo")(lambda a: turbo_set(False))
h("Power","EnableTurbo")(lambda a: turbo_set(True))
h("Power","GetCPUMaxFrequency")(lambda a: power_freqmax_get())
h("Power","GetTurboEnabled")(lambda a: turbo_get())

h("SystemInfo","GetSystemOverview")(lambda a: system_overview())
h("SystemInfo","OpenUrl")(lambda a: ok())
h("ConfigCtrl","GetConfig")(lambda a: config_get())
h("ConfigCtrl","SetConfig")(lambda a: config_set(json.loads(a[0]) if isinstance(a[0], str) else a[0]))
h("AutoStart","Enable")(lambda a: autostart_set(True))
h("AutoStart","Disable")(lambda a: autostart_set(False))
h("AutoStart","IsEnabled")(lambda a: autostart_enabled())

h("AutoFan","Start")(lambda a: autofan_start())
h("AutoFan","Stop")(lambda a: autofan_stop())
h("AutoFan","IsRunning")(lambda a: autofan_state())

h("RyzenSmu","GetSmuTelemetry")(lambda a: smu_telemetry())
h("RyzenSmu","SetStapmLimit")(lambda a: smu_set_limit(a[0], "stapm"))
h("RyzenSmu","SetFastLimit")(lambda a: smu_set_limit(a[0], "fast"))
h("RyzenSmu","SetSlowLimit")(lambda a: smu_set_limit(a[0], "slow"))
h("RyzenSmu","SetStapmTime")(lambda a: ok())
h("RyzenSmu","SetSlowTime")(lambda a: ok())
h("RyzenSmu","SetPptLimitRsmu")(lambda a: smu_set_limit(a[0], "ppt_rsmu"))
# ---- 补齐 RyzenSmu 写端点 (Dragon Range 命令号来自 C# RyzenSmuController default 分支) ----
def _smu_simple(is_mp1, cmd, name, val):
    rsp, err = _smu_exec(is_mp1, cmd, [int(val) & 0xFFFFFFFF])
    if err: return fail(err)
    return ok(f"{name} 设置成功", {"rsp": hex(rsp)}) if rsp == 1 else fail(f"SMU 响应 {hex(rsp) if rsp is not None else 'None'}")
h("RyzenSmu","SetVrmCurrentRsmu")(lambda a: _smu_simple(False, 0x57, "VRM Current", a[0]))
h("RyzenSmu","SetEdcLimitRsmu") (lambda a: _smu_simple(False, 0x58, "EDC Limit", a[0]))
h("RyzenSmu","SetTempLimitRsmu")(lambda a: _smu_simple(False, 0x59, "Temp Limit", a[0]))
h("RyzenSmu","SetPboScalar")    (lambda a: _smu_simple(False, 0x5B, "PBO Scalar", a[0]))
h("RyzenSmu","SetCurveOptimizerAll")(lambda a: smu_co_all(a[0]))

def _unsupported(name):
    def fn(a): return fail(f"{name}: 该 SMU 写操作未开放 (安全起见请用既有降压脚本/CLI)")
    return fn
for _m in ("SetVrmCurrentMp1","SetTdcLimitMp1","SetEdcLimitMp1","SetTempLimitMp1",
           "SetOcClk","SetPerCoreOcClk","SetOcVolt"):
    h("RyzenSmu",_m)(_unsupported(_m))

# ============================ HTTP Server ============================

DIST = os.path.join(os.path.dirname(os.path.abspath(__file__)),
                    "..", "bin", "publish", "WebRoot")
MIME = {".html": "text/html", ".js": "text/javascript", ".css": "text/css",
        ".png": "image/png", ".svg": "image/svg+xml", ".ico": "image/x-icon",
        ".woff2": "font/woff2", ".json": "application/json"}

# ---- 补齐前端依赖的缺失端点 (2026-08-24 修复 CPU/Fan 页面卡加载) ----
h("Power", "GetCPUMaxState")(lambda a: ok(data=int(ECF2.rd(240))))               # CMEN 所在字节: CPUPower 状态
h("CPU", "GetCPUMaxState")(lambda a: ok(data=int(ECF2.rd(240))))
h("Keyboard", "GetKeyboardMode")(lambda a: kb_mode_get())                        # LEDM 别名
h("RyzenSmu", "GetSmuVersion")(lambda a: ok(data=(readf(f"{SMU_SYS}/version") or "").strip()))
h("RyzenSmu", "GetCurveOptimizerSign")(lambda a: ok(data=-1))                    # 负值=降压 (与 Windows 一致)
h("RyzenSmu", "DisableOc")(lambda a: ok("已禁用手动超频"))
def _get_custom_mode(a):
    on = bool(ECF2.rdbit(Ecf2.CMEN_B))
    return ok("已开启" if on else "已关闭", data=on)
h("CPU", "GetCustomMode")(_get_custom_mode)
h("Power", "GetCPUPowerLimit")(lambda a: ok(data={"spl": int(ECF2.rd(Ecf2.CSPL)),
                                                  "sppt": int(ECF2.rd(Ecf2.FPPT)),
                                                  "fppt": int(ECF2.rd(Ecf2.CTCL))}))
h("GPU", "GetGpuMemoryClockRange")(lambda a: fail("混合输出下不支持显存锁频"))

def api_dispatch(group, method, args):
    fn = H.get((group, method))
    if fn is None:
        return {"Success": False, "Message": f"未实现: {group}.{method}"}
    try:
        return fn(args)
    except Exception as e:
        return {"Success": False, "Message": f"{type(e).__name__}: {e}"}

def serve(port=8800):
    from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

    class Handler(BaseHTTPRequestHandler):
        protocol_version = "HTTP/1.1"

        def log_message(self, *a): pass

        def _send(self, body, ctype="application/json"):
            data = body if isinstance(body, bytes) else json.dumps(body, ensure_ascii=False).encode()
            self.send_response(200)
            self.send_header("Content-Type", f"{ctype}; charset=utf-8")
            self.send_header("Content-Length", str(len(data)))
            if "html" in ctype:
                # index.html 不缓存: 保证浏览器总是加载最新 hash 的 assets
                self.send_header("Cache-Control", "no-cache, no-store, must-revalidate")
            self.end_headers()
            self.wfile.write(data)

        def do_GET(self):
            path = self.path.split("?")[0]
            if path.startswith("/api/"):
                parts = path.rstrip("/").partition("/api/")[2].strip("/").split("/")
                res = api_dispatch(parts[0], parts[1] if len(parts) > 1 else "", [])
                return self._send(res)
            if path in ("/", "/index.html"):
                fp = os.path.join(DIST, "index.html")
                if os.path.exists(fp):
                    return self._send(open(fp, "rb").read(), "text/html")
                return self._send("<h1>JiaolongControl Linux</h1><p>前端未构建: cd Client && npm run build</p>", "text/html")
            safe = os.path.normpath(path).lstrip("/")
            fp = os.path.join(DIST, safe)
            if fp.startswith(DIST) and os.path.isfile(fp):
                ext = os.path.splitext(fp)[1]
                return self._send(open(fp, "rb").read(), MIME.get(ext, "application/octet-stream"))
            fp2 = os.path.join(DIST, "index.html")
            if os.path.exists(fp2):
                return self._send(open(fp2, "rb").read(), "text/html")
            self._send({"error": "not found"})

        def do_POST(self):
            path = self.path.split("?")[0]
            if not path.startswith("/api/"):
                return self._send({"error": "not found"})
            ln = int(self.headers.get("Content-Length", 0) or 0)
            try:
                body = json.loads(self.rfile.read(ln) or b"{}")
            except Exception:
                body = {}
            parts = path.rstrip("/").partition("/api/")[2].strip("/").split("/")
            res = api_dispatch(parts[0], parts[1] if len(parts) > 1 else "", body.get("args", []))
            self._send(res)

    try:
        srv = ThreadingHTTPServer(("127.0.0.1", port), Handler)
    except OSError as e:
        if e.errno == 98:
            print(f"错误: 端口 {port} 已被占用。\n"
                  f"  可能已有一个 jlctl serve 在运行:  pgrep -af jlctl.py\n"
                  f"  停止它:                            sudo fuser -k {port}/tcp", flush=True)
            return 1
        raise
    try:
        boot = apply_boot_config()
        print(f"[jlctl] boot config applied: {boot}", flush=True)
    except Exception as e:
        print(f"[jlctl] boot config apply failed: {e}", flush=True)
    print(f"JiaolongControl WebUI: http://127.0.0.1:{port}/  (dist={DIST})", flush=True)
    srv.serve_forever()

# ============================ CLI ============================

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

if __name__ == "__main__":
    sys.exit(main())
