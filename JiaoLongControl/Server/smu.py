# -*- coding: utf-8 -*-
"""RyzenSmu — ryzen_smu 内核模块 sysfs 二进制接口 (遥测/限制/全核降压).

文本 echo 方式会挂起, 必须与已验证的 jiaolong-cpu-undervolt 脚本一致地以
二进制写入 smu_args / cmd 寄存器. 依赖: hardware (CPU 遥测助手).
"""
import os
import struct
import time

from .common import ok, fail
from .hardware import cpu_freq_mhz, cpu_usage, rapl_power_watts, cpu_temp

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
