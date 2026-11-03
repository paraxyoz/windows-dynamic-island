using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Windows.Media.Control;
using Windows.Storage.Streams;
using WindowsDynamicIsland.Models;
using WindowsDynamicIsland.Win32;

namespace WindowsDynamicIsland.Services
{
    public sealed class MediaSessionService
    {
        private readonly EventQueueManager _eventQueue;
        private GlobalSystemMediaTransportControlsSessionManager _sessionManager;
        private GlobalSystemMediaTransportControlsSession _currentSession;
        private readonly DispatcherTimer _timelineTimer;
        private readonly DispatcherTimer _fastPlaybackPollTimer;

        private string _lastTrackSignature = string.Empty;
        private bool _lastIsPlaying;
        private bool _initialized;

        public string Title { get; private set; } = "Нет активного медиа";
        public string Artist { get; private set; } = "Запустите плеер или браузер";
        public bool IsPlaying { get; private set; }
        public bool HasActiveSession => _currentSession != null;
        public ImageSource AlbumCover { get; private set; }
        public TimeSpan Position { get; private set; } = TimeSpan.Zero;
        public TimeSpan Duration { get; private set; } = TimeSpan.Zero;

        public double ProgressRatio
        {
            get
            {
                if (Duration.TotalSeconds <= 0.5) return 0.0;
                return Math.Max(0.0, Math.Min(1.0, Position.TotalSeconds / Duration.TotalSeconds));
            }
        }

        public event Action MediaStateChanged;

        public MediaSessionService(EventQueueManager eventQueue)
        {
            _eventQueue = eventQueue;
            _timelineTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timelineTimer.Tick += (s, e) => UpdateTimelineOnly();

            // Ultra-low-latency (180ms) synchronous playback state poll so musical note & play/pause respond instantaneously
            _fastPlaybackPollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
            _fastPlaybackPollTimer.Tick += (s, e) => CheckPlaybackStatusFast();
        }

        public async Task InitializeAsync()
        {
            if (_initialized) return;
            _initialized = true;
            try
            {
                _sessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                if (_sessionManager != null)
                {
                    _sessionManager.CurrentSessionChanged += OnCurrentSessionChanged;
                    await AttachToCurrentSessionAsync();
                    _fastPlaybackPollTimer.Start();
                }
            }
            catch (Exception ex)
            {
                StorageService.LogError("MediaSessionService.InitializeAsync", ex);
            }
        }

