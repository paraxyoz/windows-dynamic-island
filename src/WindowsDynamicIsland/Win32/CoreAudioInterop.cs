using System;
using System.Runtime.InteropServices;

namespace WindowsDynamicIsland.Win32
{
    internal enum EDataFlow
    {
        eRender = 0,
        eCapture = 1,
        eAll = 2
    }

    internal enum ERole
    {
        eConsole = 0,
        eMultimedia = 1,
        eCommunications = 2
    }

    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDeviceEnumerator
    {
        int NotImpl1();
        [PreserveSig]
        int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice ppEndpoint);
    }

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    internal class MMDeviceEnumeratorComObject
    {
    }

    [Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDevice
    {
        [PreserveSig]
        int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct AUDIO_VOLUME_NOTIFICATION_DATA
    {
        public Guid guidEventContext;
        public bool bMuted;
        public float fMasterVolume;
        public uint nChannels;
        public float afChannelVolumes;
    }

    [Guid("657804FA-D6AD-4496-8A60-352752AF4F89"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioEndpointVolumeCallback
    {
        [PreserveSig]
        int OnNotify(IntPtr pNotify);
    }

    [Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioEndpointVolume
    {
        [PreserveSig] int RegisterControlChangeNotify(IAudioEndpointVolumeCallback pNotify);
        [PreserveSig] int UnregisterControlChangeNotify(IAudioEndpointVolumeCallback pNotify);
        [PreserveSig] int GetChannelCount(out uint pnChannelCount);
        [PreserveSig] int SetMasterVolumeLevel(float fLevelDB, ref Guid pguidEventContext);
        [PreserveSig] int SetMasterVolumeLevelScalar(float fLevel, ref Guid pguidEventContext);
        [PreserveSig] int GetMasterVolumeLevel(out float pfLevelDB);
        [PreserveSig] int GetMasterVolumeLevelScalar(out float pfLevel);
        [PreserveSig] int SetChannelVolumeLevel(uint nChannel, float fLevelDB, ref Guid pguidEventContext);
        [PreserveSig] int SetChannelVolumeLevelScalar(uint nChannel, float fLevel, ref Guid pguidEventContext);
        [PreserveSig] int GetChannelVolumeLevel(uint nChannel, out float pfLevelDB);
        [PreserveSig] int GetChannelVolumeLevelScalar(uint nChannel, out float pfLevel);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, ref Guid pguidEventContext);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool pbMute);
    }

    public sealed class SystemAudioController : IAudioEndpointVolumeCallback, IDisposable
    {
        private IAudioEndpointVolume _endpointVolume;
        private readonly Guid _appContextGuid = Guid.NewGuid();
        private bool _disposed;

        public event Action<int, bool, bool> VolumeChanged; // (volumePercent, isMuted, isExternalChange)

        public SystemAudioController()
        {
            TryInitialize();
        }

        private void TryInitialize()
        {
            try
            {
                var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
                if (enumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out IMMDevice device) == 0 && device != null)
                {
                    Guid iid = typeof(IAudioEndpointVolume).GUID;
                    if (device.Activate(ref iid, 23, IntPtr.Zero, out object obj) == 0 && obj is IAudioEndpointVolume vol)
                    {
                        _endpointVolume = vol;
                        _endpointVolume.RegisterControlChangeNotify(this);
                    }
                }
            }
            catch
            {
                _endpointVolume = null;
            }
        }

        public int GetVolumePercent()
        {
            try
            {
                if (_endpointVolume == null) TryInitialize();
                if (_endpointVolume != null && _endpointVolume.GetMasterVolumeLevelScalar(out float level) == 0)
                {
                    return (int)Math.Round(Math.Max(0f, Math.Min(1f, level)) * 100f);
                }
            }
            catch
            {
            }
            return 50;
        }

        public bool GetMuted()
        {
            try
            {
                if (_endpointVolume == null) TryInitialize();
                if (_endpointVolume != null && _endpointVolume.GetMute(out bool muted) == 0)
                {
                    return muted;
                }
            }
            catch
            {
            }
            return false;
        }

        public void SetVolumePercent(double percent)
        {
            try
            {
                if (_endpointVolume == null) TryInitialize();
                if (_endpointVolume != null)
                {
                    float scalar = (float)Math.Max(0.0, Math.Min(1.0, percent / 100.0));
                    Guid ctx = _appContextGuid;
                    _endpointVolume.SetMasterVolumeLevelScalar(scalar, ref ctx);
                    if (scalar > 0.005f && GetMuted())
                    {
                        _endpointVolume.SetMute(false, ref ctx);
                    }
                }
            }
            catch
            {
            }
        }

        public void ToggleMute()
        {
            try
            {
                if (_endpointVolume == null) TryInitialize();
                if (_endpointVolume != null)
                {
                    bool currentlyMuted = GetMuted();
                    Guid ctx = _appContextGuid;
                    _endpointVolume.SetMute(!currentlyMuted, ref ctx);
                }
            }
            catch
            {
            }
        }

        int IAudioEndpointVolumeCallback.OnNotify(IntPtr pNotify)
        {
            try
            {
                if (pNotify != IntPtr.Zero)
                {
                    var data = Marshal.PtrToStructure<AUDIO_VOLUME_NOTIFICATION_DATA>(pNotify);
                    int pct = (int)Math.Round(Math.Max(0f, Math.Min(1f, data.fMasterVolume)) * 100f);
                    bool isExternal = data.guidEventContext != _appContextGuid;
                    VolumeChanged?.Invoke(pct, data.bMuted, isExternal);
                }
            }
            catch
            {
            }
            return 0;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try
            {
                _endpointVolume?.UnregisterControlChangeNotify(this);
            }
            catch
            {
            }
        }
    }
}
