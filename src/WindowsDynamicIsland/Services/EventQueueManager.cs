using System;
using System.Collections.Generic;
using System.Windows.Threading;
using WindowsDynamicIsland.Models;

namespace WindowsDynamicIsland.Services
{
    public sealed class EventQueueManager
    {
        private readonly List<IslandEvent> _queue = new List<IslandEvent>();
        private readonly DispatcherTimer _activeTimer;
        private readonly SettingsManager _settings;

        public IslandEvent CurrentEvent { get; private set; }

        public event Action<IslandEvent> ActiveEventChanged;
        public event Action<SmileyMood> SmileyMoodRequested;

        public EventQueueManager(SettingsManager settings)
        {
            _settings = settings;
            _activeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _activeTimer.Tick += OnActiveTimerTick;
        }

        private DateTime _currentEventExpiresAt;

        public void Enqueue(IslandEvent ev)
        {
            if (ev == null) return;
            if (!_settings.Current.ShowSystemNotifications &&
                ev.Priority < IslandEventPriority.Critical &&
                ev.EventType != IslandEventType.PowerCountdown &&
                ev.EventType != IslandEventType.TimerFinished)
            {
                return;
            }

            bool isTransient = ev.EventType == IslandEventType.Volume ||
                               ev.EventType == IslandEventType.MediaPlayback ||
                               ev.EventType == IslandEventType.KeyboardLayout ||
                               ev.EventType == IslandEventType.CapsLock ||
                               ev.EventType == IslandEventType.PowerCountdown;

            // 1. Coalesce identical transient event types (e.g. rapid volume changes or media play/pause spam)
            if (CurrentEvent != null && CurrentEvent.EventType == ev.EventType && isTransient)
            {
                CurrentEvent = ev;
                _currentEventExpiresAt = DateTime.Now.AddMilliseconds(Math.Max(800, ev.DurationMs));
                SmileyMoodRequested?.Invoke(ev.Mood);
                ActiveEventChanged?.Invoke(CurrentEvent);
                return;
            }

            // Purge outdated transient events from waiting queue
            if (isTransient)
            {
                _queue.RemoveAll(x => x.EventType == ev.EventType);
            }

            // 2. Preempt immediately if priority is strictly higher OR if both are interactive/transient actions
            // (e.g. user changes track, adjusts volume, or pauses music while another non-critical banner was playing)
            if (CurrentEvent != null &&
                (ev.Priority > CurrentEvent.Priority ||
                 (CurrentEvent.Priority < IslandEventPriority.Critical && (isTransient || ev.Priority >= CurrentEvent.Priority))))
            {
                ShowImmediate(ev);
                return;
            }

            if (CurrentEvent == null)
            {
                ShowImmediate(ev);
            }
            else
            {
                if (_queue.Count < 8)
                {
                    int nextLowerPriority = _queue.FindIndex(item => item.Priority < ev.Priority);
                    if (nextLowerPriority < 0) _queue.Add(ev);
                    else _queue.Insert(nextLowerPriority, ev);
                }
            }
        }

        public void DismissCurrent()
        {
            if (CurrentEvent == null) return;
            CurrentEvent = null;
            if (_queue.Count > 0)
            {
                var next = _queue[0];
                _queue.RemoveAt(0);
                ShowImmediate(next);
            }
            else
            {
                _activeTimer.Stop();
                SmileyMoodRequested?.Invoke(SmileyMood.Normal);
                ActiveEventChanged?.Invoke(null);
            }
        }

        private void ShowImmediate(IslandEvent ev)
        {
            CurrentEvent = ev;
            _currentEventExpiresAt = DateTime.Now.AddMilliseconds(Math.Max(800, ev.DurationMs));
            SmileyMoodRequested?.Invoke(ev.Mood);

            if (_settings.Current.EnableNotificationSounds &&
                (ev.EventType == IslandEventType.AiTaskFinished ||
                 ev.EventType == IslandEventType.TimerFinished ||
                 ev.EventType == IslandEventType.NoteReminder))
            {
                try
                {
                    NotificationSoundService.PlaySound(_settings.Current.NotificationSoundPreset);
                }
                catch
                {
                }
            }

            ActiveEventChanged?.Invoke(CurrentEvent);
            if (!_activeTimer.IsEnabled)
            {
                _activeTimer.Start();
            }
        }

        private void OnActiveTimerTick(object sender, EventArgs e)
        {
            if (CurrentEvent == null)
            {
                _activeTimer.Stop();
                return;
            }

            if (DateTime.Now >= _currentEventExpiresAt)
            {
                DismissCurrent();
            }
        }
    }
}
