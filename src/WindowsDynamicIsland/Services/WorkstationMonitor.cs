using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Threading;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using WindowsDynamicIsland.Models;
using WindowsDynamicIsland.Win32;

namespace WindowsDynamicIsland.Services
{
    public sealed class WorkstationMonitor : IDisposable
    {
        private static readonly Dictionary<string, string> Games = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["dota2"] = "Dota 2", ["cs2"] = "Counter-Strike 2", ["VALORANT-Win64-Shipping"] = "VALORANT",
            ["FortniteClient-Win64-Shipping"] = "Fortnite", ["Overwatch"] = "Overwatch",
            ["Cyberpunk2077"] = "Cyberpunk 2077", ["eldenring"] = "Elden Ring", ["GTA5"] = "GTA V"
        };
        private const string BatteryProperty = "System.Devices.BatteryLife";
        private readonly EventQueueManager _events;
        private readonly DispatcherTimer _timer;
        private readonly DispatcherTimer _bluetoothTimer;
        private Dictionary<string, string> _bluetooth;
        private ulong _idle, _total;
        private int _gamePid;
        private bool _disposed, _sampling, _bluetoothBusy;
        private DateTime _nextGpu;
        private bool _batteryWarned;
        private string _outputId;
        private string _inputId;

        public event Action Changed;
        public event Action OutputChanged;
        public double? CpuPercent { get; private set; }
        public double? CpuTemperature { get; private set; }
        public double? GpuPercent { get; private set; }
        public double? GpuTemperature { get; private set; }
        public int? BatteryPercent { get; private set; }
        public bool OnAc { get; private set; }
        public bool? MicrophoneMuted { get; private set; }
        public string AudioOutput { get; private set; }
        public IReadOnlyCollection<string> BluetoothDevices => _bluetooth?.Values.ToArray() ?? Array.Empty<string>();
        public string Game { get; private set; }
        public DateTime GameStarted { get; private set; }
        public bool DashboardVisible { get; set; }