        private void CheckPlaybackStatusFast()
        {
            if (_sessionManager == null) return;
            try
            {
                GlobalSystemMediaTransportControlsSession playingSession = null;
                var sessions = _sessionManager.GetSessions();
                if (sessions != null)
                {
                    for (int i = 0; i < sessions.Count; i++)
                    {
                        var s = sessions[i];
                        if (s != null && s.GetPlaybackInfo()?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                        {
                            playingSession = s;
                            break;
                        }
                    }
                }

                var targetSession = playingSession ?? _currentSession ?? _sessionManager.GetCurrentSession();
                if (targetSession != _currentSession && targetSession != null)
                {
                    _ = AttachToSpecificSessionAsync(targetSession, emitToast: true);
                    return;
                }

                bool nowPlaying = playingSession != null ||
                                  (_currentSession?.GetPlaybackInfo()?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing);

                if (nowPlaying != IsPlaying)
                {
                    IsPlaying = nowPlaying;
                    if (IsPlaying && !_timelineTimer.IsEnabled) _timelineTimer.Start();
                    else if (!IsPlaying && _timelineTimer.IsEnabled) _timelineTimer.Stop();

                    // Immediately notify UI & Smiley so the music note appears with zero delay!
                    MediaStateChanged?.Invoke();
                    _ = RefreshSessionDataAsync(emitToastIfChanged: true);
                }
            }
            catch
            {
            }
        }

        private async void OnCurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
        {
            await AttachToCurrentSessionAsync();
        }

        private Task AttachToCurrentSessionAsync()
        {
            return AttachToSpecificSessionAsync(_sessionManager?.GetCurrentSession(), emitToast: false);
        }

        private async Task AttachToSpecificSessionAsync(GlobalSystemMediaTransportControlsSession nextSession, bool emitToast)
        {
            try
            {
                if (_currentSession != null)
                {
                    try
                    {
                        _currentSession.MediaPropertiesChanged -= OnMediaPropertiesChanged;
                        _currentSession.PlaybackInfoChanged -= OnPlaybackInfoChanged;
                        _currentSession.TimelinePropertiesChanged -= OnTimelinePropertiesChanged;
                    }
                    catch
                    {
                    }
                }

                _currentSession = nextSession;
                if (_currentSession != null)
                {
                    _currentSession.MediaPropertiesChanged += OnMediaPropertiesChanged;
                    _currentSession.PlaybackInfoChanged += OnPlaybackInfoChanged;
                    _currentSession.TimelinePropertiesChanged += OnTimelinePropertiesChanged;

                    // Immediate synchronous playback check before awaiting async metadata/thumbnail
                    bool immediatePlaying = _currentSession.GetPlaybackInfo()?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
                    if (immediatePlaying != IsPlaying)
                    {
                        Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                        {
                            IsPlaying = immediatePlaying;
                            MediaStateChanged?.Invoke();
                        }));
                    }

                    await RefreshSessionDataAsync(emitToastIfChanged: emitToast);
                }
                else
                {
                    Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                    {
                        Title = "Нет активного медиа";
                        Artist = "Запустите плеер или браузер";
                        IsPlaying = false;
                        AlbumCover = null;
                        Position = TimeSpan.Zero;
                        Duration = TimeSpan.Zero;
                        _timelineTimer.Stop();
                        MediaStateChanged?.Invoke();
                    }));
                }
            }
            catch (Exception ex)
            {
                StorageService.LogError("AttachToCurrentSessionAsync", ex);
            }
        }

        private async void OnMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args)
        {
            await RefreshSessionDataAsync(emitToastIfChanged: true);
        }

        private async void OnPlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args)
        {
            try
            {
                bool immediatePlaying = sender?.GetPlaybackInfo()?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
                Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                {
                    if (IsPlaying != immediatePlaying)
                    {
                        IsPlaying = immediatePlaying;
                        if (IsPlaying && !_timelineTimer.IsEnabled) _timelineTimer.Start();
                        else if (!IsPlaying && _timelineTimer.IsEnabled) _timelineTimer.Stop();
                        MediaStateChanged?.Invoke();
                    }
                }));
            }
            catch
            {
            }
            await RefreshSessionDataAsync(emitToastIfChanged: true);
        }

        private void OnTimelinePropertiesChanged(GlobalSystemMediaTransportControlsSession sender, TimelinePropertiesChangedEventArgs args)
        {
            Application.Current?.Dispatcher?.BeginInvoke(new Action(UpdateTimelineOnly));
        }

        public async Task RefreshSessionDataAsync(bool emitToastIfChanged)
        {
            var session = _currentSession;
            if (session == null) return;

            try
            {
                var playback = session.GetPlaybackInfo();
                bool playing = playback?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

                var props = await session.TryGetMediaPropertiesAsync();
                string newTitle = !string.IsNullOrWhiteSpace(props?.Title) ? props.Title.Trim() : "Без названия";
                string newArtist = !string.IsNullOrWhiteSpace(props?.Artist) ? props.Artist.Trim() :
                                   (!string.IsNullOrWhiteSpace(props?.AlbumTitle) ? props.AlbumTitle.Trim() : "Системное медиа");

                BitmapImage cover = null;
                if (props?.Thumbnail != null)
                {
                    cover = await ReadThumbnailAsync(props.Thumbnail);
                }

                var timeline = session.GetTimelineProperties();
                TimeSpan pos = timeline != null ? timeline.Position : TimeSpan.Zero;
                TimeSpan end = timeline != null ? timeline.EndTime : TimeSpan.Zero;

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    string sig = $"{newTitle}|{newArtist}";
                    bool trackChanged = !string.IsNullOrEmpty(_lastTrackSignature) && _lastTrackSignature != sig;
                    bool playStateChanged = _lastIsPlaying != playing;

                    Title = newTitle;
                    Artist = newArtist;
                    IsPlaying = playing;
                    if (cover != null) AlbumCover = cover;
                    Position = pos;
                    Duration = end;

                    if (IsPlaying && !_timelineTimer.IsEnabled) _timelineTimer.Start();
                    else if (!IsPlaying && _timelineTimer.IsEnabled) _timelineTimer.Stop();

                    MediaStateChanged?.Invoke();

                    if (emitToastIfChanged && (trackChanged || playStateChanged))
                    {
                        _eventQueue?.Enqueue(new IslandEvent
                        {
                            EventType = IslandEventType.MediaPlayback,
                            Priority = IslandEventPriority.Normal,
                            Title = newTitle,
                            Subtitle = playing ? $"Играет • {newArtist}" : $"Пауза • {newArtist}",
                            BadgeText = playing ? "PLAY" : "PAUSE",
                            DurationMs = 2300,
                            Mood = playing ? SmileyMood.MusicPlaying : SmileyMood.Normal
                        });
                    }

                    _lastTrackSignature = sig;
                    _lastIsPlaying = playing;
                });
            }
            catch (Exception ex)
            {
                StorageService.LogError("RefreshSessionDataAsync", ex);
            }
        }

        private void UpdateTimelineOnly()
        {
            var session = _currentSession;
            if (session == null) return;
            try
            {
                var tl = session.GetTimelineProperties();
                if (tl != null)
                {
                    Position = tl.Position;
                    Duration = tl.EndTime;
                    if (IsPlaying && tl.LastUpdatedTime != default)
                    {
                        var elapsed = DateTimeOffset.UtcNow - tl.LastUpdatedTime.ToUniversalTime();
                        if (elapsed.TotalSeconds > 0 && elapsed.TotalSeconds < 300)
                        {
                            var est = tl.Position + elapsed;
                            if (Duration.TotalSeconds > 0 && est > Duration) est = Duration;
                            Position = est;
                        }
                    }
                    MediaStateChanged?.Invoke();
                }
            }
            catch
            {
            }
        }

        private static async Task<BitmapImage> ReadThumbnailAsync(IRandomAccessStreamReference streamRef)
        {
            try
            {
                using (IRandomAccessStreamWithContentType winrtStream = await streamRef.OpenReadAsync())
                {
                    if (winrtStream == null || winrtStream.Size == 0) return null;
                    uint size = (uint)Math.Min(winrtStream.Size, 2 * 1024 * 1024);
                    byte[] bytes = new byte[size];
                    using (var reader = new DataReader(winrtStream.GetInputStreamAt(0)))
                    {
                        await reader.LoadAsync(size);
                        reader.ReadBytes(bytes);
                    }

                    var bmp = new BitmapImage();
                    using (var ms = new MemoryStream(bytes))
                    {
                        bmp.BeginInit();
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.DecodePixelWidth = 120;
                        bmp.StreamSource = ms;
                        bmp.EndInit();
                        bmp.Freeze();
                    }
                    return bmp;
                }
            }
            catch
            {
                return null;
            }
        }

        public async Task TogglePlayPauseAsync()
        {
            // Optimistic zero-latency UI & Smiley update
            IsPlaying = !IsPlaying;
            MediaStateChanged?.Invoke();

            try
            {
                if (_currentSession != null)
                {
                    await _currentSession.TryTogglePlayPauseAsync();
                    await RefreshSessionDataAsync(emitToastIfChanged: true);
                    return;
                }
            }
            catch
            {
            }

            // Fallback to hardware media key
            NativeMethods.keybd_event((byte)NativeMethods.VK_MEDIA_PLAY_PAUSE, 0, NativeMethods.KEYEVENTF_EXTENDEDKEY, UIntPtr.Zero);
            NativeMethods.keybd_event((byte)NativeMethods.VK_MEDIA_PLAY_PAUSE, 0, NativeMethods.KEYEVENTF_EXTENDEDKEY | NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        public async Task SkipNextAsync()
        {
            try
            {
                if (_currentSession != null)
                {
                    await _currentSession.TrySkipNextAsync();
                    await RefreshSessionDataAsync(emitToastIfChanged: true);
                    return;
                }
            }
            catch
            {
            }

            NativeMethods.keybd_event((byte)NativeMethods.VK_MEDIA_NEXT_TRACK, 0, NativeMethods.KEYEVENTF_EXTENDEDKEY, UIntPtr.Zero);
            NativeMethods.keybd_event((byte)NativeMethods.VK_MEDIA_NEXT_TRACK, 0, NativeMethods.KEYEVENTF_EXTENDEDKEY | NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        public async Task SkipPreviousAsync()
        {
            try
            {
                if (_currentSession != null)
                {
                    await _currentSession.TrySkipPreviousAsync();
                    await RefreshSessionDataAsync(emitToastIfChanged: true);
                    return;
                }
            }
            catch
            {
            }

            NativeMethods.keybd_event((byte)NativeMethods.VK_MEDIA_PREV_TRACK, 0, NativeMethods.KEYEVENTF_EXTENDEDKEY, UIntPtr.Zero);
            NativeMethods.keybd_event((byte)NativeMethods.VK_MEDIA_PREV_TRACK, 0, NativeMethods.KEYEVENTF_EXTENDEDKEY | NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
        }
    }
}
