using System;

namespace WindowsDynamicIsland.Models
{
    [Serializable]
    public class ClipboardEntry
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Text { get; set; } = string.Empty;
        public DateTime CopiedAt { get; set; } = DateTime.Now;
        public bool IsPinned { get; set; }
        public string SourceApp { get; set; } = string.Empty;

        public string SingleLinePreview
        {
            get
            {
                if (string.IsNullOrEmpty(Text)) return string.Empty;
                string normalized = Text.Replace("\r\n", " ↵ ").Replace("\n", " ↵ ").Replace("\t", " ");
                return normalized.Length > 140 ? normalized.Substring(0, 140) + "…" : normalized;
            }
        }

        public int LineCount
        {
            get
            {
                if (string.IsNullOrEmpty(Text)) return 0;
                int count = 1;
                for (int i = 0; i < Text.Length; i++)
                {
                    if (Text[i] == '\n') count++;
                }
                return count;
            }
        }

        public string FormattedTime
        {
            get
            {
                var diff = DateTime.Now - CopiedAt;
                if (diff.TotalSeconds < 60) return "только что";
                if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes} мин назад";
                if (CopiedAt.Date == DateTime.Today) return CopiedAt.ToString("HH:mm");
                return CopiedAt.ToString("dd.MM HH:mm");
            }
        }
    }
}