        public WorkstationMonitor(EventQueueManager events)
        {
            _events = events;
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _timer.Tick += Sample;
            _bluetoothTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(12) };
            _bluetoothTimer.Tick += SampleBluetooth;
            _timer.Start();
            _bluetoothTimer.Start();
        }

        private async void Sample(object sender, EventArgs args)
        {
            if (_sampling || _disposed) return;
            _sampling = true;
            try
            {
                var power = SystemInformation.PowerStatus;
                BatteryPercent = (power.BatteryChargeStatus & BatteryChargeStatus.NoSystemBattery) == 0 &&
                                 power.BatteryChargeStatus != BatteryChargeStatus.Unknown && power.BatteryLifePercent >= 0
                    ? (int?)Math.Round(power.BatteryLifePercent * 100) : null;
                OnAc = power.PowerLineStatus == PowerLineStatus.Online;
                if (OnAc || BatteryPercent > 18) _batteryWarned = false;
                if (!_batteryWarned && !OnAc && BatteryPercent <= 15)
                {
                    _batteryWarned = true;
                    Notify(IslandEventType.DeviceConnection, "Низкий заряд ноутбука", BatteryPercent + "%", "Питание");
                }

                DetectGame();
                var output = SystemAudioController.ReadEndpoint(EDataFlow.eRender);
                var microphone = SystemAudioController.ReadEndpoint(EDataFlow.eCapture);
                if (_outputId != null && output.Id != null && _outputId != output.Id)
                {
                    OutputChanged?.Invoke();
                    Notify(IslandEventType.DeviceConnection, "Аудиовыход изменён", output.Name ?? "Другое устройство Windows", "Звук");
                }
                if (microphone.Id != null && _inputId == microphone.Id && MicrophoneMuted.HasValue &&
                    microphone.Muted.HasValue && MicrophoneMuted != microphone.Muted)
                    Notify(IslandEventType.DeviceConnection, microphone.Muted.Value ? "Микрофон выключен" : "Микрофон включён",
                        "Системный микрофон Windows", "Звук");
                _outputId = output.Id;
                AudioOutput = output.Name;
                _inputId = microphone.Id;
                MicrophoneMuted = microphone.Muted;

                if (GetSystemTimes(out ulong idle, out ulong kernel, out ulong user))
                {
                    ulong total = kernel + user;
                    if (_total != 0 && total > _total && idle >= _idle)
                        CpuPercent = 100 * (1 - Math.Min(1, (double)(idle - _idle) / (total - _total)));
                    _idle = idle;
                    _total = total;
                }

                if ((DashboardVisible || Game != null) && DateTime.UtcNow >= _nextGpu)
                {
                    _nextGpu = DateTime.UtcNow.AddSeconds(6);
                    var sensors = await Task.Run(() =>
                    {
                        var gpu = ReadNvidia();
                        return (GpuLoad: gpu.Load ?? ReadGpuEngineLoad(), GpuTemp: gpu.Temperature, CpuTemp: ReadCpuTemperature());
                    });
                    if (_disposed) return;
                    CpuTemperature = sensors.CpuTemp;
                    GpuPercent = sensors.GpuLoad;
                    GpuTemperature = sensors.GpuTemp;
                }
                Changed?.Invoke();
            }
            catch (Exception ex) { StorageService.LogError(nameof(Sample), ex); }
            finally { _sampling = false; }
        }

        private void DetectGame()
        {
            NativeMethods.GetWindowThreadProcessId(NativeMethods.GetForegroundWindow(), out uint foreground);
            if (foreground != 0 && foreground != _gamePid)
            {
                try
                {
                    using (var app = Process.GetProcessById((int)foreground))
                    {
                        if (Games.TryGetValue(app.ProcessName, out string name))
                        {
                            _gamePid = app.Id;
                            Game = name;
                            GameStarted = app.StartTime;
                            Notify(IslandEventType.DeviceConnection, "Игровая сессия", name, "Игра");
                            return;
                        }
                    }
                }
                catch (ArgumentException) { }
                catch (System.ComponentModel.Win32Exception) { }
            }
            if (_gamePid == 0) return;
            try
            {
                using (var process = Process.GetProcessById(_gamePid))
                    if (!process.HasExited && process.StartTime == GameStarted) return;
            }
            catch (ArgumentException) { }
            catch (System.ComponentModel.Win32Exception) { }
            _gamePid = 0;
            Game = null;
        }

        private async void SampleBluetooth(object sender, EventArgs args)
        {
            if (_disposed || _bluetoothBusy) return;
            _bluetoothBusy = true;
            try
            {
                var classic = await DeviceInformation.FindAllAsync(
                    BluetoothDevice.GetDeviceSelectorFromConnectionStatus(BluetoothConnectionStatus.Connected),
                    new[] { BatteryProperty });
                var lowEnergy = await DeviceInformation.FindAllAsync(
                    BluetoothLEDevice.GetDeviceSelectorFromConnectionStatus(BluetoothConnectionStatus.Connected),
                    new[] { BatteryProperty });
                if (_disposed) return;
                var next = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var device in classic.Concat(lowEnergy).Where(device => !string.IsNullOrWhiteSpace(device.Name)))
                {
                    string label = device.Name;
                    if (device.Properties.TryGetValue(BatteryProperty, out var value) &&
                        int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out int battery) &&
                        battery >= 0 && battery <= 100)
                        label += $" · {battery}%";
                    if (!next.TryGetValue(device.Name, out string previous) || label.Length > previous.Length)
                        next[device.Name] = label;
                }
                if (_bluetooth != null)
                {
                    foreach (var added in next.Keys.Except(_bluetooth.Keys, StringComparer.OrdinalIgnoreCase))
                        Notify(IslandEventType.DeviceConnection, "Bluetooth подключён", next[added], "Bluetooth");
                    foreach (var removed in _bluetooth.Keys.Except(next.Keys, StringComparer.OrdinalIgnoreCase))
                        Notify(IslandEventType.DeviceConnection, "Bluetooth отключён", removed, "Bluetooth");
                }
                _bluetooth = next;
                Changed?.Invoke();
            }
            catch (Exception ex)
            {
                _bluetoothTimer.Stop();
                StorageService.LogError(nameof(SampleBluetooth), ex);
            }
            finally { _bluetoothBusy = false; }
        }

        private static (double? Load, double? Temperature) ReadNvidia()
        {
            string executable = Path.Combine(Environment.SystemDirectory, "nvidia-smi.exe");
            if (!File.Exists(executable)) return (null, null);
            try
            {
                using (var process = Process.Start(new ProcessStartInfo(executable, "--query-gpu=utilization.gpu,temperature.gpu --format=csv,noheader,nounits")
                {
                    UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true
                }))
                {
                    if (!process.WaitForExit(2000))
                    {
                        process.Kill();
                        return (null, null);
                    }
                    var fields = (process.StandardOutput.ReadLine() ?? "").Split(',');
                    if (fields.Length != 2) return (null, null);
                    return (Parse(fields[0]), Parse(fields[1]));
                }
            }
            catch { return (null, null); }
        }

        private static double? ReadGpuEngineLoad()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher(@"root\CIMV2",
                    "SELECT Name, UtilizationPercentage FROM Win32_PerfFormattedData_GPUPerformanceCounters_GPUEngine"))
                using (var values = searcher.Get())
                {
                    var engines = new Dictionary<string, double>();
                    foreach (ManagementObject row in values)
                    {
                        using (row)
                        {
                            string name = Convert.ToString(row["Name"]);
                            int start = name.IndexOf("_luid_", StringComparison.OrdinalIgnoreCase);
                            if (start < 0 || row["UtilizationPercentage"] == null) continue;
                            string engine = name.Substring(start);
                            engines.TryGetValue(engine, out double sum);
                            engines[engine] = sum + Convert.ToDouble(row["UtilizationPercentage"], CultureInfo.InvariantCulture);
                        }
                    }
                    return engines.Count == 0 ? (double?)null : Math.Min(100, engines.Values.Max());
                }
            }
            catch { return null; }
        }

        private static double? ReadCpuTemperature()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher(@"root\LibreHardwareMonitor",
                    "SELECT Name, Identifier, Value FROM Sensor WHERE SensorType='Temperature'"))
                using (var values = searcher.Get())
                {
                    foreach (ManagementObject row in values)
                    {
                        using (row)
                        {
                            string id = Convert.ToString(row["Identifier"]);
                            string name = Convert.ToString(row["Name"]);
                            if ((id.StartsWith("/intelcpu/", StringComparison.OrdinalIgnoreCase) ||
                                 id.StartsWith("/amdcpu/", StringComparison.OrdinalIgnoreCase)) &&
                                (name.Equals("CPU Package", StringComparison.OrdinalIgnoreCase) ||
                                 name.IndexOf("Tctl", StringComparison.OrdinalIgnoreCase) >= 0) && row["Value"] != null)
                            {
                                double temperature = Convert.ToDouble(row["Value"], CultureInfo.InvariantCulture);
                                if (temperature > 0 && temperature < 130) return temperature;
                            }
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        private static double? Parse(string value) =>
            double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double result) ? (double?)result : null;

        private void Notify(IslandEventType type, string title, string detail, string source) => _events.Enqueue(new IslandEvent
        {
            EventType = type, Priority = IslandEventPriority.Normal, Title = title, Subtitle = detail, BadgeText = source,
            DurationMs = 3500, Mood = SmileyMood.NotificationAlert
        });

        public void Dispose()
        {
            _disposed = true;
            _timer.Stop();
            _bluetoothTimer.Stop();
        }

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetSystemTimes(out ulong idle, out ulong kernel, out ulong user);
    }
}
