using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Windows.Foundation;
using Windows.Media.Control;
using Windows.Storage.Streams;
using WindowsDynamicIsland.Models;
using WindowsDynamicIsland.Win32;
using MediaSession = Windows.Media.Control.GlobalSystemMediaTransportControlsSession;
using SessionManager = Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager;

namespace WindowsDynamicIsland.Services
{
    public sealed class MediaSessionService : IDisposable
    {
        private readonly EventQueueManager _eventQueue;
        private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
        private readonly DispatcherTimer _timelineTimer;
        private SessionManager _manager;
        private MediaSession _session;
        private int _metadataRequest;
        private bool _disposed;
        private bool _hasTrack;
        private bool _seeking;
        private TimeSpan _start;
        private TimeSpan _seekMin;
        private TimeSpan _seekMax;
        private DateTimeOffset _seekTime;
        private DateTimeOffset _previousTimelineUpdate;
        private TimeSpan _seekTarget;

        public string Title { get; private set; } = "Нет активного медиа";
        public string Artist { get; private set; } = "Запустите плеер или браузер";
        public string TrackArtist { get; private set; }
        public string Album { get; private set; }
        public int TrackVersion { get; private set; }
        public bool IsPlaying { get; private set; }
        public bool HasActiveSession => _session != null;
        public bool CanSeek { get; private set; }
        public ImageSource AlbumCover { get; private set; }
        public TimeSpan Position { get; private set; }
        public TimeSpan Duration { get; private set; }

        public event Action MediaStateChanged;
        public event Action TimelineChanged;

        public MediaSessionService(EventQueueManager eventQueue)
        {
            _eventQueue = eventQueue;
            _timelineTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
            _timelineTimer.Tick += (s, e) => UpdateTimeline();
        }

        public async Task InitializeAsync()
        {
            try
            {
                var manager = await SessionManager.RequestAsync();
                if (_disposed || _manager != null || manager == null) return;
                _manager = manager;
                _manager.CurrentSessionChanged += OnCurrentSessionChanged;
                _manager.SessionsChanged += OnSessionsChanged;
                await AttachCurrentAsync();
            }
            catch (Exception ex)
            {
                StorageService.LogError(nameof(InitializeAsync), ex);
            }
        }

        private void OnCurrentSessionChanged(SessionManager sender, CurrentSessionChangedEventArgs args)
        {
            _dispatcher.InvokeAsync(AttachCurrentAsync);
        }

        private void OnSessionsChanged(SessionManager sender, SessionsChangedEventArgs args)
        {
            _dispatcher.InvokeAsync(AttachCurrentAsync);
        }

        private async Task AttachCurrentAsync()
        {
            if (_disposed) return;
            try
            {
                var next = _manager.GetCurrentSession();
                if (next == _session) return;
                DetachSession();
                _session = next;
                _timelineTimer.Stop();
                _metadataRequest++;
                TrackVersion++;
                _hasTrack = false;
                _seeking = false;
                _seekTime = default;
                AlbumCover = null;
                TrackArtist = Album = null;
                Title = next == null ? "Нет активного медиа" : "Загрузка трека…";
                Artist = next == null ? "Запустите плеер или браузер" : string.Empty;
                IsPlaying = CanSeek = false;
                Position = Duration = TimeSpan.Zero;

                if (next != null)
                {
                    next.MediaPropertiesChanged += OnMediaPropertiesChanged;
                    next.PlaybackInfoChanged += OnPlaybackInfoChanged;
                    next.TimelinePropertiesChanged += OnTimelinePropertiesChanged;
                    UpdatePlayback(next);
                }

                MediaStateChanged?.Invoke();
                TimelineChanged?.Invoke();
                if (next != null) await RefreshTrackAsync(next);
            }
            catch (Exception ex)
            {
                StorageService.LogError(nameof(AttachCurrentAsync), ex);
            }
        }

        private void DetachSession()
        {
            if (_session == null) return;
            _session.MediaPropertiesChanged -= OnMediaPropertiesChanged;
            _session.PlaybackInfoChanged -= OnPlaybackInfoChanged;
            _session.TimelinePropertiesChanged -= OnTimelinePropertiesChanged;
            _session = null;
        }

        private void OnMediaPropertiesChanged(MediaSession sender, MediaPropertiesChangedEventArgs args)
        {
            _dispatcher.InvokeAsync(() => RefreshTrackAsync(sender));
        }

        private void OnPlaybackInfoChanged(MediaSession sender, PlaybackInfoChangedEventArgs args)
        {
            _dispatcher.InvokeAsync(() => UpdatePlayback(sender));
        }

