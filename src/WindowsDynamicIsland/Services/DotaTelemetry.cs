using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Threading;

namespace WindowsDynamicIsland.Services
{
    public sealed class DotaTelemetry : IDisposable
    {
        private const int Port = 40971;
        private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
        private TcpListener _listener;
        private string _token;
        private string _summary;
        private DateTime _received;

        public bool IsListening => _listener != null;
        public string Summary => DateTime.UtcNow - _received < TimeSpan.FromSeconds(35) ? _summary : null;

        public void Start(string token)
        {
            if (_listener != null) return;
            if (!Guid.TryParseExact(token, "N", out _)) throw new ArgumentException(nameof(token));
            var listener = new TcpListener(IPAddress.Loopback, Port);
            listener.Start(4);
            _token = token;
            _listener = listener;
            _ = Task.Run(() => ReceiveAsync(listener, token));
        }

        public string Config => !IsListening ? null :
            "\"Dynamic Island\"\n{\n" +
            $"  \"uri\" \"http://127.0.0.1:{Port}/dota\"\n" +
            "  \"timeout\" \"4.0\"\n  \"buffer\" \"0.2\"\n  \"throttle\" \"1.0\"\n  \"heartbeat\" \"15.0\"\n" +
            $"  \"auth\" {{ \"token\" \"{_token}\" }}\n" +
            "  \"data\" { \"map\" \"1\" \"player\" \"1\" \"hero\" \"1\" }\n}\n";

        private async Task ReceiveAsync(TcpListener listener, string token)
        {
            while (true)
            {
                TcpClient client;
                try { client = await listener.AcceptTcpClientAsync().ConfigureAwait(false); }
                catch (ObjectDisposedException) { return; }
                catch (SocketException) { return; }
                using (client)
                {
                    client.ReceiveTimeout = 2000;
                    client.SendTimeout = 1000;
                    try
                    {
                        using (var stream = client.GetStream())
                        {
                            var body = ReadRequest(stream);
                            bool valid = TrySummarize(body, token, out string summary);
                            var reply = Encoding.ASCII.GetBytes(valid
                                ? "HTTP/1.1 200 OK\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"
                                : "HTTP/1.1 400 Bad Request\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
                            stream.Write(reply, 0, reply.Length);
                            if (valid)
                                _ = _dispatcher.BeginInvoke(new Action(() =>
                                {
                                    if (_listener != listener) return;
                                    _summary = summary;
                                    _received = DateTime.UtcNow;
                                }));
                        }
                    }
                    catch (IOException) { }
                    catch (SocketException) { }
                }
            }
        }

        private static string ReadRequest(Stream stream)
        {
            var headers = new StringBuilder();
            var timeout = Stopwatch.StartNew();
            int marker = 0;
            for (int i = 0; i < 8192 && timeout.ElapsedMilliseconds < 4000; i++)
            {
                int value = stream.ReadByte();
                if (value < 0) return null;
                headers.Append((char)value);
                marker = (marker << 8) | value;
                if (marker != 0x0d0a0d0a) continue;
                string[] lines = headers.ToString().Split(new[] { "\r\n" }, StringSplitOptions.None);
                if (!lines[0].StartsWith("POST /dota HTTP/1.", StringComparison.Ordinal)) return null;
                int size = -1;
                foreach (string line in lines)
                    if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase) &&
                        !int.TryParse(line.Substring(15).Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out size))
                        return null;
                if (size <= 0 || size > 65536) return null;
                foreach (string line in lines)
                    if (string.Equals(line.Trim(), "Expect: 100-continue", StringComparison.OrdinalIgnoreCase))
                    {
                        byte[] response = Encoding.ASCII.GetBytes("HTTP/1.1 100 Continue\r\n\r\n");
                        stream.Write(response, 0, response.Length);
                        break;
                    }
                var body = new byte[size];
                int offset = 0;
                while (offset < size)
                {
                    if (timeout.ElapsedMilliseconds >= 4000) return null;
                    int read = stream.Read(body, offset, size - offset);
                    if (read == 0) return null;
                    offset += read;
                }
                return Encoding.UTF8.GetString(body);
            }
            return null;
        }

        public static bool TrySummarize(string body, string token, out string summary)
        {
            summary = null;
            if (string.IsNullOrWhiteSpace(body) || string.IsNullOrEmpty(token)) return false;
            try
            {
                var root = new JavaScriptSerializer { MaxJsonLength = 65536, RecursionLimit = 32 }
                    .Deserialize<Dictionary<string, object>>(body);
                var auth = Section(root, "auth");
                if (!string.Equals(Value(auth, "token"), token, StringComparison.Ordinal)) return false;
                var map = Section(root, "map");
                var player = Section(root, "player");
                var hero = Section(root, "hero");
                string phase = Value(map, "game_state");
                if (map == null || phase == "DOTA_GAMERULES_STATE_POST_GAME") return true;
                var parts = new List<string>();
                string match = Value(map, "matchid");
                if (!string.IsNullOrEmpty(match) && match != "0") parts.Add("Матч " + match);
                string name = Value(hero, "name");
                if (!string.IsNullOrEmpty(name)) parts.Add(name.Replace("npc_dota_hero_", "").Replace('_', ' '));
                string radiant = Value(map, "radiant_score") ?? Value(map, "radiantscore");
                string dire = Value(map, "dire_score") ?? Value(map, "direscore");
                if (radiant != null && dire != null) parts.Add($"Radiant {radiant} : {dire} Dire");
                string kills = Value(player, "kills"), deaths = Value(player, "deaths"), assists = Value(player, "assists");
                if (kills != null && deaths != null && assists != null) parts.Add($"K/D/A {kills}/{deaths}/{assists}");
                if (int.TryParse(Value(map, "clock_time"), out int seconds) && seconds >= -600 && seconds <= 86400)
                    parts.Add((seconds < 0 ? "−" : "") + TimeSpan.FromSeconds(Math.Abs(seconds)).ToString(@"m\:ss"));
                summary = parts.Count == 0 ? "Dota 2 · ожидание матча" : string.Join(" · ", parts);
                if (summary.Length > 240) summary = summary.Substring(0, 240);
                return true;
            }
            catch (ArgumentException) { return false; }
            catch (InvalidOperationException) { return false; }
        }

        private static Dictionary<string, object> Section(Dictionary<string, object> root, string key) =>
            root != null && root.TryGetValue(key, out var value) ? value as Dictionary<string, object> : null;

        private static string Value(Dictionary<string, object> root, string key) =>
            root != null && root.TryGetValue(key, out var value) ? Convert.ToString(value, CultureInfo.InvariantCulture) : null;

        public void Dispose()
        {
            var listener = _listener;
            _listener = null;
            listener?.Stop();
            _summary = null;
        }
    }
}
