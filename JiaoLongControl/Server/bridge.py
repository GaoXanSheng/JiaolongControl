# -*- coding: utf-8 -*-
"""Bridge 分发表: H[(group, method)] → handler(args) 的完整注册表.

与 Windows 端 C# Bridge 端点一一对应; 注册项按 CPU/Fan/GPU/LogoLight/
Keyboard/PerformanceMode/NvidiaGpu/Power/SystemInfo/ConfigCtrl/AutoStart/
AutoFan/RyzenSmu 分组, 由 server.api_dispatch 或 CLI 消费.
"""
import json

from .common import ok, fail
from .autofan import autofan_start, autofan_stop, autofan_state
from .cpu_power import (CPU_MAX_MHZ, cpu_info, power_freqmax_get,
                        power_freqmax_reset, power_freqmax_set, turbo_get, turbo_set)
from .ecf2 import Ecf2
from .gpu import _nvidia_action, gpu_powerlimit_range, gpu_stats
from .hardware import (ECF2, _first, cpu_freq_mhz, cpu_temp, cpu_usage,
                       nvidia_query, readf)
from .mifs import (fan_auto, fan_set, fan_speed_get, fnlock_get, fnlock_set,
                   gpumode_get, gpumode_set, kb_brightness_get,
                   kb_brightness_set, kb_color_get, kb_color_set, kb_mode_get,
                   kb_mode_set, logo_get, logo_set, maxfanswitch_get,
                   maxfanswitch_set, perfmode_get, perfmode_set, tplock_get, tplock_set)
from .smu import (SMU_SYS, _smu_exec, smu_co_all, smu_set_limit, smu_telemetry)
from .system_info import (autostart_enabled, autostart_set, config_get,
                          config_set, system_overview)

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