        private void OnTimelinePropertiesChanged(MediaSession sender, TimelinePropertiesChangedEventArgs args)
        {
            _dispatcher.InvokeAsync(() =>
            {
                if (sender == _session) UpdateTimeline();
            });
        }

        private void UpdatePlayback(MediaSession session)
        {
            if (session == null || session != _session) return;
            try
            {
                var playback = session.GetPlaybackInfo();
                bool wasPlaying = IsPlaying;
                IsPlaying = playback?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
                UpdateTimeline();
                if (wasPlaying == IsPlaying) return;
                MediaStateChanged?.Invoke();
                if (_hasTrack) ShowMediaToast();
            }
            catch (Exception ex)
            {
                StorageService.LogError(nameof(UpdatePlayback), ex);
            }
        }

        private async Task RefreshTrackAsync(MediaSession session)
        {
            if (session != _session) return;
            int request = ++_metadataRequest;
            try
            {
                var props = await session.TryGetMediaPropertiesAsync();
                if (request != _metadataRequest || session != _session) return;

                string title = props?.Title?.Trim();
                string artist = props?.Artist?.Trim();
                string album = props?.AlbumTitle?.Trim();
                bool changed = !_hasTrack || title != Title || artist != TrackArtist || album != Album;
                if (changed)
                {
                    bool showToast = _hasTrack;
                    _hasTrack = !string.IsNullOrWhiteSpace(title);
                    TrackVersion++;
                    _seeking = false;
                    _seekTime = default;
                    Title = _hasTrack ? title : "Без названия";
                    TrackArtist = artist;
                    Album = album;
                    Artist = !string.IsNullOrWhiteSpace(artist) ? artist :
                        (!string.IsNullOrWhiteSpace(album) ? album : "Системное медиа");
                    AlbumCover = null;
                    UpdateTimeline();
                    MediaStateChanged?.Invoke();
                    if (showToast && _hasTrack) ShowMediaToast();
                }

                var cover = props?.Thumbnail == null ? null : await ReadThumbnailAsync(props.Thumbnail);
                if (request != _metadataRequest || session != _session) return;
                AlbumCover = cover;
                MediaStateChanged?.Invoke();
            }
            catch (Exception ex)
            {
                if (request == _metadataRequest)
                    StorageService.LogError(nameof(RefreshTrackAsync), ex);
            }
        }

        private void ShowMediaToast()
        {
            _eventQueue?.Enqueue(new IslandEvent
            {
                EventType = IslandEventType.MediaPlayback,
                Priority = IslandEventPriority.Normal,
                Title = Title,
                Subtitle = IsPlaying ? $"Играет • {Artist}" : $"Пауза • {Artist}",
                BadgeText = IsPlaying ? "PLAY" : "PAUSE",
                DurationMs = 2300,
                Mood = IsPlaying ? SmileyMood.MusicPlaying : SmileyMood.Normal
            });
        }

        private void UpdateTimeline()
        {
            var session = _session;
            if (session == null) return;
            try
            {
                var timeline = session.GetTimelineProperties();
                var playback = session.GetPlaybackInfo();
                _start = timeline?.StartTime ?? TimeSpan.Zero;
                Duration = (timeline?.EndTime ?? _start) - _start;
                if (Duration < TimeSpan.Zero) Duration = TimeSpan.Zero;

                _seekMin = Clamp(timeline?.MinSeekTime ?? _start, _start, _start + Duration);
                var end = _start + Duration;
                var reportedMax = timeline?.MaxSeekTime ?? _start;
                _seekMax = reportedMax <= _start && _seekMin == _start ? end : Clamp(reportedMax, _seekMin, end);
                CanSeek = _hasTrack && !_seeking && _seekMax > _seekMin &&
                    playback?.Controls?.IsPlaybackPositionEnabled == true;

                var now = DateTimeOffset.UtcNow;
                double rate = IsPlaying ? playback?.PlaybackRate ?? 1.0 : 0;
                if (double.IsNaN(rate) || double.IsInfinity(rate)) rate = 0;
                var lastUpdated = timeline?.LastUpdatedTime ?? default;
                var anchor = timeline?.Position ?? _start;
                double elapsed = lastUpdated == default ? 0 : Math.Max(0, (now - lastUpdated).TotalSeconds);

                // Some players acknowledge a seek before publishing their new timeline.
                if (_seekTime != default && lastUpdated <= _previousTimelineUpdate &&
                    (now - _seekTime).TotalSeconds < 2)
                {
                    anchor = _seekTarget;
                    elapsed = Math.Max(0, (now - _seekTime).TotalSeconds);
                }
                else
                {
                    _seekTime = default;
                }

                var seconds = (anchor - _start).TotalSeconds + elapsed * rate;
                Position = TimeSpan.FromSeconds(Math.Max(0, Math.Min(Duration.TotalSeconds, seconds)));
                if (IsPlaying || _seekTime != default) _timelineTimer.Start();
                else _timelineTimer.Stop();
                TimelineChanged?.Invoke();
            }
            catch (Exception ex)
            {
                _timelineTimer.Stop();
                CanSeek = false;
                TimelineChanged?.Invoke();
                StorageService.LogError(nameof(UpdateTimeline), ex);
            }
        }

