using System;

namespace WindowsDynamicIsland.Models
{
    public enum IslandEventType
    {
        KeyboardLayout = 0,
        CapsLock = 1,
        Volume = 2,
        MediaPlayback = 3,
        AppNotification = 4,
        AiTaskFinished = 5,
        TimerFinished = 6,
        NoteReminder = 7,
        DeviceConnection = 8,
        PowerCountdown = 9,
        TaskProgress = 10
    }

    public enum IslandEventPriority
    {
        Low = 0,
        Normal = 1,
        High = 2,
        Critical = 3
    }

    public class IslandEvent
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public IslandEventType EventType { get; set; }
        public IslandEventPriority Priority { get; set; } = IslandEventPriority.Normal;
        public string Title { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public string BadgeText { get; set; } = string.Empty;
        public double? ProgressRatio { get; set; }
        public int DurationMs { get; set; } = 2400;
        public SmileyMood Mood { get; set; } = SmileyMood.Normal;
        public string PrimaryActionLabel { get; set; } = string.Empty;
        public Action PrimaryAction { get; set; }
        public string SecondaryActionLabel { get; set; } = string.Empty;
        public Action SecondaryAction { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
