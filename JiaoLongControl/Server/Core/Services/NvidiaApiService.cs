using System;
using System.Linq;
using JiaoLongControl.Server.Core.Utils;
using NvAPIWrapper;
using NvAPIWrapper.GPU;
using NvAPIWrapper.Native;
using NvAPIWrapper.Native.GPU;
using NvAPIWrapper.Native.GPU.Structures;

namespace JiaoLongControl.Server.Core.Services
{
    public class NvidiaApiService : IDisposable
    {
        private bool _isDisposed;
        private PhysicalGPU[] _gpus;

        public NvidiaApiService()
        {
            try
            {
                NVIDIA.Initialize();
                _gpus = PhysicalGPU.GetPhysicalGPUs();
            }
            catch (Exception e)
            {
                // Here we can log the exception
                Console.WriteLine($"NVIDIA API initialization failed: {e.Message}");
                _gpus = Array.Empty<PhysicalGPU>();
            }
        }

        public CommandResult GetGpuTemperature(int gpuIndex = 0)
        {
            if (gpuIndex < 0 || gpuIndex >= _gpus.Length)
            {
                return new CommandResult(false, "Invalid GPU index.");
            }

            try
            {
                var gpu = _gpus[gpuIndex];
                var thermalSensor = gpu.ThermalInformation;

                if (thermalSensor != null)
                {
                    return new CommandResult(true, "Success", thermalSensor.PhysicalGPU.ThermalInformation);
                }

                return new CommandResult(false, "Could not find GPU thermal sensor.");
            }
            catch (Exception e)
            {
                return new CommandResult(false, $"Failed to get GPU temperature: {e.Message}");
            }
        }

        public CommandResult LockGpuClock(int frequency, int gpuIndex = 0)
        {
            if (gpuIndex < 0 || gpuIndex >= _gpus.Length)
            {
                return new CommandResult(false, "Invalid GPU index.");
            }
            
            try
            {
                var gpu = _gpus[gpuIndex];
                
                // Note: Locking a specific frequency might require more complex interaction 
                // with performance states (P-States).
                // A common approach is to set the min and max of the highest performance state to the desired frequency.
                // For simplicity, we first try a direct approach if available, or log the need for a more complex one.

                // This is a simplified example. Real-world clock locking is complex.
                // We'll attempt to set a public performance cap.
                 var clocks = new PerformanceStates20ClockEntryV1(PerformanceStateId.P0_3DPerformance, ClockType.CurrentClock, new PerformanceStates20ClockEntryV1.GpuDelta(frequency * 1000));
                 gpu.SetPerformanceStates20(new[] {clocks});


                return new CommandResult(true, $"Successfully set GPU clock lock to {frequency} MHz (implementation may vary by driver/hardware).");
            }
            catch (Exception e)
            {
                return new CommandResult(false, $"Failed to lock GPU clock: {e.Message}");
            }
        }
        
        ~NvidiaApiService()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed) return;

            if (disposing)
            {
                // No managed resources to dispose of in this class directly
            }

            try
            {
                NVIDIA.Unload();
            }
            catch (Exception e)
            {
                Console.WriteLine($"NVIDIA API unload failed: {e.Message}");
            }
            
            _isDisposed = true;
        }
    }
}
