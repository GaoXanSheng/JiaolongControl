using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using JiaoLongControl.Server.Core.Controllers;
using JiaoLongControl.Server.Core.Utils;

namespace JiaoLongControl.Server.Interop
{
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.AutoDual)]
    public class Bridge
    {
        public static Bridge Instance { get; } = new();

        // 保留属性供 C# 内部使用
        public CpuController CPU { get; } = new();
        public FanController Fan { get; } = new();
        public GpuController GPU { get; } = new();
        public LogoLightController LogoLight { get; } = new();
        public KeyboardController Keyboard { get; } = new();
        public PerformanceModeController PerformanceMode { get; } = new();
        public ConfigController Config { get; } = new();
        public AutoStartController AutoStart { get; } = new();
        public AutoFanControl AutoFan { get; } = new();
        public NvidiaGpuController NvidiaGpu { get; } = new();
        public PowerController Power { get; } = new();
    }
}