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
    public sealed class LyricWord
    {
        public string Text { get; }
        public TimeSpan Start { get; }
        public TimeSpan End { get; }

        public LyricWord(string text, int start, int end)
        {
            Text = text;
            Start = TimeSpan.FromMilliseconds(start);
            End = TimeSpan.FromMilliseconds(end);
        }
    }

    public sealed class LyricLine
    {
        public TimeSpan Time { get; }
        public string Text { get; }

        public IReadOnlyList<LyricWord> Words { get; }

        public LyricLine(TimeSpan time, string text, IReadOnlyList<LyricWord> words = null)
        {
            Time = time;
            Text = text;
            Words = words ?? Array.Empty<LyricWord>();
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

        private static readonly Regex ReleaseSuffix = new Regex(
            @"\s*[\[(](?:official\s+(?:music\s+)?video|official\s+audio|lyrics?|visuali[sz]er|remastered(?:\s+\d{4})?|\d{4}\s+remaster)[\])]\s*$",
            RegexOptions.IgnoreCase);
        private static readonly Regex ArtistSuffix = new Regex(@"\s+-\s+Topic$", RegexOptions.IgnoreCase);
        private const string Api = "https://lrclib.net/api/";

        public static bool CanLookup(string title, string artist) =>
            !string.IsNullOrWhiteSpace(artist) || (title?.IndexOf(" - ", StringComparison.Ordinal) ?? -1) > 0;

        public static async Task<TrackLyrics> FetchAsync(
            string title, string artist, string album, TimeSpan duration, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(artist))
            {
                int split = title.IndexOf(" - ", StringComparison.Ordinal);
                if (split < 1) return ReadTrack(null);
                artist = title.Substring(0, split).Trim();
                title = title.Substring(split + 3).Trim();
            }

            string signature = "track_name=" + Uri.EscapeDataString(title) + "&artist_name=" + Uri.EscapeDataString(artist);
            string exact = Api + "get?" + signature;
            if (!string.IsNullOrWhiteSpace(album)) exact += "&album_name=" + Uri.EscapeDataString(album);
            int seconds = (int)Math.Round(duration.TotalSeconds);
            if (seconds >= 1 && seconds <= 3600) exact += "&duration=" + seconds.ToString(CultureInfo.InvariantCulture);
            var record = await ReadJsonAsync<Dictionary<string, object>>(exact, cancellationToken).ConfigureAwait(false);
            var original = ReadTrack(record);
            if (original.Lines.Count > 0 || original.Instrumental) return original;

            string cleanTitle = ReleaseSuffix.Replace(title, "").Trim();
            string cleanArtist = ArtistSuffix.Replace(artist, "").Trim();
            if (cleanTitle.Length == 0 || cleanArtist.Length == 0) return original;
            string search = Api + "search?track_name=" + Uri.EscapeDataString(cleanTitle)
                + "&artist_name=" + Uri.EscapeDataString(cleanArtist);
            await Task.Delay(250, cancellationToken).ConfigureAwait(false);
            var candidates = await ReadJsonAsync<Dictionary<string, object>[]>(search, cancellationToken).ConfigureAwait(false);
            var match = (candidates ?? Array.Empty<Dictionary<string, object>>())
                .Where(item => SameName(cleanTitle, Value(item, "trackName")) && SameName(cleanArtist, Value(item, "artistName")))
                .Select(item => new
                {
                    item,
                    difference = seconds > 0 && double.TryParse(Convert.ToString(item.TryGetValue("duration", out var d) ? d : null,
                        CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out double length)
                        ? Math.Abs(length - seconds) : seconds > 0 ? double.MaxValue : 0,
                    timed = !string.IsNullOrWhiteSpace(Value(item, "syncedLyrics")) || Flag(item, "hasWordSync")
                })
                .Where(candidate => candidate.difference <= 8)
                .OrderByDescending(candidate => candidate.timed && candidate.difference <= 4)
                .ThenBy(candidate => candidate.difference)
                .ThenByDescending(candidate => candidate.timed)
                .FirstOrDefault();
            if (match == null) return original;

            var alternative = ReadTrack(match.item);
            // A longer duration mismatch is good enough for reading, but can be a different edit.
            if (seconds == 0 || match.difference > 4)
                return new TrackLyrics(Array.Empty<LyricLine>(), original.PlainText ?? alternative.PlainText, alternative.Instrumental);
            return alternative.Lines.Count > 0 || string.IsNullOrWhiteSpace(original.PlainText) ? alternative : original;
        }

        private static bool SameName(string requested, string candidate)
        {
            if (string.IsNullOrWhiteSpace(candidate)) return false;
            string Normalize(string value) => Regex.Replace(value.Normalize(), @"[^\p{L}\p{M}\p{N}]+", "").ToUpperInvariant();
            return Normalize(requested) == Normalize(candidate);
        }

        private static string Value(Dictionary<string, object> data, string key) =>
            data != null && data.TryGetValue(key, out var value) ? value as string : null;

        private static bool Flag(Dictionary<string, object> data, string key) =>
            data != null && data.TryGetValue(key, out var value) && value is bool enabled && enabled;

        private static TrackLyrics ReadTrack(Dictionary<string, object> data)
        {
            var lines = Flag(data, "hasWordSync") ? ParseWordLyrics(Value(data, "lyricsfile")) : Array.Empty<LyricLine>();
            if (lines.Count == 0) lines = ParseLrc(Value(data, "syncedLyrics"));
            return new TrackLyrics(lines, Value(data, "plainLyrics"), Flag(data, "instrumental"));
        }

        private static async Task<T> ReadJsonAsync<T>(string url, CancellationToken cancellationToken) where T : class
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                request.Headers.UserAgent.ParseAdd("WindowsDynamicIsland/1.0 (https://github.com/paraxyoz/windows-dynamic-island)");
                using (var response = await Client.SendAsync(request, cancellationToken).ConfigureAwait(false))
                {
                    if (response.StatusCode == HttpStatusCode.NotFound) return null;
                    response.EnsureSuccessStatusCode();
                    return new JavaScriptSerializer().Deserialize<T>(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                }
            }
        }

        public static IReadOnlyList<LyricLine> ParseWordLyrics(string yaml)
        {
            if (string.IsNullOrWhiteSpace(yaml)) return Array.Empty<LyricLine>();
            var result = new List<LyricLine>();
            var words = new List<LyricWord>();
            string line = null, word = null;
            int lineIndent = -1, start = -1, wordStart = -1, wordEnd = -1;
            bool inLines = false;

            void FinishWord()
            {
                if (word != null && wordStart >= 0 && wordEnd > wordStart)
                    words.Add(new LyricWord(word, wordStart, wordEnd));
                word = null;
                wordStart = wordEnd = -1;
            }

            void FinishLine()
            {
                FinishWord();
                if (line != null && start >= 0)
                {
                    var ordered = words.OrderBy(item => item.Start).ToArray();
                    // Use exact source text if the word sequence does not cover the full line.
                    if (string.Concat(ordered.Select(item => item.Text)).Trim() != line.Trim()) ordered = Array.Empty<LyricWord>();
                    result.Add(new LyricLine(TimeSpan.FromMilliseconds(start), line, ordered));
                }
                words.Clear();
                line = null;
                start = -1;
            }

            foreach (string row in yaml.Split('\n'))
            {
                string token = row.TrimStart();
                if (!inLines)
                {
                    inLines = token.TrimEnd() == "lines:";
                    continue;
                }
                if (string.IsNullOrWhiteSpace(token) || token.StartsWith("#")) continue;
                int indent = row.Length - token.Length;
                if (indent == 0 && token[0] != '-') break;

                if (token.StartsWith("- text:", StringComparison.Ordinal))
                {
                    if (lineIndent < 0) lineIndent = indent;
                    var value = Scalar(token.Substring(7));
                    if (indent == lineIndent)
                    {
                        FinishLine();
                        line = value;
                    }
                    else
                    {
                        FinishWord();
                        word = value;
                    }
                }
                else if (token.StartsWith("start_ms:", StringComparison.Ordinal) &&
                    int.TryParse(token.Substring(9).Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int ms))
                {
                    if (word != null && indent > lineIndent + 2) wordStart = ms;
                    else start = ms;
                }
                else if (word != null && token.StartsWith("end_ms:", StringComparison.Ordinal) &&
                    int.TryParse(token.Substring(7).Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int end))
                {
                    wordEnd = end;
                }
            }
            FinishLine();
            return result.Any(lineItem => lineItem.Words.Count > 0)
                ? result.OrderBy(lineItem => lineItem.Time).ToArray() : Array.Empty<LyricLine>();
        }

        private static string Scalar(string value)
        {
            value = value.Trim();
            if (value.Length >= 2 && value[0] == '\'' && value[value.Length - 1] == '\'')
                return value.Substring(1, value.Length - 2).Replace("''", "'");
            if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
            {
                try { return new JavaScriptSerializer().Deserialize<string>(value); }
                catch (ArgumentException) { return null; }
            }
            return value.StartsWith("|") || value.StartsWith(">") ? null : value;
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
