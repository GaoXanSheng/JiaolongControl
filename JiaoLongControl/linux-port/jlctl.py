#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
JiaolongControl Linux Port — CLI + WebUI 后端入口

机器: MECHREVO Jiaolong MRID6 (Ryzen 7 7745HX + RTX 4060 Laptop)

硬件通路 (ground-truth 来自 ACPI SSDT4/DSDT 反编译 + 实测):
  A. ECF2 共享内存  (/dev/mem @ 0xFE800400, 256B): 性能模式/RGB键盘/Logo灯/
     FnLock/触控板锁/显卡模式/CPU温度/风扇转速镜像 —— 即 Windows 端 WMI
     MiInterface(MIFS) 的底层落点, 绕过 0xBAEAF000 mailbox 直写
  B. EC 端口 IO     (/dev/port @ 0x4E/0x4F): 风扇手动转速/模式 —— Blding64 协议
  C. amd_pstate/sysfs: CPU 频率墙/EPP/boost/governor
  D. nvidia-smi:     GPU 遥测/锁频/功耗墙 (root)
  E. k10temp/RAPL:   SMU 遥测 (替代 LibreHardwareMonitor)

需要 root 运行: sudo python3 jlctl.py <command>。
实际实现已按职责拆分到包 Server/ (见该包 __init__.py 的模块说明)。
"""
import os
import sys

if __name__ == "__main__":
    sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
    from Server.cli import main
    sys.exit(main())
