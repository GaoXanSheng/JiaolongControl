<h1 align="center">JiaoLongControl</h1>

<p align="center">
  <strong>蛟龙 16 PRO 笔记本硬件控制中心</strong><br>
  <em>基于 7945HX + RTX 4060 版本开发，理论兼容其他 16 PRO [2023] 版本</em>
</p>

<p align="center">
  <img src="Doc/Main.png" alt="主界面" width="800" />
</p>

<p align="center">
  <a href="https://qm.qq.com/q/4ase4LoAJi">
    <img src="https://img.shields.io/badge/QQ%20群-蛟龙工具箱问题反馈-EB1923?logo=tencentqq&logoColor=white" alt="QQ Group">
  </a>
  <img src="https://img.shields.io/badge/.NET-8.0_WPF-512BD4?logo=dotnet" alt=".NET">
  <img src="https://img.shields.io/badge/Vue-3.5-4FC08D?logo=vuedotjs" alt="Vue">
  <img src="https://img.shields.io/badge/WebView2-前端宿主-0078D7?logo=microsoftedge&logoColor=white" alt="WebView2">
  <img src="https://img.shields.io/badge/license-MIT-green" alt="License">
</p>

---

## 简介

JiaoLongControl 是一套面向机械革命蛟龙 16 PRO 的**桌面级硬件控制中心**：

- **WPF 原生壳 + WebView2 前端**：C# 侧负责驱动与硬件交互，Vue 3 前端负责界面，通过 WebView2 Host Object 双向桥接
- **全量配置持久化**：所有设置保存于 `config/config.yaml`（原子写入 + `.bak` 备份），支持无损版本迁移
- **开机策略**：计划任务方式开机自启，各模块策略（风扇 / CPU / GPU / SMU 降压 / 键盘渐变）启动与睡眠唤醒时自动恢复

## 功能

### CPU
- **功率控制** — 短时功率 (SPL) / 长时功率 (SPP) 调节
- **温度墙** — 60°C ~ 105°C 可设
- **睿频开关** — 通过 `powercfg` 修改电源计划
- **最大频率限制** — 支持 AC / DC 分别设定
- **实时监控** — 温度、使用率、频率、电压

### GPU
- **显卡模式切换** — 混合输出 / 独显直连（重启生效）
- **核心频率锁定** — 锁定指定频率，支持范围检测
- **显存频率锁定** — 同上
- **功耗限制** — mW 级精度调节
- **解锁 DB** — 通过 NVPCF 驱动解锁 GPU 功率上限
- **实时监控** — 使用率、显存占用、核心/显存频率、温度、风扇转速

### Ryzen SMU（高级 CPU 调校）
- **功耗限制** — STAPM / Fast PPT / Slow PPT / PPT
- **电流限制** — VRM / TDC / EDC
- **温度限制** — MP1 / RSMU
- **PBO** — Scalar / OC Clock / Per-Core OC Clock
- **Curve Optimizer** — 全核 / 分核，正压 / 降压；分核数值**持久化到配置文件**，启动时按所选模式（全核 / 分核）自动应用
- 自动检测 CPU 家族（Dragon Range / FP7 / FP8 / Strix / FP6）

### 风扇
- **手动控制** — CPU / GPU 风扇独立调速
- **高级自动风扇** — 温度驱动的智能调速：
  - 交叉散热算法（CPU/GPU 温度互相影响）
  - 温度平滑滤波
  - 爬升/下降速率限制
  - 共享热管同步
- **风扇曲线编辑器** — 可视化编辑温度-转速曲线，支持多曲线合并显示
- **开机自启恢复** — 启动时自动恢复风扇策略

### RGB 键盘
- 颜色自定义（R / G / B）
- 亮度 4 级调节
- 固定色 / 关闭模式
- 开机渐变灯效（睡眠唤醒自动恢复）

### OSD 屏幕显示
- **胶囊式悬浮指示器** — 全圆角 + 亚克力质感底板 + 状态强调色光晕，点击穿透、不抢焦点、始终置顶
- **键盘事件触发** — 全局低级键盘钩子监听音量增减 / 静音 / 大写 / 数字 / 滚动锁定，不拦截按键、不影响系统行为
- **EC 事件通道** — 订阅 `HID_EVENT20` WMI 事件，原生 Fn 热键（性能模式 / 键盘背光 / Fn 锁 / 触摸板锁）即时感知；事件不可用时自动退回轮询兜底
- **性能模式分色** — 性能（红·闪电）/ 均衡（蓝·仪表）/ 安静（绿·静音）三种形态随状态切换
- **动画** — 光晕浮现 → 胶囊展开 → 内容级联渐显 → 呼吸驻留；退场收拢压缩消散
- **缩放与清晰度** — 按显示器 DPI 物理像素定位（PerMonitorV2），字号真实排版（微软雅黑 + ClearType），支持自定义缩放
- **主题自适应** — 深色 / 浅色跟随应用主题

### 常规设置
- **锁定控制** — 大写键 / 数字键 / 功能键（Fn Lock）/ 触摸板锁定一键切换，状态实时同步（C# 检测到变化主动推送）
- **Logo A壳徽标灯** — 个性化 A 面灯光开关
- **独显直连** — 强制独立显卡输出（重启生效）
- **风扇曲线合并** — 风扇曲线页多曲线合并显示

### 系统设置
- **开机自启 / 自启时最小化** — 基于 Windows 计划任务，随托盘常驻
- **自启动策略** — 高级风扇 / CPU / GPU / SMU 降压 / 键盘渐变等模块开机自动应用
- **界面主题** — 浅色 / 深色 / 跟随系统
- **PawnIO 驱动检测** — SMU 功能依赖的 PawnIO 运行时状态提示

---

## 使用说明

1. 从 [Releases](../../releases) 下载最新安装包
2. 运行安装程序（Inno Setup），安装 [PawnIO](https://pawnio.eu/) 运行时（Ryzen SMU 功能依赖）
3. 启动后会在系统托盘显示图标，右键可显示主界面或退出
4. 在系统设置页可配置开机自启和启动最小化；在常规设置页可快捷切换各类锁定状态

> **注意：** 修改硬件参数有一定风险，请确保理解各项设置的含义后再操作。使用前建议备份当前配置。Curve Optimizer 降压过度可能导致死机 / 蓝屏，请从小幅度开始逐核验证。

---

## 开发

```bash
# 前端开发（需要 Node.js 20.19+ / 22.12+）
cd JiaoLongControl/Client
npm install
npm run dev

# 后端构建（需要 .NET 8 SDK，目标平台 x64）
dotnet build JiaoLongControl.sln -p:Platform=x64

# 发布
dotnet publish JiaoLongControl/JiaoLongControl.csproj -c Release
```

- 前端开发时 Vite dev server 运行在 `localhost:5173`，后端 WebView2 在开发模式下指向该地址
- 调试 SMU 功能请安装 PawnIO；EC / 风扇功能依赖随包分发的 `JiaoLongDriver64` 驱动
- 程序清单要求**管理员权限**运行（硬件访问所需）
- 配置文件位于程序目录 `config/config.yaml`，字段含义见文件内注释

---

## 许可证

[MIT](LICENSE.md) © 2025 GaoXanSheng

---

<p align="center">
  <sub>使用风险自负 · 非官方工具 · 与机械革命/清华同方无关联</sub>
</p>
