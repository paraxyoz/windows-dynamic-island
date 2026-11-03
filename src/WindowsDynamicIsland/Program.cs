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
            // CLI Integration: allow external tools (Codex, Antigravity hooks, scripts) to push events
            // Example: WindowsDynamicIsland.exe --notify "Codex завершил работу ✓"
            if (args != null && args.Length > 0)
            {
                for (int i = 0; i < args.Length; i++)
                {
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