        public async Task<bool> SeekAsync(TimeSpan position, int trackVersion)
        {
            var session = _session;
            if (!CanSeek || session == null || trackVersion != TrackVersion) return false;
            _seeking = true;
            UpdateTimeline();
            try
            {
                var target = Clamp(_start + Clamp(position, TimeSpan.Zero, Duration), _seekMin, _seekMax);
                var previousUpdate = session.GetTimelineProperties().LastUpdatedTime;
                bool accepted = await session.TryChangePlaybackPositionAsync(target.Ticks);
                if (session != _session || trackVersion != TrackVersion || !accepted) return false;
                _seekTarget = target;
                _seekTime = DateTimeOffset.UtcNow;
                _previousTimelineUpdate = previousUpdate;
                return true;
            }
            catch (Exception ex)
            {
                StorageService.LogError(nameof(SeekAsync), ex);
                return false;
            }
            finally
            {
                if (session == _session && trackVersion == TrackVersion)
                {
                    _seeking = false;
                    UpdateTimeline();
                }
            }
        }

        private static TimeSpan Clamp(TimeSpan value, TimeSpan minimum, TimeSpan maximum)
        {
            return value < minimum ? minimum : value > maximum ? maximum : value;
        }

        private static async Task<BitmapImage> ReadThumbnailAsync(IRandomAccessStreamReference streamRef)
        {
            try
            {
                using (var stream = await streamRef.OpenReadAsync())
                {
                    if (stream == null || stream.Size == 0 || stream.Size > 8 * 1024 * 1024) return null;
                    uint size = (uint)stream.Size;
                    var bytes = new byte[size];
                    using (var reader = new DataReader(stream.GetInputStreamAt(0)))
                    {
                        if (await reader.LoadAsync(size) != size) return null;
                        reader.ReadBytes(bytes);
                    }

                    var bitmap = new BitmapImage();
                    using (var buffer = new MemoryStream(bytes))
                    {
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.DecodePixelWidth = 256;
                        bitmap.StreamSource = buffer;
                        bitmap.EndInit();
                        bitmap.Freeze();
                    }
                    return bitmap;
                }
            }
            catch
            {
                return null;
            }
        }

        public Task TogglePlayPauseAsync() => RunCommandAsync(
            session => session.TryTogglePlayPauseAsync(), NativeMethods.VK_MEDIA_PLAY_PAUSE);

        public Task SkipNextAsync() => RunCommandAsync(
            session => session.TrySkipNextAsync(), NativeMethods.VK_MEDIA_NEXT_TRACK);

        public Task SkipPreviousAsync() => RunCommandAsync(
            session => session.TrySkipPreviousAsync(), NativeMethods.VK_MEDIA_PREV_TRACK);

        private async Task RunCommandAsync(Func<MediaSession, IAsyncOperation<bool>> command, int fallbackKey)
        {
            var session = _session;
            try
            {
                if (session != null && await command(session))
                {
                    UpdatePlayback(session);
                    return;
                }
            }
            catch (Exception ex)
            {
                StorageService.LogError(nameof(RunCommandAsync), ex);
            }

            if (_disposed || session != _session) return;
            NativeMethods.keybd_event((byte)fallbackKey, 0, NativeMethods.KEYEVENTF_EXTENDEDKEY, UIntPtr.Zero);
            NativeMethods.keybd_event((byte)fallbackKey, 0,
                NativeMethods.KEYEVENTF_EXTENDEDKEY | NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        public void Dispose()
        {
            _disposed = true;
            _timelineTimer.Stop();
            if (_manager != null)
            {
                _manager.CurrentSessionChanged -= OnCurrentSessionChanged;
                _manager.SessionsChanged -= OnSessionsChanged;
            }
            DetachSession();
        }
    }
}
