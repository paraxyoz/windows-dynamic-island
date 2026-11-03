using System;

namespace WindowsDynamicIsland.Models
{
    [Serializable]
    public class NoteItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        public bool IsPinned { get; set; }
        public DateTime? ReminderAt { get; set; }
        public bool ReminderCompleted { get; set; }
        public bool ReminderNotified { get; set; }

        public string DisplayTitle
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(Title)) return Title.Trim();
                if (!string.IsNullOrWhiteSpace(Content))
                {
                    string firstLine = Content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)[0].Trim();
                    return firstLine.Length > 48 ? firstLine.Substring(0, 48) + "…" : firstLine;
                }
                return "Новая заметка";
            }
        }
    }

    public class LapRecord
    {
        public int LapNumber { get; set; }
        public TimeSpan SplitTime { get; set; }
        public TimeSpan TotalTime { get; set; }

        public string FormattedSplit => FormatStopwatch(SplitTime);
        public string FormattedTotal => FormatStopwatch(TotalTime);

        public static string FormatStopwatch(TimeSpan ts)
        {
            if (ts.TotalHours >= 1)
                return string.Format("{0:00}:{1:00}:{2:00}.{3:00}", (int)ts.TotalHours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
            return string.Format("{0:00}:{1:00}.{2:00}", ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
        }
    }
}
