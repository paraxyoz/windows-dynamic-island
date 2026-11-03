using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;

namespace WindowsDynamicIsland.Services
{
    public sealed class SoundPresetInfo
    {
        public string Key { get; set; }
        public string DisplayName { get; set; }
        public string FullPath { get; set; }
    }

    public static class NotificationSoundService
    {
        private static MediaPlayer _player;

        public static IReadOnlyList<SoundPresetInfo> GetAvailablePresets()
        {
            var list = new List<SoundPresetInfo>();
            var registered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string[] searchDirs = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Sounds"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Sounds"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "уведомления")
            };

            foreach (var dir in searchDirs)
            {
                if (!Directory.Exists(dir)) continue;
                try
                {
                    foreach (var file in Directory.GetFiles(dir, "*.mp3"))
                    {
                        string name = Path.GetFileName(file);
                        if (registered.Contains(name)) continue;
                        registered.Add(name);

                        string disp = Path.GetFileNameWithoutExtension(file);
                        if (string.Equals(name, "applepay.mp3", StringComparison.OrdinalIgnoreCase)) disp = "Apple Pay";
                        else if (string.Equals(name, "plum.mp3", StringComparison.OrdinalIgnoreCase)) disp = "Plum";
                        else if (string.Equals(name, "trum.mp3", StringComparison.OrdinalIgnoreCase)) disp = "Trum";
                        else if (string.Equals(name, "phonk.mp3", StringComparison.OrdinalIgnoreCase)) disp = "Phonk";

                        list.Add(new SoundPresetInfo
                        {
                            Key = name,
                            DisplayName = disp,
                            FullPath = file
                        });
                    }
                }
                catch
                {
                }
            }

            return list;
        }

        public static void PlaySound(string presetOrFileName)
        {
            if (string.IsNullOrWhiteSpace(presetOrFileName) ||
                string.Equals(presetOrFileName, "none", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
            {
                try
                {
                    string targetFile = null;
                    if (File.Exists(presetOrFileName))
                    {
                        targetFile = presetOrFileName;
                    }
                    else
                    {
                        var presets = GetAvailablePresets();
                        foreach (var p in presets)
                        {
                            if (string.Equals(p.Key, presetOrFileName, StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(p.DisplayName, presetOrFileName, StringComparison.OrdinalIgnoreCase))
                            {
                                targetFile = p.FullPath;
                                break;
                            }
                        }
                    }

                    if (targetFile != null && File.Exists(targetFile))
                    {
                        if (_player == null)
                        {
                            _player = new MediaPlayer();
                        }
                        _player.Stop();
                        _player.Open(new Uri(targetFile, UriKind.Absolute));
                        _player.Volume = 0.85;
                        _player.Play();
                    }
                }
                catch
                {
                }
            }));
        }
    }
}
