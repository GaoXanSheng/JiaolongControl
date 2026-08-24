# JiaolongControl Linux 移植 — 硬件协议全解 (ground-truth, 已实测)

日期 2026-08-23。机器: MECHREVO Jiaolong MRID6 / Ryzen 7 7745HX / RTX 4060L / 内核 6.8.0-138 / nvidia 570.211.01

## 架构总览：两条独立 EC 通路 + SMU
| 通路 | 地址/端口 | 用途 | Linux 可达性(实测) |
|---|---|---|---|
| **A. Blding64 端口 EC** | IO 端口 0x4E(addr/模式)/0x4F(data) | 风扇转速设/读、风扇模式、芯片探测 | ✅ **用户态 ioperm 直读直写已验证** |
| **B. MIFS mailbox** | 物理内存 0xBAEAF000 (36B) | 性能模式/GPU模式/RGB/键锁 | ❌ /dev/mem 被拒(ACPI NVS+STRICT_DEVMEM) → 需内核模块 ioremap |
| **B'. H_EC MMIO** | 物理内存 0xFE800400 (0xFFB) | MIFS 的遥测读(风扇/温度/RGB状态) | ❌ 同上, 需内核模块 |
| **C. Ryzen SMU** | sysfs /sys/kernel/ryzen_smu_drv/* | CPU 降压/功率墙 (已有服务在用) | ✅ root 可读写, 已验证 |
| **D. NVIDIA** | nvidia-smi | GPU 频率/功耗/温度 | ✅ root, 已验证 -lgc 可用 |

## 通路 A: Blding64 端口 EC 协议 (已 1:1 复刻并实测通过)
端口: `ADDR=0x4E` (模式选择) `DATA=0x4F` (数据)
- 模式选择: 写 ADDR=0x2E 表示"下一条 DATA 是命令/地址", 写 ADDR=0x2F 表示"下一条 DATA 是数值"
- **读 EC_RAM(idx16)** 时序 (8 步):
  ```
  outb(0x2E,0x4E); outb(0x11,0x4F);   // cmd: 选高地址
  outb(0x2F,0x4E); outb(idx>>8,0x4F); // 高字节
  outb(0x2E,0x4E); outb(0x10,0x4F);   // cmd: 选低地址
  outb(0x2F,0x4E); outb(idx&0xFF,0x4F);// 低字节
  outb(0x2E,0x4E); outb(0x12,0x4F);   // cmd: 取数据
  outb(0x2F,0x4E);                    // ★关键: 切数值模式 (漏了会回显0x12)
  data = inb(0x4F);
  ```
- **写 EC_RAM(idx16,data)** 时序:
  ```
  outb(0x2E,0x4E); outb(0x11,0x4F);
  outb(0x2F,0x4E); outb(idx>>8,0x4F);
  outb(0x2E,0x4E); outb(0x10,0x4F);
  outb(0x2F,0x4E); outb(idx&0xFF,0x4F);
  outb(0x2E,0x4E); outb(0x12,0x4F);
  outb(0x2F,0x4E);
  outb(data,0x4F);
  ```
- 芯片存活探测: `EC_RAM_READ(0x2000) == 0x55` (EC_init 校验, 实测=0x55 ✅)
- 初始化: 若 0x2000==0x55, 则 `v=EC_READ(0x1060); EC_WRITE(0x1060, v|0x80)`

### 风扇寄存器 (SysEnums.ECMemoryTable)
| 寄存器 | 说明 |
|---|---|
| 0xC834 (lo)/0xC835 (hi) | Fan1 (CPU) 当前转速读 (实测 0x12=18? 单位存疑, 见下) |
| 0xC836/0xC837 | Fan2 (GPU) 当前转速读 (实测 0x01) |
| 0xC83C | Fan1_RPM_SET 写 (CpuFanSetSpeed 用) |
| 0xC83D | Fan2_RPM_SET 写 |
| 0xB20 | 风扇模式位: bit1(0x02)=CPU手动, bit3(0x08)=GPU手动, 0=自动 (RemoveFanSpeed 写 0) |
| 0xB21 | 附加 |
- CpuFanSetSpeed(s): EC_WRITE(0xC83C, s); EC_WRITE(0xB20, EC_READ(0xB20)|0x02)
- GpuFanSetSpeed(s): EC_WRITE(0xC83D, s); EC_WRITE(0xB20, EC_READ(0xB20)|0x08)
- RemoveFanSpeed(): GpuFanSetSpeed(0); CpuFanSetSpeed(0); EC_WRITE(0xB20,0)
- 转速单位: Windows 侧 1 单位≈100RPM, 上限 68 (6800RPM)。实测读值 0x12=18 → 1800RPM 合理(低负载)

## 通路 B: MIFS mailbox 0xBAEAF000 (36 字节)
SSDT4 Device(WMID) _HID PNP0C14 _UID MIFS:
```
OperationRegion (XGNS, SystemMemory, 0xBAEAF000, 0x24)
Field (XGNS, AnyAcc, Lock, Preserve) {
    MTID,  8,     // +0x00
    WMIB,  256,   // +0x01 (32 字节 = WMI 32B 协议缓冲!)
    CTID,  16,    // +0x21
    PSSP,  8      // +0x23  (指向 SystemIO 0x2B 的 CSPR 区, WSSP 字节)
}
Method (WSMI, 2) { MTID=Arg0; WMIB=Arg1; }   // 写入口
Method (WMAA, 3) { ... 读 H_EC 返回 RETS 32B } // 读出口
```
- **WSMI** 是 SET 路径: 把 MTID(1B)+WMIB(32B) 写入 0xBAEAF000。WMIB 就是 WMI 的 32 字节 InData。
- **WMAA** 是 GET 路径: 从 H_EC(0xFE800400) 读字段填 RETS。
- WMI 32B 协议 (MethodServices.cs):
  - `buffer[1]` = MethodType: 0xFA(250)=Get, 0xFB(251)=Set
  - `buffer[3]` = MethodName
  - `buffer[4..]` = 值 (set 时), 或结果 (get 时: data[4], 或 (data[4],data[5]) 为 u16, 或 (4,5,6))
- MethodName (enum, buffer[3]):
  8=SystemPerMode(性能/节能档) 9=GpuMode(混合/独显) 10=RgbKeyboardStatus 11=FnLock 12=TPLock
  13=CPUGPUFanSpeed 14=GPUFanSpeed_NotUse 15=Ambientlight 16=RGBKeyboardMode 17=RGBKeyboardColor
  18=RGBKeyboardBrightness 19=SystemAcType 20=MaxFanSpeedSwitch 21=MaxFanSpeed 22=CPUThermometer 23=CPUPower

## 通路 B': H_EC MMIO 0xFE800400 (0xFF 字节) — MIFS 遥测读源
DSDT Device(H_EC) _HID PNP0C09 (标准 EC):
- _CRS 请求 IO 0x0062(1B) + 0x0066(1B) [标准 EC data/cmd 端口]
- OperationRegion (ECF2, SystemMemory, 0xFE800400, 0xFF) — 但 ECAV 默认 0, ECRD/ECWT 是桩(仅当 _REG 置 ECAV=1 才真正 Deref)
- 关键字段 (0xFE800400+offset): 0x28=SMPR/SMST/SMAD/SMCD, 0x2A=SDAT(16B), 0x5F=FASP/ECWR, 0x60=PAWT, 0x61=B1SN(16b)...
- 注意: 标准 EC 端口 0x62/0x66 与 Blding64 的 0x4E/0x4F 是**不同的 EC 访问方式**(0x4E/0x4F 是 Blding 私有 RAM 映射协议)。两者都可达, 但寄存器含义不同。

## 内核模块需求 (通路 B/B')
0xBAEAF000 与 0xFE800400 均非 System RAM → STRICT_DEVMEM 下 /dev/mem 读/写都被拒(实测 0xBAEAF000 read=EPERM)。
**必须** 写内核模块 jiaolong_ec.ko: ioremap 0xBAEAF000(0x24) 和 0xFE800400(0xFF), 暴露 chardev:
- ioctl/写: WSMI(mtid, wmib32) → 性能模式/GPU模式/RGB 设置
- read: WMAA 等效 → 遥测
- (风扇可走用户态 0x4E/0x4F, 不进模块, 降低风险)

## 已有可复用 (勿破坏)
- `jiaolong-cpu-undervolt` 服务 (enabled+active): modprobe ryzen_smu + /usr/local/sbin/jiaolong-cpu-undervolt -50 (全核 CO -50)
- `lactd` 服务 (/etc/lact): 独立, 与本移植无关, 勿动
- ryzen_smu DKMS (codename 17, mp1_if_version 2, AM5_V1): MP1 msg 0x3B10530/rsp 0x3B1057C/arg 0x3B109C4; RSMU msg 0x3B10524/rsp 0x3B10570/arg 0x3B10A40

## 参考
- 同厂机械革命: github.com/xuwd1/mechrevo-wujie14-kmod (无界14, IP3 xN39 模具, 性能模式+键盘背光, DKMS)
- 原仓库: /home/pushuai/Documents/program/JiaolongControl/JiaolongControl
