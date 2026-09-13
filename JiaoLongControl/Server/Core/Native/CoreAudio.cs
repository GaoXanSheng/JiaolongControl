using System.Runtime.InteropServices;

namespace JiaoLongControl.Server.Core.Native
{
    /// <summary>
    /// Core Audio (MMDevice API) 最小互操作：读取/设置默认播放设备的系统音量与静音，供 OSD 音量指示使用。
    /// COM 接口按 vtable 顺序完整声明前缀方法，未使用的尾部方法省略。
    /// </summary>
    internal static class CoreAudio
    {
        private static readonly object Lock = new();
        private static IAudioEndpointVolume? _endpoint;

        /// <summary>获取系统音量 (0~1) 与静音状态；失败抛出异常由调用方处理。</summary>
        public static VolumeState GetVolumeState()
        {
            lock (Lock)
            {
                var ep = GetEndpoint();
                ep.GetMasterVolumeLevelScalar(out var volume);
                ep.GetMute(out var muted);
                return new VolumeState(volume, muted);
            }
        }

        public static void SetVolume(float volume)
        {
            lock (Lock)
            {
                GetEndpoint().SetMasterVolumeLevelScalar(Math.Clamp(volume, 0f, 1f), Guid.Empty);
            }
        }

        public static void SetMute(bool muted)
        {
            lock (Lock)
            {
                GetEndpoint().SetMute(muted, Guid.Empty);
            }
        }

        private static IAudioEndpointVolume GetEndpoint()
        {
            if (_endpoint != null) return _endpoint;
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
            enumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out var device);
            // CLSCTX_INPROC_SERVER
            device.Activate(typeof(IAudioEndpointVolume).GUID, 0x1, IntPtr.Zero, out var obj);
            _endpoint = (IAudioEndpointVolume)obj;
            return _endpoint;
        }

        public readonly record struct VolumeState(float Volume, bool Muted);

        private enum EDataFlow
        {
            eRender = 0,
            eCapture,
            eAll,
        }

        private enum ERole
        {
            eConsole = 0,
            eMultimedia,
            eCommunications,
        }

        [ComImport]
        [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        private class MMDeviceEnumeratorComObject
        {}

        [ComImport]
        [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceEnumerator
        {
            [PreserveSig]
            int EnumAudioEndpoints(EDataFlow dataFlow, int dwStateMask, out IntPtr ppDevices);

            [PreserveSig]
            int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, [MarshalAs(UnmanagedType.Interface)] out IMMDevice ppEndpoint);
        }

        [ComImport]
        [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDevice
        {
            [PreserveSig]
            int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.Interface)] out object ppInterface);
        }

        [ComImport]
        [Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioEndpointVolume
        {
            [PreserveSig]
            int RegisterControlChangeCallback(IntPtr guid, IntPtr pNotify);

            [PreserveSig]
            int UnregisterControlChangeCallback(IntPtr pNotify);

            [PreserveSig]
            int GetChannelCount(out uint pnChannelCount);

            [PreserveSig]
            int SetMasterVolumeLevel(float fLevelDB, ref Guid pguidEventContext);

            [PreserveSig]
            int SetMasterVolumeLevelScalar(float fLevel, ref Guid pguidEventContext);

            [PreserveSig]
            int GetMasterVolumeLevel(out float pfLevelDB);

            [PreserveSig]
            int GetMasterVolumeLevelScalar(out float pfLevel);

            [PreserveSig]
            int SetChannelVolumeLevel(uint nChannel, float fLevelDB, ref Guid pguidEventContext);

            [PreserveSig]
            int SetChannelVolumeLevelScalar(uint nChannel, float fLevel, ref Guid pguidEventContext);

            [PreserveSig]
            int GetChannelVolumeLevel(uint nChannel, out float pfLevelDB);

            [PreserveSig]
            int GetChannelVolumeLevelScalar(uint nChannel, out float pfLevel);

            [PreserveSig]
            int SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, ref Guid pguidEventContext);

            [PreserveSig]
            int GetMute([MarshalAs(UnmanagedType.Bool)] out bool pbMute);
        }
    }
}
