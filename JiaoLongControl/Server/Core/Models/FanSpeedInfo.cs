using System.Runtime.InteropServices;

namespace JiaoLongControl.Server.Core.Models
{
    [ComVisible(true)]
    public class FanSpeedInfo
    {
        /// <summary>转速读数的合理上限, 超过视为无效读数 (调用方按 0 处理)。</summary>
        public const int ValidMaxRpm = 10000;

        public int CPUFanSpeed { get; set; }
        public int GPUFanSpeed { get; set; }
    }
}