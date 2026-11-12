using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;
using WindowsDynamicIsland.Models;
using WindowsDynamicIsland.Win32;

namespace WindowsDynamicIsland.Services
{
    public enum PowerActionKind
    {
        None = 0,
        Shutdown = 1,
        Restart = 2
    }

    public sealed class SystemIntegrationService : IDisposable
    {
        public const string IpcPipeName = "WindowsDynamicIsland_IPC_v1";

        private readonly SettingsManager _settings;
        private readonly EventQueueManager _eventQueue;
        private readonly SystemAudioController _audioController;
        private readonly DispatcherTimer _keyboardStateTimer;
        private readonly DispatcherTimer _powerCountdownTimer;
        private CancellationTokenSource _ipcCts;

        // Low-level keyboard hook for near-zero-latency Caps Lock & Layout switch detection
        private IntPtr _keyboardHookHandle = IntPtr.Zero;
        private readonly NativeMethods.LowLevelKeyboardProc _keyboardHookDelegate;

        private string _currentLayoutShort = "EN";
        private string _currentLayoutFull = "English";
        private bool _capsLockOn;
        private bool _numLockOn;
        private bool _initialStateLoaded;

        private UserNotificationListener _notificationListener;
        private bool _notificationsSupported;
        private bool _requestingNotificationAccess;
        private bool _notificationEventRegistered;
        private DispatcherTimer _notificationPoll;
        private HashSet<uint> _visibleNotificationIds = new HashSet<uint>();
        private bool _disposed;

        // Power management countdown
        public PowerActionKind PendingPowerAction { get; private set; } = PowerActionKind.None;
        public int PowerCountdownSeconds { get; private set; }

        public string CurrentKeyboardLayoutShort => _currentLayoutShort;
        public string CurrentKeyboardLayoutFull => _currentLayoutFull;
        public bool IsCapsLockOn => _capsLockOn;
        public bool IsNumLockOn => _numLockOn;
        public SystemAudioController Audio => _audioController;
        public bool NotificationsListenerActive => _notificationsSupported;
        public string NotificationAccessDetail { get; private set; }

        public event Action KeyboardStateChanged;
        public event Action<int, bool> VolumeStateChanged;
        public event Action PowerStateChanged;
        public event Action<string> IpcCommandReceived;

        public SystemIntegrationService(SettingsManager settings, EventQueueManager eventQueue)
        {
            _settings = settings;
            _eventQueue = eventQueue;
            _audioController = new SystemAudioController();
            _audioController.VolumeChanged += OnHardwareVolumeChanged;

            _keyboardHookDelegate = OnLowLevelKeyboardHook;
            InstallLowLevelKeyboardHook();

            // Ultra-lightweight 40ms check at Input priority (Win32 GetKeyboardLayout + GetKeyState takes < 0.01ms)
            _keyboardStateTimer = new DispatcherTimer(DispatcherPriority.Input)
            {
                Interval = TimeSpan.FromMilliseconds(40)
            };
            _keyboardStateTimer.Tick += OnKeyboardStatePoll;
            _keyboardStateTimer.Start();

            _powerCountdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _powerCountdownTimer.Tick += OnPowerCountdownTick;

            CheckKeyboardStateImmediate();
            StartIpcPipeServer();

        }

        private void InstallLowLevelKeyboardHook()
        {
            try
            {
                using (Process curProcess = Process.GetCurrentProcess())
                using (ProcessModule curModule = curProcess.MainModule)
                {
                    IntPtr hMod = NativeMethods.GetModuleHandle(curModule?.ModuleName);
                    _keyboardHookHandle = NativeMethods.SetWindowsHookEx(
                        NativeMethods.WH_KEYBOARD_LL,
                        _keyboardHookDelegate,
                        hMod,
                        0);
                }
            }
            catch
            {
                _keyboardHookHandle = IntPtr.Zero;
            }
        }

