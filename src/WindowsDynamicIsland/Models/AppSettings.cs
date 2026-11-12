using System;

namespace WindowsDynamicIsland.Models
{
    public enum AppThemeMode
    {
        Dark = 0,
        Light = 1,
        System = 2,
        Ios26Glass = 3
    }

    public enum SmileyStylePreset
    {
        Classic = 0,   // ^_^
        Minimal = 1,   // •ᴗ•
        Cyber = 2,     // [•_•]
        Cat = 3,       // =^..^=
        Kawaii = 4     // ◕‿◕
    }

    public enum SmileyMood
    {
        Normal = 0,
        MusicPlaying = 1,
        NotificationAlert = 2,
        TaskCompleted = 3,
        IdleBlink = 4,
        TimerActive = 5
    }

    public enum SmileyReactionType
    {
        WakeUp = 0,
        Expand = 1,
        Collapse = 2,
        TabChanged = 3,
        ClipboardCopied = 4,
        FileDropped = 5,
        NoteTyped = 6,
        VolumeChanged = 7,
        VolumeMuted = 8,
        LayoutSwitched = 9,
        UserClicked = 10
    }

    [Serializable]
    public class AppSettings
    {
        // --- Внешний вид (Appearance) ---
        public int SchemaVersion { get; set; } = 4;
        public AppThemeMode Theme { get; set; } = AppThemeMode.Light;
        public double Opacity { get; set; } = 0.98;
        public double IslandScale { get; set; } = 1.0;
        public double CollapsedCornerRadius { get; set; } = 17.0;
        public double ExpandedCornerRadius { get; set; } = 18.0;
        public double TopOffsetPx { get; set; } = 0.0;
        public double HorizontalOffsetPx { get; set; } = 0.0;

        // Отображаемые элементы в спокойном состоянии
        public bool ShowSmileyInCollapsed { get; set; } = true;
        public bool ShowWeatherInCollapsed { get; set; } = true;
        public bool ShowClockInCollapsed { get; set; } = true;
        public bool ShowNotesIconInCollapsed { get; set; } = true;
        public bool ShowTimerInCollapsed { get; set; } = true;
        public bool EnableSmileyAnimation { get; set; } = true;

        // --- Поведение (Behavior) ---
        public bool ExpandOnHover { get; set; } = true;
        public int ExpandDelayMs { get; set; } = 150;
        public int WakeExpandDelayMs { get; set; } = 650;
        public int CollapseDelayMs { get; set; } = 260;
        public double AnimationSpeedMultiplier { get; set; } = 1.0;
        public bool AutoRetractOnIdle { get; set; } = true;
        public int IdleRetractSeconds { get; set; } = 4;
        public bool ShowSystemNotifications { get; set; } = true;
        public bool WindowsNotificationListenerEnabled { get; set; }
        public string DotaTelemetryToken { get; set; }
        public bool AutoStartWithWindows { get; set; } = false;
        public int SelectedMonitorIndex { get; set; } = 0;
        public bool HideInFullscreenApps { get; set; } = true;
        public bool AllowOverFullscreenApps { get; set; } = true;
        public bool IsIslandHidden { get; set; } = false;

        // --- Кастомизация (Customization) ---
        public SmileyStylePreset SmileyStyle { get; set; } = SmileyStylePreset.Classic;
        public bool ClockShowSeconds { get; set; } = false;
        public bool ClockUse24HourFormat { get; set; } = true;
        public bool WeatherUseCelsius { get; set; } = true;
        public int WeatherRefreshMinutes { get; set; } = 20;
        public bool CompactIconsMode { get; set; } = false;
        public string HotkeyToggleIsland { get; set; } = "Alt+Space";
        public string HotkeyQuickNote { get; set; } = "Alt+N";
        public string HotkeyClipboard { get; set; } = "Alt+B";
        public bool EnableNotificationSounds { get; set; } = true;
        public string NotificationSoundPreset { get; set; } = "applepay.mp3";

        // --- Приватность и Буфер обмена ---
        public bool ClipboardMonitoringEnabled { get; set; } = true;
        public bool ClipboardFilterSensitiveData { get; set; } = true;
        public int ClipboardMaxHistory { get; set; } = 50;

        public static AppSettings CreateDefaults()
        {
            return new AppSettings();
        }

        public AppSettings Clone()
        {
            return (AppSettings)MemberwiseClone();
        }
    }
}
