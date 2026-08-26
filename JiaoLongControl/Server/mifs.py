# -*- coding: utf-8 -*-
"""控制器: MIFS 等价 (ECF2) — 性能模式/风扇/键盘RGB/Logo/FnLock/触摸板锁/显卡模式."""
from .common import ok, fail
from .ecf2 import Ecf2
from .hardware import ECF2, PEC

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
    from . import autofan           # 延迟导入: autofan 依赖本模块的 maxfanswitch_set
    autofan.autofan_stop()
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
    from . import autofan           # 延迟导入: autofan 依赖本模块
    autofan.autofan_stop()
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
