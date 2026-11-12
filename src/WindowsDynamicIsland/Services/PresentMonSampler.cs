using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace WindowsDynamicIsland.Services
{
    public static class PresentMonSampler
    {
        public static async Task<double?> SampleAsync(int pid)
        {
            string executable = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools", "PresentMon.exe");
            if (!File.Exists(executable) || pid <= 0) return null;
            try
            {
                using (var process = Process.Start(new ProcessStartInfo(executable,
                    $"--process_id {pid} --session_name WindowsDynamicIsland-{Process.GetCurrentProcess().Id} " +
                    "--timed 3 --terminate_after_timed --terminate_on_proc_exit --no_console_stats --output_stdout --v1_metrics")
                {
                    UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
                }))
                {
                    var output = process.StandardOutput.ReadToEndAsync();
                    var errors = process.StandardError.ReadToEndAsync();
                    var reads = Task.WhenAll(output, errors);
                    if (await Task.WhenAny(reads, Task.Delay(7000)).ConfigureAwait(false) != reads)
                    {
                        if (!process.HasExited) process.Kill();
                        return null;
                    }
                    await reads.ConfigureAwait(false);
                    if (!process.WaitForExit(500) || process.ExitCode != 0) return null;
                    return ReadFps(await output.ConfigureAwait(false));
                }
            }
            catch { return null; }
        }

        public static double? ReadFps(string csv)
        {
            if (string.IsNullOrWhiteSpace(csv)) return null;
            using (var reader = new StringReader(csv))
            {
                int timeColumn = -1, chainColumn = -1;
                var chains = new Dictionary<string, List<double>>();
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    var fields = line.Split(',');
                    if (timeColumn < 0)
                    {
                        timeColumn = Array.IndexOf(fields, "MsBetweenPresents");
                        chainColumn = Array.IndexOf(fields, "SwapChainAddress");
                        continue;
                    }
                    if (chainColumn < 0 || fields.Length <= Math.Max(timeColumn, chainColumn) ||
                        !double.TryParse(fields[timeColumn], NumberStyles.Float, CultureInfo.InvariantCulture, out double ms) ||
                        ms <= 0 || ms > 2000) continue;
                    string chain = fields[chainColumn];
                    if (!chains.TryGetValue(chain, out var intervals))
                        chains.Add(chain, intervals = new List<double>());
                    intervals.Add(ms);
                }

                var samples = chains.Values.OrderByDescending(intervals => intervals.Count).FirstOrDefault();
                return samples == null || samples.Count < 3 ? (double?)null : 1000 / samples.Average();
            }
        }
    }
}
