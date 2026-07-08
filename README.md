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
  <img src="https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet" alt=".NET">
  <img src="https://img.shields.io/badge/Vue-3.5-4FC08D?logo=vuedotjs" alt="Vue">
  <img src="https://img.shields.io/badge/license-MIT-green" alt="License">
</p>

---

## 功能

### CPU
- **功率控制** — 短时功率 (SPL) / 长时功率 (SPP) 调节
- **温度墙** — 60°C ~ 105°C 可设
- **睿频开关** — 通过 `powercfg` 修改电源计划
- **最大频率限制** — 支持 AC / DC 分别设定
- **实时监控** — 温度、使用率、频率、电压

### GPU
- **显卡模式切换** — 混合输出 / 独显直连
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
- **Curve Optimizer** — 全核 / 分核，正压 / 降压
- 自动检测 CPU 家族（Dragon Range / FP7 / FP8 / Strix / FP6）

### 风扇
- **手动控制** — CPU / GPU 风扇独立调速
- **高级自动风扇** — 温度驱动的智能调速：
  - 交叉散热算法（CPU/GPU 温度互相影响）
  - 温度平滑滤波
  - 爬升/下降速率限制
  - 共享热管同步
- **风扇曲线编辑器** — 可视化编辑温度-转速曲线
- **开机自启恢复** — 启动时自动恢复风扇策略

### RGB 键盘
- 颜色自定义（R / G / B ）
- 亮度 4 级调节
- 固定色 / 关闭模式

### 环境光
- Logo 灯开关控制

---

## 使用说明

1. 从 [Releases](../../releases) 下载最新安装包
2. 运行安装程序（Inno Setup）
3. 启动后会在系统托盘显示图标，右键可显示主界面或退出
4. 在设置页可配置开机自启和启动最小化

> **注意：** 修改硬件参数有一定风险，请确保理解各项设置的含义后再操作。使用前建议备份当前配置。

---

## 开发

```bash
# 前端开发（需要 Node.js 18+）
cd JiaoLongControl/Client
npm install
npm run dev

# 后端构建（需要 .NET 8 SDK）
dotnet build JiaoLongControl/JiaoLongControl.csproj

# 发布
dotnet publish JiaoLongControl/JiaoLongControl.csproj -c Release
```

前端开发时 Vite dev server 运行在 `localhost:5173`，后端 WebView2 在开发模式下指向该地址。

---

## 许可证

[MIT](LICENSE.md) © 2025 GaoXanSheng

---

<p align="center">
  <sub>使用风险自负 · 非官方工具 · 与机械革命/同方无关联</sub>
</p>
