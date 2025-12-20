using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using JiaoLongControl.Server.Core.Controllers;
using JiaoLongControl.Server.Core.Services;

namespace JiaoLongControl.Server.Interop
{
    [System.Runtime.InteropServices.ComVisible(true)]
    public class Bridge
    {
        public CpuController CPU { get; } = new();
        public FanController Fan { get; } = new();
        public GpuController GPU { get; } = new();
        public LogoLightController LogoLight { get; } = new();
        public KeyboardController Keyboard { get; } = new();
        public PerformanceModeController PerformanceMode { get; } = new();
        public HardwareController Hardware { get; } = new();
        public ConfigController Config { get; } = new();
        public AutoStartController AutoStart { get; } = new();
        public AutoFanControl AutoFan { get; } = new();
    }
}