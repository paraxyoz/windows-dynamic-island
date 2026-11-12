using System;
using System.Diagnostics;
using System.Reflection;
using Microsoft.Win32;
using WindowsDynamicIsland.Models;

namespace WindowsDynamicIsland.Services
{
    public sealed class SettingsManager
    {
        private const string SettingsFileName = "settings.json";
        private const string RunRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppRegistryValueName = "WindowsDynamicIsland";

        private bool _startupEnabled;

        public AppSettings Current { get; private set; }

        public event Action<AppSettings> SettingsChanged;

        public SettingsManager()
        {
            Current = StorageService.LoadJson(SettingsFileName, AppSettings.CreateDefaults);
            if (Current.SchemaVersion < 4)
            {
                if (Current.SchemaVersion < 2)
                {
                    Current.Theme = AppThemeMode.Light;
                }
                if (Current.SchemaVersion < 3)
                {
                    Current.CollapsedCornerRadius = 17.0;
                    Current.ExpandedCornerRadius = 18.0;
                    Current.ExpandDelayMs = 150;
                    Current.TopOffsetPx = 0.0;
                    Current.AutoRetractOnIdle = true;
                    Current.IdleRetractSeconds = 4;
                    Current.HideInFullscreenApps = true;
                }
                if (Current.WakeExpandDelayMs < 150)
                {
                    Current.WakeExpandDelayMs = 650;
                }
                Current.SchemaVersion = 4;
                ValidateBounds(Current);
                StorageService.SaveJson(SettingsFileName, Current);
            }
            else
            {
                ValidateBounds(Current);
            }
            _startupEnabled = ReadStartupRegistry();
            Current.AutoStartWithWindows = _startupEnabled;
        }

        public void Save()
        {
            ValidateBounds(Current);
            if (Current.AutoStartWithWindows != _startupEnabled)
            {
                SyncStartupRegistry(Current.AutoStartWithWindows);
                _startupEnabled = ReadStartupRegistry();
                Current.AutoStartWithWindows = _startupEnabled;
            }
            StorageService.SaveJson(SettingsFileName, Current);
            SettingsChanged?.Invoke(Current);
        }

        public void ResetToDefaults()
        {
            Current = AppSettings.CreateDefaults();
            Save();
        }

        private static void ValidateBounds(AppSettings s)
        {
            if (s == null) return;
            s.Opacity = Math.Max(0.55, Math.Min(1.0, s.Opacity));
            s.IslandScale = Math.Max(0.80, Math.Min(1.35, s.IslandScale));
            s.CollapsedCornerRadius = Math.Max(8.0, Math.Min(26.0, s.CollapsedCornerRadius));
            s.ExpandedCornerRadius = Math.Max(10.0, Math.Min(32.0, s.ExpandedCornerRadius));
            s.TopOffsetPx = Math.Max(0.0, Math.Min(48.0, s.TopOffsetPx));
            s.HorizontalOffsetPx = Math.Max(-600.0, Math.Min(600.0, s.HorizontalOffsetPx));
            s.ExpandDelayMs = Math.Max(0, Math.Min(1500, s.ExpandDelayMs));
            s.WakeExpandDelayMs = Math.Max(150, Math.Min(2500, s.WakeExpandDelayMs));
            s.CollapseDelayMs = Math.Max(80, Math.Min(3000, s.CollapseDelayMs));
            s.AnimationSpeedMultiplier = Math.Max(0.4, Math.Min(2.5, s.AnimationSpeedMultiplier));
            s.IdleRetractSeconds = Math.Max(2, Math.Min(30, s.IdleRetractSeconds));
            s.ClipboardMaxHistory = Math.Max(10, Math.Min(300, s.ClipboardMaxHistory));
            s.WeatherRefreshMinutes = Math.Max(5, Math.Min(120, s.WeatherRefreshMinutes));
        }

        private static bool ReadStartupRegistry()
        {
            if (StorageService.IsolatedDataDirectory != null) return false;
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunRegistryPath))
                    return key?.GetValue(AppRegistryValueName) is string path && !string.IsNullOrWhiteSpace(path);
            }
            catch (Exception ex)
            {
                StorageService.LogError(nameof(ReadStartupRegistry), ex);
                return false;
            }
        }

        public static void SyncStartupRegistry(bool enable)
        {
            if (StorageService.IsolatedDataDirectory != null) return;
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunRegistryPath, true))
                {
                    if (key == null) return;
                    if (enable)
                    {
                        string exePath = Process.GetCurrentProcess().MainModule?.FileName
                                         ?? Assembly.GetExecutingAssembly().Location;
                        key.SetValue(AppRegistryValueName, $"\"{exePath}\"");
                    }
                    else
                    {
                        if (key.GetValue(AppRegistryValueName) != null)
                        {
                            key.DeleteValue(AppRegistryValueName, false);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                StorageService.LogError("SyncStartupRegistry", ex);
            }
        }

        public static bool IsWindowsInLightTheme()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    object val = key?.GetValue("AppsUseLightTheme");
                    if (val is int intVal)
                    {
                        return intVal == 1;
                    }
                }
            }
            catch
            {
            }
            return false;
        }
    }
}