        private IntPtr OnLowLevelKeyboardHook(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int msg = wParam.ToInt32();
                if (msg == NativeMethods.WM_KEYDOWN || msg == NativeMethods.WM_KEYUP ||
                    msg == NativeMethods.WM_SYSKEYDOWN || msg == NativeMethods.WM_SYSKEYUP)
                {
                    int vkCode = Marshal.ReadInt32(lParam);
                    if (vkCode == NativeMethods.VK_CAPITAL ||
                        vkCode == NativeMethods.VK_NUMLOCK ||
                        vkCode == NativeMethods.VK_SHIFT ||
                        vkCode == NativeMethods.VK_LSHIFT ||
                        vkCode == NativeMethods.VK_RSHIFT ||
                        vkCode == NativeMethods.VK_MENU ||
                        vkCode == NativeMethods.VK_LMENU ||
                        vkCode == NativeMethods.VK_RMENU ||
                        vkCode == NativeMethods.VK_LWIN ||
                        vkCode == NativeMethods.VK_RWIN ||
                        vkCode == NativeMethods.VK_SPACE)
                    {
                        // Schedule immediate check on UI thread right after OS processes the key event
                        Application.Current?.Dispatcher?.BeginInvoke(
                            DispatcherPriority.Send,
                            new Action(() => PollKeyboardStateCore()));
                    }
                }
            }
            return NativeMethods.CallNextHookEx(_keyboardHookHandle, nCode, wParam, lParam);
        }

        private void OnHardwareVolumeChanged(int volumePercent, bool isMuted, bool isExternal)
        {
            Application.Current?.Dispatcher?.BeginInvoke(
                DispatcherPriority.Send,
                new Action(() =>
                {
                    VolumeStateChanged?.Invoke(volumePercent, isMuted);
                    if (isExternal)
                    {
                        _eventQueue?.Enqueue(new IslandEvent
                        {
                            EventType = IslandEventType.Volume,
                            Priority = IslandEventPriority.Normal,
                            Title = isMuted ? "Звук выключен" : $"Громкость {volumePercent}%",
                            Subtitle = isMuted ? "Системный микшер • MUTE" : "Системный аудиовыход",
                            BadgeText = isMuted ? "MUTE" : $"{volumePercent}%",
                            ProgressRatio = isMuted ? 0.0 : volumePercent / 100.0,
                            DurationMs = 1400,
                            Mood = SmileyMood.Normal
                        });
                    }
                }));
        }

        private void CheckKeyboardStateImmediate()
        {
            _currentLayoutShort = NativeMethods.GetCurrentKeyboardLanguageShortName(out _currentLayoutFull);
            _capsLockOn = NativeMethods.IsCapsLockOn();
            _numLockOn = NativeMethods.IsNumLockOn();
            _initialStateLoaded = true;
        }

        public void SwitchKeyboardLayoutAndNotify(IntPtr islandHwnd)
        {
            NativeMethods.SwitchToNextKeyboardLayout(islandHwnd);
            // Poll immediately and after 15ms once target window processes WM_INPUTLANGCHANGEREQUEST
            PollKeyboardStateCore();
            Task.Delay(18).ContinueWith(_ =>
            {
                Application.Current?.Dispatcher?.BeginInvoke(
                    DispatcherPriority.Send,
                    new Action(PollKeyboardStateCore));
            });
        }

        public void ToggleCapsLockAndNotify()
        {
            NativeMethods.ToggleCapsLock();
            PollKeyboardStateCore();
        }

        private void OnKeyboardStatePoll(object sender, EventArgs e)
        {
            PollKeyboardStateCore();
        }

