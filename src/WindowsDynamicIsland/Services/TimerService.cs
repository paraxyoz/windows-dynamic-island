using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Threading;
using WindowsDynamicIsland.Models;

namespace WindowsDynamicIsland.Services
{
    public sealed class TimerService
    {
        private readonly EventQueueManager _eventQueue;
        private readonly DispatcherTimer _uiTimer;

        // --- Countdown Timer ---
        private TimeSpan _timerDuration = TimeSpan.FromMinutes(5);
        private TimeSpan _timerRemaining = TimeSpan.FromMinutes(5);
        private readonly Stopwatch _countdown = new Stopwatch();
        private TimeSpan _remainingAtStart;
        private bool _timerRunning;
        private bool _timerPaused;

        // --- Stopwatch ---
        private readonly Stopwatch _stopwatch = new Stopwatch();
        private readonly List<LapRecord> _laps = new List<LapRecord>();
        private TimeSpan _lastLapTotal = TimeSpan.Zero;

        public event Action TimerTick;
        public event Action StopwatchTick;

        public bool IsTimerActive => _timerRunning || _timerPaused;
        public bool IsTimerRunning => _timerRunning;
        public bool IsTimerPaused => _timerPaused;
        public TimeSpan TimerRemaining => _timerRemaining;
        public TimeSpan TimerTotalDuration => _timerDuration;

        public bool IsStopwatchRunning => _stopwatch.IsRunning;
        public TimeSpan StopwatchElapsed => _stopwatch.Elapsed;
        public IReadOnlyList<LapRecord> Laps => _laps;

        public TimerService(EventQueueManager eventQueue)
        {
            _eventQueue = eventQueue;
            _uiTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            _uiTimer.Tick += OnTick;
        }

        public string FormattedTimerRemaining
        {
            get
            {
                var ts = _timerRemaining;
                if (ts < TimeSpan.Zero) ts = TimeSpan.Zero;
                int totalSec = (int)Math.Ceiling(ts.TotalSeconds);
                int hrs = totalSec / 3600;
                int mins = (totalSec % 3600) / 60;
                int secs = totalSec % 60;
                return hrs > 0 ? $"{hrs:00}:{mins:00}:{secs:00}" : $"{mins:00}:{secs:00}";
            }
        }

        public string FormattedStopwatch => LapRecord.FormatStopwatch(_stopwatch.Elapsed);

        public static string FormatStopwatchTime(TimeSpan ts) => LapRecord.FormatStopwatch(ts);

        public void SetTimerDuration(int hours, int minutes, int seconds)
        {
            var span = new TimeSpan(Math.Max(0, hours), Math.Max(0, minutes), Math.Max(0, seconds));
            if (span.TotalSeconds < 1) span = TimeSpan.FromSeconds(10);
            _timerDuration = span;
            if (!_timerRunning && !_timerPaused)
            {
                _timerRemaining = _timerDuration;
                TimerTick?.Invoke();
            }
        }

        public void StartTimer()
        {
            if (_timerRunning) return;
            if (_timerRemaining.TotalSeconds <= 0)
            {
                _timerRemaining = _timerDuration;
            }
            _remainingAtStart = _timerRemaining;
            _countdown.Restart();
            _timerRunning = true;
            _timerPaused = false;
            EnsureLoopState();
            TimerTick?.Invoke();
        }

        public void PauseTimer()
        {
            if (!_timerRunning) return;
            _countdown.Stop();
            _timerRemaining = _remainingAtStart - _countdown.Elapsed;
            if (_timerRemaining < TimeSpan.Zero) _timerRemaining = TimeSpan.Zero;
            _timerRunning = false;
            _timerPaused = true;
            EnsureLoopState();
            TimerTick?.Invoke();
        }

        public void ResumeTimer()
        {
            if (!_timerPaused) return;
            StartTimer();
        }

        public void ResetTimer()
        {
            _countdown.Reset();
            _timerRunning = false;
            _timerPaused = false;
            _timerRemaining = _timerDuration;
            EnsureLoopState();
            TimerTick?.Invoke();
        }

        public void StartStopwatch()
        {
            _stopwatch.Start();
            EnsureLoopState();
            StopwatchTick?.Invoke();
        }

        public void PauseStopwatch()
        {
            _stopwatch.Stop();
            EnsureLoopState();
            StopwatchTick?.Invoke();
        }

        public void ResetStopwatch()
        {
            _stopwatch.Reset();
            _laps.Clear();
            _lastLapTotal = TimeSpan.Zero;
            EnsureLoopState();
            StopwatchTick?.Invoke();
        }

        public void RecordLap()
        {
            if (!_stopwatch.IsRunning && _stopwatch.Elapsed == TimeSpan.Zero) return;
            TimeSpan currentTotal = _stopwatch.Elapsed;
            TimeSpan split = currentTotal - _lastLapTotal;
            _lastLapTotal = currentTotal;

            _laps.Insert(0, new LapRecord
            {
                LapNumber = _laps.Count + 1,
                SplitTime = split,
                TotalTime = currentTotal
            });
            StopwatchTick?.Invoke();
        }

        private void EnsureLoopState()
        {
            bool needTimer = _timerRunning || _stopwatch.IsRunning;
            if (needTimer && !_uiTimer.IsEnabled)
            {
                _uiTimer.Start();
            }
            else if (!needTimer && _uiTimer.IsEnabled)
            {
                _uiTimer.Stop();
            }
        }

        private void OnTick(object sender, EventArgs e)
        {
            if (_timerRunning)
            {
                _timerRemaining = _remainingAtStart - _countdown.Elapsed;
                if (_timerRemaining <= TimeSpan.Zero)
                {
                    _countdown.Reset();
                    _timerRemaining = _timerDuration;
                    _timerRunning = false;
                    _timerPaused = false;
                    EnsureLoopState();
                    TimerTick?.Invoke();

                    _eventQueue?.Enqueue(new IslandEvent
                    {
                        EventType = IslandEventType.TimerFinished,
                        Priority = IslandEventPriority.Critical,
                        Title = "Таймер завершён ✓",
                        Subtitle = $"Время ({FormatDuration(_timerDuration)}) вышло",
                        BadgeText = "00:00",
                        DurationMs = 6000,
                        Mood = SmileyMood.TaskCompleted
                    });
                }
                else
                {
                    TimerTick?.Invoke();
                }
            }

            if (_stopwatch.IsRunning)
            {
                StopwatchTick?.Invoke();
            }
        }

        private static string FormatDuration(TimeSpan ts)
        {
            if (ts.TotalHours >= 1)
                return $"{(int)ts.TotalHours} ч {ts.Minutes} мин";
            if (ts.TotalMinutes >= 1)
                return $"{ts.Minutes} мин {ts.Seconds} сек";
            return $"{ts.Seconds} сек";
        }
    }
}
