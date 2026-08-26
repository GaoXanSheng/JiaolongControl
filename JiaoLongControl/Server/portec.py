# -*- coding: utf-8 -*-
"""通路 B: EC 端口 IO (/dev/port @ 0x4E/0x4F) — Blding64 协议.

用于风扇手动转速/模式; 读时序末尾必须 0x4E←0x2F 切回数据模式后再 inb(0x4F),
否则读到命令回显.
"""

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
