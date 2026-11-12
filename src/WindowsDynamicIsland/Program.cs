using System;
using System.Threading;
using System.Windows;
using WindowsDynamicIsland.Services;
using WindowsDynamicIsland.UI;

namespace WindowsDynamicIsland
{
    public static class Program
    {
        private const string MutexName = @"Local\WindowsDynamicIsland_SingleInstance_v1";

        [STAThread]
        public static int Main(string[] args)
        {
            if (args != null && args.Length > 0 &&
                System.Text.RegularExpressions.Regex.IsMatch(args[0], @"\Achrome-extension://[a-p]{32}/\z"))
                return BrowserDownloadHost.Run();

            try
            {
                int packageNameLength = 0;
                const int appModelErrorNoPackage = 15700;
                if (WindowsDynamicIsland.Win32.NativeMethods.GetCurrentPackageFullName(ref packageNameLength, null) == appModelErrorNoPackage)
                    WindowsDynamicIsland.Win32.NativeMethods.SetCurrentProcessExplicitAppUserModelID("Paraxyoz.WindowsDynamicIsland");
            }
            catch
            {
            }

            // CLI Integration: allow external tools (Codex, Antigravity hooks, scripts) to push events
            // Example: WindowsDynamicIsland.exe --notify "Codex завершил работу ✓"
            if (args != null && args.Length > 0)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] == "--task" && i + 3 < args.Length)
                        return SystemIntegrationService.SendIpcNotification($"TASK:{args[i + 1]}|{args[i + 2]}|{args[i + 3]}") ? 0 : 1;
                    if (args[i] == "--ai-done" && i + 1 < args.Length)
                        return SystemIntegrationService.SendIpcNotification("AI:" + args[i + 1]) ? 0 : 1;
                    if (args[i] == "--dota-config" && i + 1 < args.Length)
                        return SystemIntegrationService.SendIpcNotification("CMD:dotaconfig:" + args[i + 1]) ? 0 : 1;
                    if (string.Equals(args[i], "--notify", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                    {
                        SystemIntegrationService.SendIpcNotification(args[i + 1]);
                        return 0;
                    }
                    if (string.Equals(args[i], "--expand", StringComparison.OrdinalIgnoreCase))
                    {
                        SystemIntegrationService.SendIpcNotification("CMD:expand");
                        return 0;
                    }
                    if (string.Equals(args[i], "--collapse", StringComparison.OrdinalIgnoreCase))
                    {
                        SystemIntegrationService.SendIpcNotification("CMD:collapse");
                        return 0;
                    }
                    if (string.Equals(args[i], "--tab", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                    {
                        SystemIntegrationService.SendIpcNotification("CMD:tab:" + args[i + 1]);
                        return 0;
                    }
                    if (string.Equals(args[i], "--shot", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                    {
                        SystemIntegrationService.SendIpcNotification("CMD:shot:" + args[i + 1]);
                        return 0;
                    }
                }
            }

            using (var mutex = new Mutex(true, MutexName, out bool createdNew))
            {
                if (!createdNew)
                {
                    // Already running: notify existing instance to pulse
                    SystemIntegrationService.SendIpcNotification("Dynamic Island активен|Нажмите Alt+Space для открытия");
                    return 0;
                }

                AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                {
                    StorageService.LogError("UnhandledException", e.ExceptionObject as Exception);
                };

                var app = new Application
                {
                    ShutdownMode = ShutdownMode.OnExplicitShutdown
                };

                ModernStyles.ApplyToApplication(app);

                app.DispatcherUnhandledException += (s, e) =>
                {
                    StorageService.LogError("DispatcherUnhandledException", e.Exception);
                    e.Handled = true;
                };

                var mainWindow = new IslandWindow();
                app.MainWindow = mainWindow;
                mainWindow.Show();

                return app.Run();
            }
        }
    }
}