        public void PollKeyboardStateCore()
        {
            string shortLang = NativeMethods.GetCurrentKeyboardLanguageShortName(out string fullLang);
            bool caps = NativeMethods.IsCapsLockOn();
            bool num = NativeMethods.IsNumLockOn();

            bool layoutChanged = _initialStateLoaded && shortLang != _currentLayoutShort;
            bool capsChanged = _initialStateLoaded && caps != _capsLockOn;
            bool numChanged = _initialStateLoaded && num != _numLockOn;

            _currentLayoutShort = shortLang;
            _currentLayoutFull = fullLang;
            _capsLockOn = caps;
            _numLockOn = num;

            if (layoutChanged || capsChanged || numChanged)
            {
                KeyboardStateChanged?.Invoke();
            }

            if (layoutChanged)
            {
                _eventQueue?.Enqueue(new IslandEvent
                {
                    EventType = IslandEventType.KeyboardLayout,
                    Priority = IslandEventPriority.Normal,
                    Title = $"Раскладка: {shortLang}",
                    Subtitle = fullLang,
                    BadgeText = shortLang,
                    DurationMs = 1250,
                    Mood = SmileyMood.Normal
                });
            }

            if (capsChanged)
            {
                _eventQueue?.Enqueue(new IslandEvent
                {
                    EventType = IslandEventType.CapsLock,
                    Priority = IslandEventPriority.Normal,
                    Title = caps ? "Caps Lock включён" : "Caps Lock выключен",
                    Subtitle = caps ? "Ввод заглавными буквами" : "Обычный регистр клавиатуры",
                    BadgeText = caps ? "CAPS ON" : "CAPS OFF",
                    DurationMs = 1300,
                    Mood = caps ? SmileyMood.NotificationAlert : SmileyMood.Normal
                });
            }
        }

        public void OnDeviceChanged(int eventType)
        {
            if (eventType != NativeMethods.DBT_DEVICEARRIVAL && eventType != NativeMethods.DBT_DEVICEREMOVECOMPLETE)
                return;

            bool connected = eventType == NativeMethods.DBT_DEVICEARRIVAL;
            _eventQueue?.Enqueue(new IslandEvent
            {
                EventType = IslandEventType.DeviceConnection,
                Priority = IslandEventPriority.Normal,
                Title = connected ? "Устройство подключено" : "Устройство отключено",
                Subtitle = "Системная шина периферии Windows",
                BadgeText = connected ? "USB +" : "USB −",
                DurationMs = 2000,
                Mood = SmileyMood.NotificationAlert
            });
        }

        public async Task InitializeNotificationListenerAsync(bool requestAccess = false)
        {
            if (_disposed || _notificationsSupported || _requestingNotificationAccess) return;
            _requestingNotificationAccess = true;
            try
            {
                _notificationListener = UserNotificationListener.Current;
                var access = requestAccess
                    ? await _notificationListener.RequestAccessAsync()
                    : _notificationListener.GetAccessStatus();
                if (_disposed) return;
                StorageService.LogInfo("Windows notification access: " + access);
                NotificationAccessDetail = access == UserNotificationListenerAccessStatus.Denied
                    ? "Windows отклонила доступ. Проверьте разрешение для Windows Dynamic Island."
                    : "Windows ещё не подтвердила доступ к уведомлениям.";
                if (access != UserNotificationListenerAccessStatus.Allowed) return;
                try
                {
                    _notificationListener.NotificationChanged += OnWindowsNotificationChanged;
                    _notificationEventRegistered = true;
                }
                catch (Exception ex) when (ex.HResult == unchecked((int)0x80070490) || ex.HResult == unchecked((int)0x800706BE))
                {
                    var current = await _notificationListener.GetNotificationsAsync(NotificationKinds.Toast);
                    if (_disposed) return;
                    _visibleNotificationIds = new HashSet<uint>(current.Select(item => item.Id));
                    _notificationPoll = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
                    _notificationPoll.Tick += PollWindowsNotifications;
                    _notificationPoll.Start();
                }
                _notificationsSupported = true;
                if (!_settings.Current.WindowsNotificationListenerEnabled)
                {
                    _settings.Current.WindowsNotificationListenerEnabled = true;
                    _settings.Save();
                }
            }
            catch (Exception ex)
            {
                NotificationAccessDetail = $"Windows не подключила уведомления (0x{ex.HResult:X8}).";
                if (_settings.Current.WindowsNotificationListenerEnabled)
                {
                    _settings.Current.WindowsNotificationListenerEnabled = false;
                    _settings.Save();
                }
                StorageService.LogError(nameof(InitializeNotificationListenerAsync), ex);
            }
            finally
            {
                _requestingNotificationAccess = false;
            }
        }

