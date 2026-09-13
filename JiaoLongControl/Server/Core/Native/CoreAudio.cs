using System.Runtime.InteropServices;

namespace JiaoLongControl.Server.Core.Native
{
    /// <summary>
    /// Core Audio (MMDevice API) 最小互操作：读取/设置默认播放设备的系统音量与静音，供 OSD 音量指示使用。
    /// COM 接口按 vtable 顺序完整声明前缀方法，未使用的尾部方法省略。
    /// 通过 IMMNotificationClient 监听默认设备切换，切换后自动重建 endpoint，避免读写到旧设备的音量。
    /// </summary>
    internal static class CoreAudio
    {
        private static readonly object Lock = new();
        private static IAudioEndpointVolume? _endpoint;
        private static IMMDeviceEnumerator? _enumerator;
        private static DeviceNotificationClient? _notificationClient;

        /// <summary>获取系统音量 (0~1) 与静音状态；失败抛出异常由调用方处理。</summary>
        public static VolumeState GetVolumeState()
        {
            lock (Lock)
            {
                try
                {
                    return ReadState();
                }
                catch (COMException)
                {
                    // 当前 endpoint 失效（如设备被移除），重建后再试一次；仍失败则抛给调用方
                    _endpoint = null;
                    return ReadState();
                }
            }
        }

        public static void SetVolume(float volume)
        {
            lock (Lock)
            {
                try
                {
                    GetEndpoint().SetMasterVolumeLevelScalar(Math.Clamp(volume, 0f, 1f), Guid.Empty);
                }
                catch (COMException)
                {
                    _endpoint = null;
                    GetEndpoint().SetMasterVolumeLevelScalar(Math.Clamp(volume, 0f, 1f), Guid.Empty);
                }
            }
        }

        public static void SetMute(bool muted)
        {
            lock (Lock)
            {
                try
                {
                    GetEndpoint().SetMute(muted, Guid.Empty);
                }
                catch (COMException)
                {
                    _endpoint = null;
                    GetEndpoint().SetMute(muted, Guid.Empty);
                }
            }
        }

        private static VolumeState ReadState()
        {
            var ep = GetEndpoint();
            ep.GetMasterVolumeLevelScalar(out var volume);
            ep.GetMute(out var muted);
            return new VolumeState(volume, muted);
        }

        private static IAudioEndpointVolume GetEndpoint()
        {
            if (_endpoint != null) return _endpoint;

            if (_enumerator == null)
            {
                _enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
                // 静态持有，防止回调对象被 GC 后系统仍向其派发通知
                _notificationClient = new DeviceNotificationClient();
                _enumerator.RegisterEndpointNotificationCallback(_notificationClient);
            }

            _enumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out var device);
            // CLSCTX_INPROC_SERVER
            device.Activate(typeof(IAudioEndpointVolume).GUID, 0x1, IntPtr.Zero, out var obj);
            _endpoint = (IAudioEndpointVolume)obj;
            return _endpoint;
        }

        /// <summary>默认播放设备变化或可用设备增删时使缓存失效，下次读取时重新解析。</summary>
        private static void InvalidateEndpoint()
        {
            lock (Lock)
            {
                _endpoint = null;
            }
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

            // vtable 占位：GetDevice 位于 GetDefaultAudioEndpoint 与 RegisterEndpointNotificationCallback 之间
            [PreserveSig]
            int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string pwstrId, out IntPtr ppDevice);

            [PreserveSig]
            int RegisterEndpointNotificationCallback(IMMNotificationClient pClient);

            [PreserveSig]
            int UnregisterEndpointNotificationCallback(IMMNotificationClient pClient);
        }

        [ComImport]
        [Guid("7991EEC9-7E89-4D85-8390-6C703CEC60C0")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMNotificationClient
        {
            void OnDeviceStateChanged([MarshalAs(UnmanagedType.LPWStr)] string pwstrDeviceId, int dwNewState);

            void OnDeviceAdded([MarshalAs(UnmanagedType.LPWStr)] string pwstrDeviceId);

            void OnDeviceRemoved([MarshalAs(UnmanagedType.LPWStr)] string pwstrDeviceId);

            void OnDefaultDeviceChanged(EDataFlow dataFlow, ERole role, [MarshalAs(UnmanagedType.LPWStr)] string pwstrDefaultDeviceId);

            void OnPropertyValueChanged([MarshalAs(UnmanagedType.LPWStr)] string pwstrDeviceId, in PropertyKey key);
        }

        private readonly record struct PropertyKey(Guid FormatId, int PropertyId);

        [ComVisible(true)]
        private sealed class DeviceNotificationClient : IMMNotificationClient
        {
            public void OnDeviceStateChanged(string pwstrDeviceId, int dwNewState) => InvalidateEndpoint();

            public void OnDeviceAdded(string pwstrDeviceId) { }

            public void OnDeviceRemoved(string pwstrDeviceId) => InvalidateEndpoint();

            public void OnDefaultDeviceChanged(EDataFlow dataFlow, ERole role, string pwstrDefaultDeviceId)
            {
                if (dataFlow == EDataFlow.eRender)
                    InvalidateEndpoint();
            }

            public void OnPropertyValueChanged(string pwstrDeviceId, in PropertyKey key) { }
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

            // 非 PreserveSig：失败 HRESULT 自动抛 COMException，避免失效设备读回 0 值
            void SetMasterVolumeLevelScalar(float fLevel, ref Guid pguidEventContext);

            [PreserveSig]
            int GetMasterVolumeLevel(out float pfLevelDB);

            void GetMasterVolumeLevelScalar(out float pfLevel);

            [PreserveSig]
            int SetChannelVolumeLevel(uint nChannel, float fLevelDB, ref Guid pguidEventContext);

            [PreserveSig]
            int SetChannelVolumeLevelScalar(uint nChannel, float fLevel, ref Guid pguidEventContext);

            [PreserveSig]
            int GetChannelVolumeLevel(uint nChannel, out float pfLevelDB);

            [PreserveSig]
            int GetChannelVolumeLevelScalar(uint nChannel, out float pfLevel);

            void SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, ref Guid pguidEventContext);

            void GetMute([MarshalAs(UnmanagedType.Bool)] out bool pbMute);
        }
    }
}
