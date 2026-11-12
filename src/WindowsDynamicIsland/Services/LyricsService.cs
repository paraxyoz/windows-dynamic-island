using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace WindowsDynamicIsland.Services
{
    public sealed class LyricLine
    {
        public TimeSpan Time { get; }
        public string Text { get; }

        public LyricLine(TimeSpan time, string text)
        {
            Time = time;
            Text = text;
        }
    }

    public sealed class TrackLyrics
    {
        public IReadOnlyList<LyricLine> Lines { get; }
        public string PlainText { get; }
        public bool Instrumental { get; }

        public TrackLyrics(IReadOnlyList<LyricLine> lines, string plainText, bool instrumental)
        {
            Lines = lines;
            PlainText = plainText;
            Instrumental = instrumental;
        }

        public int ActiveLine(TimeSpan position)
        {
            int first = 0;
            int end = Lines.Count;
            while (first < end)
            {
                int middle = first + (end - first) / 2;
                if (Lines[middle].Time <= position) first = middle + 1;
                else end = middle;
            }
            return first - 1;
        }
    }

    public static class LyricsService
    {
        private static readonly HttpClient Client = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
        private static readonly Regex Timestamp = new Regex(@"\G\[([0-9]{1,4}):([0-5][0-9])(?:[.:]([0-9]{1,3}))?\]");
        private static readonly Regex Offset = new Regex(@"^\s*\[offset:\s*([+-]?\d+)\s*\]", RegexOptions.IgnoreCase | RegexOptions.Multiline);

        public static async Task<TrackLyrics> FetchAsync(
            string title, string artist, string album, TimeSpan duration, CancellationToken cancellationToken)
        {
            string url = "https://lrclib.net/api/get?track_name=" + Uri.EscapeDataString(title)
                + "&artist_name=" + Uri.EscapeDataString(artist);
            if (!string.IsNullOrWhiteSpace(album)) url += "&album_name=" + Uri.EscapeDataString(album);
            int seconds = (int)Math.Round(duration.TotalSeconds);
            if (seconds >= 1 && seconds <= 3600) url += "&duration=" + seconds.ToString(CultureInfo.InvariantCulture);

            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                request.Headers.UserAgent.ParseAdd("WindowsDynamicIsland/1.0 (https://github.com/paraxyoz/windows-dynamic-island)");
                using (var response = await Client.SendAsync(request, cancellationToken).ConfigureAwait(false))
                {
                    if (response.StatusCode == HttpStatusCode.NotFound)
                        return new TrackLyrics(Array.Empty<LyricLine>(), null, false);
                    response.EnsureSuccessStatusCode();
                    string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var data = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
                    data.TryGetValue("syncedLyrics", out var synced);
                    data.TryGetValue("plainLyrics", out var plain);
                    data.TryGetValue("instrumental", out var instrumental);
                    return new TrackLyrics(ParseLrc(synced as string), plain as string, instrumental is bool value && value);
                }
            }
        }

        public static IReadOnlyList<LyricLine> ParseLrc(string lrc)
        {
            if (string.IsNullOrWhiteSpace(lrc)) return Array.Empty<LyricLine>();
            int.TryParse(Offset.Match(lrc).Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int offset);

            var lines = new List<LyricLine>();
            foreach (string raw in lrc.Split('\n'))
            {
                string row = raw.TrimStart('\uFEFF', ' ', '\t');
                var tags = Timestamp.Matches(row);
                if (tags.Count == 0) continue;
                var last = tags[tags.Count - 1];
                string text = row.Substring(last.Index + last.Length).Trim();
                foreach (Match tag in tags)
                {
                    int minutes = int.Parse(tag.Groups[1].Value, CultureInfo.InvariantCulture);
                    int seconds = int.Parse(tag.Groups[2].Value, CultureInfo.InvariantCulture);
                    int fraction = int.Parse(tag.Groups[3].Value.PadRight(3, '0'), CultureInfo.InvariantCulture);
                    var time = TimeSpan.FromMilliseconds(minutes * 60000L + seconds * 1000L + fraction - offset);
                    lines.Add(new LyricLine(time, text));
                }
            }

            return lines.GroupBy(line => line.Time)
                .OrderBy(group => group.Key)
                .Select(group => new LyricLine(group.Key, string.Join("\n", group.Select(line => line.Text)
                    .Where(text => text.Length > 0).Distinct())))
                .ToArray();
        }
    }
}