        private void OnWindowsNotificationChanged(UserNotificationListener sender, UserNotificationChangedEventArgs args)
        {
            if (_disposed || args.ChangeKind != UserNotificationChangedKind.Added || !_settings.Current.ShowSystemNotifications) return;
            try
            {
                ForwardWindowsNotification(sender.GetNotification(args.UserNotificationId));
            }
            catch (Exception ex)
            {
                StorageService.LogError(nameof(OnWindowsNotificationChanged), ex);
            }
        }

        private async void PollWindowsNotifications(object sender, EventArgs args)
        {
            _notificationPoll.Stop();
            try
            {
                var current = await _notificationListener.GetNotificationsAsync(NotificationKinds.Toast);
                if (_disposed) return;
                if (_settings.Current.ShowSystemNotifications)
                    foreach (var toast in current.Where(toast => !_visibleNotificationIds.Contains(toast.Id)).OrderBy(toast => toast.CreationTime))
                        ForwardWindowsNotification(toast);
                _visibleNotificationIds = new HashSet<uint>(current.Select(toast => toast.Id));
                _notificationPoll.Start();
            }
            catch (Exception ex)
            {
                _notificationsSupported = false;
                NotificationAccessDetail = $"Windows остановила получение уведомлений (0x{ex.HResult:X8}).";
                StorageService.LogError(nameof(PollWindowsNotifications), ex);
            }
        }

