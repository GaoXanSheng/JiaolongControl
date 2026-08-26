# -*- coding: utf-8 -*-
"""通路 A: ECF2 共享内存 (/dev/mem @ 0xFE800400, 256B).

与 ACPI OperationRegion(ECF2, SystemMemory, 0xFE800400, 0xFF) 完全一致,
字段偏移由 DSDT H_EC Field 定义逐位解析得出.
"""

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
