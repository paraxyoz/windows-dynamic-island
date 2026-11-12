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
        [PreserveSig] int OpenPropertyStore(int access, [MarshalAs(UnmanagedType.Interface)] out IPropertyStore store);
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PropertyKey
    {
        public Guid Format;
        public int Id;
    }

    [StructLayout(LayoutKind.Explicit, Size = 24)]
    internal struct PropertyVariant
    {
        [FieldOffset(0)] public ushort Type;
        [FieldOffset(8)] public IntPtr Pointer;
    }

    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int GetAt(uint index, out PropertyKey key);
        [PreserveSig] int GetValue(ref PropertyKey key, out PropertyVariant value);
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

        internal void RebindOutput()
        {
            try { _endpointVolume?.UnregisterControlChangeNotify(this); }
            catch (COMException) { }
            if (_endpointVolume != null) Marshal.ReleaseComObject(_endpointVolume);
            _endpointVolume = null;
            if (!_disposed) TryInitialize();
        }

        internal static (string Id, bool? Muted, string Name) ReadEndpoint(EDataFlow flow)
        {
            IMMDeviceEnumerator enumerator = null;
            IMMDevice endpoint = null;
            object volume = null;
            try
            {
                enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
                if (enumerator.GetDefaultAudioEndpoint(flow, ERole.eMultimedia, out endpoint) != 0 || endpoint == null)
                    return (null, null, null);
                if (endpoint.GetId(out string id) != 0) return (null, null, null);
                if (flow == EDataFlow.eRender) return (id, null, ReadDeviceName(endpoint));
                var guid = typeof(IAudioEndpointVolume).GUID;
                if (endpoint.Activate(ref guid, 23, IntPtr.Zero, out volume) == 0 &&
                    volume is IAudioEndpointVolume level && level.GetMute(out bool muted) == 0)
                    return (id, muted, null);
                return (id, null, ReadDeviceName(endpoint));
            }
            catch (COMException) { return (null, null, null); }
            finally
            {
                if (volume != null) Marshal.ReleaseComObject(volume);
                if (endpoint != null) Marshal.ReleaseComObject(endpoint);
                if (enumerator != null) Marshal.ReleaseComObject(enumerator);
            }
        }

        private static string ReadDeviceName(IMMDevice endpoint)
        {
            if (endpoint.OpenPropertyStore(0, out IPropertyStore store) != 0 || store == null) return null;
            try
            {
                var key = new PropertyKey { Format = new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), Id = 14 };
                if (store.GetValue(ref key, out PropertyVariant value) != 0) return null;
                try { return value.Type == 31 ? Marshal.PtrToStringUni(value.Pointer) : null; }
                finally { PropVariantClear(ref value); }
            }
            finally { Marshal.ReleaseComObject(store); }
        }

        [DllImport("ole32.dll")]
        private static extern int PropVariantClear(ref PropertyVariant value);

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