        private void ForwardWindowsNotification(UserNotification toast)
        {
            if (toast == null) return;
            try
            {
                var appName = toast.AppInfo?.DisplayInfo?.DisplayName;
                var appId = toast.AppInfo?.AppUserModelId;
                string messenger = IdentifyMessenger(appName, appId);
                bool telegram = messenger == "Telegram";
                var binding = toast.Notification?.Visual?.GetBinding(KnownNotificationBindings.ToastGeneric);
                var texts = binding?.GetTextElements()?.Select(t => t.Text?.Trim())
                    .Where(t => !string.IsNullOrWhiteSpace(t)).ToList() ?? new List<string>();
                string senderName = texts.FirstOrDefault() ?? appName ?? "Уведомление";
                if (string.Equals(senderName, appName, StringComparison.OrdinalIgnoreCase) && texts.Count >= 3)
                {
                    texts.RemoveAt(0);
                    senderName = texts[0];
                }
                string body = string.Join(" ", texts.Skip(1));
                var chat = telegram ? TelegramChatUri(senderName) : null;

                Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                {
                    var notification = new IslandEvent
                    {
                        EventType = IslandEventType.AppNotification,
                        Priority = IslandEventPriority.High,
                        Title = senderName,
                        Subtitle = string.IsNullOrWhiteSpace(body) ? "Новое уведомление" : body,
                        BadgeText = messenger ?? appName ?? "Windows",
                        DurationMs = messenger != null ? 8500 : 5600,
                        Mood = SmileyMood.NotificationAlert,
                        PrimaryActionLabel = chat != null ? "Открыть " + senderName : "Уведомления"
                    };
                    notification.PrimaryAction = () =>
                    {
                        if (_eventQueue.CurrentEvent == notification) _eventQueue.DismissCurrent();
                        if (chat == null || !TryOpenChat(chat)) OpenNotificationCenter();
                    };
                    _eventQueue.Enqueue(notification);
                }));
            }
            catch (Exception ex)
            {
                StorageService.LogError(nameof(ForwardWindowsNotification), ex);
            }
        }

        private static string IdentifyMessenger(string displayName, string appId)
        {
            string identity = displayName + " " + appId;
            foreach (string name in new[] { "Telegram", "WhatsApp", "Signal", "Discord", "Teams", "Slack", "Viber", "Messenger" })
            {
                if (identity.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0) return name;
            }
            return null;
        }

        public static Uri TelegramChatUri(string senderName)
        {
            if (string.IsNullOrEmpty(senderName) || !Regex.IsMatch(senderName, @"\A@[A-Za-z][A-Za-z0-9_]{3,31}\z"))
                return null;
            return new Uri("tg://resolve?domain=" + senderName.Substring(1));
        }

        private static bool TryOpenChat(Uri chat)
        {
            try
            {
                using (Process.Start(new ProcessStartInfo(chat.OriginalString) { UseShellExecute = true })) { }
                return true;
            }
            catch (Exception ex)
            {
                StorageService.LogError(nameof(TryOpenChat), ex);
                return false;
            }
        }

        private static void OpenNotificationCenter()
        {
            // The original Windows notification retains its app-provided chat activation.
            int windowsBuild = Environment.OSVersion.Version.Build;
            try
            {
                using (var version = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                    if (int.TryParse(version?.GetValue("CurrentBuildNumber") as string, out int build)) windowsBuild = build;
            }
            catch (System.Security.SecurityException) { }

            byte key = windowsBuild >= 22000 ? (byte)0x4E : (byte)0x41; // Win+N / Win+A
            NativeMethods.keybd_event((byte)NativeMethods.VK_LWIN, 0, NativeMethods.KEYEVENTF_EXTENDEDKEY, UIntPtr.Zero);
            NativeMethods.keybd_event(key, 0, 0, UIntPtr.Zero);
            NativeMethods.keybd_event(key, 0, NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
            NativeMethods.keybd_event((byte)NativeMethods.VK_LWIN, 0,
                NativeMethods.KEYEVENTF_EXTENDEDKEY | NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        // --- IPC Named Pipe Server for CLI Commands ---
        private void StartIpcPipeServer()
        {
            _ipcCts = new CancellationTokenSource();
            CancellationToken token = _ipcCts.Token;

            Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        using (var server = new NamedPipeServerStream(IpcPipeName, PipeDirection.In, 4, PipeTransmissionMode.Byte, PipeOptions.Asynchronous))
                        {
                            await server.WaitForConnectionAsync(token);
                            using (var reader = new StreamReader(server, Encoding.UTF8))
                            {
                                string payload = await reader.ReadToEndAsync();
                                if (!string.IsNullOrWhiteSpace(payload))
                                {
                                    HandleIpcPayload(payload.Trim());
                                }
                            }
                        }
                    }
                    catch
                    {
                        await Task.Delay(300);
                    }
                }
            }, token);
        }

        private void HandleIpcPayload(string payload)
        {
            if (payload.StartsWith("CMD:", StringComparison.OrdinalIgnoreCase))
            {
                string cmd = payload.Substring(4).Trim();
                Application.Current?.Dispatcher?.BeginInvoke(
                    DispatcherPriority.Send,
                    new Action(() =>
                    {
                        IpcCommandReceived?.Invoke(cmd);
                    }));
                return;
            }

            string[] parts = payload.Split(new[] { '|' }, 2);
            string title = parts[0].Trim();
            string subtitle = parts.Length > 1 ? parts[1].Trim() : "Системное уведомление";
            Application.Current?.Dispatcher?.BeginInvoke(
                DispatcherPriority.Send,
                new Action(() =>
                {
                    _eventQueue?.Enqueue(new IslandEvent
                    {
                        EventType = IslandEventType.AppNotification,
                        Priority = IslandEventPriority.Normal,
                        Title = title,
                        Subtitle = subtitle,
                        BadgeText = "УВЕД",
                        DurationMs = 3200,
                        Mood = SmileyMood.NotificationAlert
                    });
                }));
        }

        public static bool SendIpcNotification(string message)
        {
            try
            {
                using (var client = new NamedPipeClientStream(".", IpcPipeName, PipeDirection.Out))
                {
                    client.Connect(800);
                    byte[] bytes = Encoding.UTF8.GetBytes(message);
                    client.Write(bytes, 0, bytes.Length);
                    client.Flush();
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        // --- Power Management (Shutdown / Restart with Confirmation & Cancellation) ---
        public void SchedulePowerAction(PowerActionKind kind, int delaySeconds = 10)
        {
            if (kind == PowerActionKind.None)
            {
                CancelPowerAction();
                return;
            }

            PendingPowerAction = kind;
            PowerCountdownSeconds = Math.Max(5, delaySeconds);
            _powerCountdownTimer.Start();
            PowerStateChanged?.Invoke();
            EmitPowerCountdownEvent();
        }

        public void CancelPowerAction()
        {
            if (PendingPowerAction == PowerActionKind.None) return;
            PendingPowerAction = PowerActionKind.None;
            PowerCountdownSeconds = 0;
            _powerCountdownTimer.Stop();

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "shutdown.exe",
                    Arguments = "/a",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
            }
            catch
            {
            }

            PowerStateChanged?.Invoke();
            _eventQueue?.DismissCurrent();
        }

        private void OnPowerCountdownTick(object sender, EventArgs e)
        {
            if (PendingPowerAction == PowerActionKind.None)
            {
                _powerCountdownTimer.Stop();
                return;
            }

            PowerCountdownSeconds--;
            PowerStateChanged?.Invoke();

            if (PowerCountdownSeconds <= 0)
            {
                var action = PendingPowerAction;
                PendingPowerAction = PowerActionKind.None;
                _powerCountdownTimer.Stop();
                PowerStateChanged?.Invoke();
                ExecuteSystemPowerCommand(action);
            }
            else
            {
                EmitPowerCountdownEvent();
            }
        }

        private void EmitPowerCountdownEvent()
        {
            string actionName = PendingPowerAction == PowerActionKind.Shutdown ? "Выключение ПК" : "Перезагрузка ПК";
            _eventQueue?.Enqueue(new IslandEvent
            {
                EventType = IslandEventType.PowerCountdown,
                Priority = IslandEventPriority.Critical,
                Title = $"{actionName} через {PowerCountdownSeconds} сек",
                Subtitle = "Нажмите «Отменить», чтобы прервать операцию",
                BadgeText = $"{PowerCountdownSeconds}с",
                DurationMs = 1500,
                Mood = SmileyMood.NotificationAlert,
                PrimaryActionLabel = "Отменить",
                PrimaryAction = CancelPowerAction
            });
        }

        private static void ExecuteSystemPowerCommand(PowerActionKind action)
        {
            try
            {
                string flag = action == PowerActionKind.Restart ? "/r /t 0" : "/s /t 0";
                Process.Start(new ProcessStartInfo
                {
                    FileName = "shutdown.exe",
                    Arguments = flag,
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
            }
            catch (Exception ex)
            {
                StorageService.LogError("ExecuteSystemPowerCommand", ex);
            }
        }

        public void Dispose()
        {
            _disposed = true;
            if (_keyboardHookHandle != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_keyboardHookHandle);
                _keyboardHookHandle = IntPtr.Zero;
            }
            _keyboardStateTimer?.Stop();
            _powerCountdownTimer?.Stop();
            _ipcCts?.Cancel();
            _notificationPoll?.Stop();
            if (_notificationListener != null && _notificationEventRegistered)
                _notificationListener.NotificationChanged -= OnWindowsNotificationChanged;
            _audioController?.Dispose();
        }
    }
}
