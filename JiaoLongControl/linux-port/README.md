# JiaolongControl — Linux Port (蛟龙16 Pro)

原仓库 [GaoXanSheng/JiaolongControl](https://github.com/GaoXanSheng/JiaolongControl) 的 Linux 移植：
保留原版 Vue3 WebUI 外观与全部交互，用 **纯 Python 标准库后端** 替换 .NET/WPF/WebView2 壳，
提供 **CLI (`jlctl.py`)** 与 **WebUI** 两个入口。

适配机型: MECHREVO 蛟龙16 Pro MRID6 (Ryzen 7 7745HX + RTX 4060 Laptop)。
理论兼容同模具 7945HX 及其他蛟龙 16 Pro [2023] 版本。

## 快速开始

```bash
# CLI
sudo python3 jlctl.py status          # 全家桶状态
sudo python3 jlctl.py perfmode balance # 性能模式: performance/quiet/balance
sudo python3 jlctl.py fan set 60       # 手动风扇 60% (0-100)
sudo python3 jlctl.py fan auto         # 恢复 EC 自动调速
sudo python3 jlctl.py rgb color 255 60 0
sudo python3 jlctl.py serve --port 8800 # 启动 WebUI → http://127.0.0.1:8800/
```

WebUI 前端已预构建在 `../../bin/publish/WebRoot`（由 `JiaoLongControl/Client/` vite 构建产出）。
如需重建前端: `cd ../../JiaoLongControl/Client && npm install && npx vite build`。

## 代码结构

`jlctl.py` 是薄入口，实际实现按职责拆分在 `../Server/` 包中：

| 模块 | 职责 |
|---|---|
| `Server/common.py` | CommandResult 助手 (`ok`/`fail`) |
| `Server/ecf2.py` / `portec.py` | EC 硬件访问：ECF2 共享内存 / EC 端口 IO (Blding64) |
| `Server/hardware.py` | ECF2/PEC 单例、root 检查、sysfs/procfs 助手、nvidia-smi 封装 |
| `Server/mifs.py` | 性能模式/风扇/键盘 RGB/Logo/FnLock/触控板锁/显卡模式 |
| `Server/cpu_power.py` | CPU 频率墙/Turbo/EPP/governor |
| `Server/gpu.py` | NVIDIA 遥测/锁频/功耗墙 |
| `Server/system_info.py` | 系统概览、持久化配置、systemd 自启 |
| `Server/autofan.py` | 自动风扇后台线程 + 开机配置应用 |
| `Server/smu.py` | RyzenSmu sysfs 二进制接口 |
| `Server/bridge.py` | API 分发表 `H` 与全部 handler 注册 |
| `Server/server.py` | HTTP 服务 (前端托管 + `/api/*` 分发) |
| `Server/cli.py` | argparse 子命令解析与分发 |

## 功能矩阵

| 功能 | 实现通路 | 状态 |
|---|---|---|
| 性能模式 (高性能/静音/平衡) | ECF2 共享内存 TFLG=0x55→ITSM | ✅ |
| CPU 温度/频率/占用率/信息 | k10temp / amd_pstate sysfs / procfs | ✅ |
| CPU 频率墙 / Turbo / EPP / governor | scaling_max_freq / cpufreq boost / EPP | ✅ |
| 风扇转速读取 | ECF2 F1/F2 寄存器 | ✅ |
| 风扇手动定速 / 自动曲线 | EC 端口 0x4E/0x4F (Blding64 协议) + 后台线程 | ✅ |
| RGB 键盘 开关/颜色/亮度 | ECF2 LEDM/RGBR/G/B/KBNL | ✅ |
| Logo 灯 / FnLock / 触控板锁 | ECF2 FWDE/FNHK/TOCP 位域 | ✅ |
| GPU 模式(混合/独显) | ECF2 GPMD (重启生效, 与 Windows 一致) | ✅ |
| NVIDIA 遥测/锁频/功耗墙 | nvidia-smi (root) | ✅ |
| SMU 遥测 (PPT/温度/频率) | RAPL + k10temp (替代 LHM) | ✅ |
| SMU 功率墙 (SPL/SPPT/FPPT) | ryzen_smu DKMS sysfs | ⚠️ 实验性 |
| 配置保存 / 自启动 | ~/.config/jiaolongcontrol/config.json / systemd --user | ✅ |

## 技术内幕 (为什么能绕开 WMI)

Windows 版经 WMI `MICommonInterface`(MIFS_0) 调 `MiInterface`，其底层是
SSDT4 中 `WMID` 设备的 mailbox (物理内存 0xBAEAF000)。本机实测该 RAM 页
无法 ioremap(WC)/direct-map/dev-mem 访问（BIOS e820 单页异常），
但反编译发现 **MIFS 的全部落点就是 H_EC 的 ECF2 MMIO 区 (0xFE800400)**：

```
WMAA(InData[32B]) ─┬─ Get: 直接 ECRD(ECF2 字段)
                   └─ Set: 先 ECWT(0x55, TFLG) 触发, 再 ECWT(value, 字段)
```

因此本移植用 `/dev/mem` 直读直写 ECF2 即可等价实现 MIFS 全部功能；
风扇手动控制则复刻 Blding64.sys 的 EC 端口时序 (`/dev/port`)。
寄存器偏移全部由 DSDT `Field (ECF2)` 定义逐位解析得出，非猜测。

## 与 Windows 版差异

- 无托盘/开机自启 GUI（Linux 用 systemd user unit 可选）。
- CPU 电压显示不支持（无 per-core 电压 sysfs），SMU 曲线优化器请沿用既有降压脚本。
- 显存锁频 (-lmc) 在 Optimus 混合输出下不可用。
- MIFS mailbox (0xBAEAF000) 在本机 BIOS 下不可映射——本移植不依赖它。

## 安全说明

- 所有硬件写入均为与 ACPI AML / Windows 驱动完全相同的字节序列。
- 服务仅绑定 127.0.0.1，不对局域网开放。
- 需要 root（/dev/mem、/dev/port、nvidia-smi 特权操作）。
