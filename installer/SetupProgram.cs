using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace WindowsDynamicIslandSetup
{
    public static class SetupProgram
    {
        [STAThread]
        public static int Main(string[] args)
        {
            string defaultDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs",
                "WindowsDynamicIsland");

            if (args != null && args.Any(a => string.Equals(a, "--silent", StringComparison.OrdinalIgnoreCase)))
            {
                PerformInstall(defaultDir, createDesktopShortcut: true, enableAutoStart: true, launchNow: false);
                return 0;
            }

            var app = new Application();
            var win = CreateSetupWindow(defaultDir);
            app.MainWindow = win;
            win.Show();
            return app.Run();
        }

        private static Window CreateSetupWindow(string targetDir)
        {
            var win = new Window
            {
                Title = "Установка Windows Dynamic Island",
                Width = 520,
                Height = 350,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                ResizeMode = ResizeMode.NoResize
            };

            var shell = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(18, 18, 21)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(42, 42, 48)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(24)
            };

            shell.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ButtonState == MouseButtonState.Pressed) win.DragMove();
            };

            var stack = new StackPanel();

            var pill = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(10, 10, 12)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(38, 38, 44)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(14, 5, 14, 5),
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, 16),
                Child = new TextBlock
                {
                    Text = "^_^  •  Windows Dynamic Island",
                    FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(20, 184, 166))
                }
            };
            stack.Children.Add(pill);

            stack.Children.Add(new TextBlock
            {
                Text = "Установка Windows Dynamic Island",
                FontFamily = new FontFamily("Segoe UI Variable Display, Segoe UI"),
                FontSize = 20,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(237, 237, 239))
            });

            stack.Children.Add(new TextBlock
            {
                Text = "Интерактивный центр управления Windows 11: системный плеер, буфер обмена с drag-and-drop, файловая полка, заметки, таймеры и живая погода.",
                FontFamily = new FontFamily("Segoe UI Variable Display, Segoe UI"),
                FontSize = 12.5,
                Foreground = new SolidColorBrush(Color.FromRgb(161, 161, 170)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 6, 0, 16)
            });

            var pathBox = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(28, 28, 32)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(45, 45, 52)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, 7, 10, 7),
                Margin = new Thickness(0, 0, 0, 12),
                Child = new TextBlock
                {
                    Text = targetDir,
                    FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                    FontSize = 11.5,
                    Foreground = new SolidColorBrush(Color.FromRgb(210, 210, 218)),
                    TextTrimming = TextTrimming.CharacterEllipsis
                }
            };
            stack.Children.Add(pathBox);

            var cbShortcut = new CheckBox
            {
                Content = "Создать ярлык на Рабочем столе и в меню Пуск",
                IsChecked = true,
                Foreground = new SolidColorBrush(Color.FromRgb(220, 220, 226)),
                Margin = new Thickness(0, 0, 0, 6)
            };
            var cbAutoStart = new CheckBox
            {
                Content = "Запускать автоматически вместе с Windows 11",
                IsChecked = true,
                Foreground = new SolidColorBrush(Color.FromRgb(220, 220, 226)),
                Margin = new Thickness(0, 0, 0, 18)
            };
            stack.Children.Add(cbShortcut);
            stack.Children.Add(cbAutoStart);

            var btnRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

            var cancelBtn = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(35, 35, 40)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16, 8, 16, 8),
                Margin = new Thickness(0, 0, 10, 0),
                Cursor = Cursors.Hand,
                Child = new TextBlock
                {
                    Text = "Отмена",
                    Foreground = new SolidColorBrush(Color.FromRgb(220, 220, 226)),
                    FontSize = 13
                }
            };
            cancelBtn.MouseLeftButtonUp += (s, e) => win.Close();

            var installText = new TextBlock
            {
                Text = "Установить и запустить",
                Foreground = new SolidColorBrush(Color.FromRgb(10, 14, 14)),
                FontWeight = FontWeights.SemiBold,
                FontSize = 13
            };
            var installBtn = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(20, 184, 166)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(20, 8, 20, 8),
                Cursor = Cursors.Hand,
                Child = installText
            };
            installBtn.MouseLeftButtonUp += (s, e) =>
            {
                installText.Text = "Установка...";
                PerformInstall(targetDir, cbShortcut.IsChecked == true, cbAutoStart.IsChecked == true, launchNow: true);
                win.Close();
            };

            btnRow.Children.Add(cancelBtn);
            btnRow.Children.Add(installBtn);
            stack.Children.Add(btnRow);

            shell.Child = stack;
            win.Content = shell;
            return win;
        }

        private static void PerformInstall(string installDir, bool createDesktopShortcut, bool enableAutoStart, bool launchNow)
        {
            try
            {
                foreach (var p in Process.GetProcessesByName("WindowsDynamicIsland"))
                {
                    try { p.Kill(); p.WaitForExit(1000); } catch { }
                }

                if (!Directory.Exists(installDir))
                {
                    Directory.CreateDirectory(installDir);
                }

                string exeTarget = Path.Combine(installDir, "WindowsDynamicIsland.exe");
                string icoTarget = Path.Combine(installDir, "app.ico");

                ExtractResource("Payload.WindowsDynamicIsland.exe", exeTarget);
                ExtractResource("Payload.app.ico", icoTarget);

                if (enableAutoStart)
                {
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                    {
                        key?.SetValue("WindowsDynamicIsland", $"\"{exeTarget}\"");
                    }
                }

                if (createDesktopShortcut)
                {
                    string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                    CreateShortcut(Path.Combine(desktop, "Windows Dynamic Island.lnk"), exeTarget, installDir, icoTarget);

                    string startMenu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Windows Dynamic Island.lnk");
                    CreateShortcut(startMenu, exeTarget, installDir, icoTarget);
                }

                if (launchNow && File.Exists(exeTarget))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = exeTarget,
                        WorkingDirectory = installDir,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка установки: " + ex.Message, "Windows Dynamic Island", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static void ExtractResource(string resourceName, string destPath)
        {
            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            {
                if (s == null) throw new InvalidOperationException("Не найден ресурс " + resourceName);
                using (FileStream fs = File.Create(destPath))
                {
                    s.CopyTo(fs);
                }
            }
        }

        private static void CreateShortcut(string shortcutPath, string targetExe, string workDir, string iconPath)
        {
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return;
                dynamic shell = Activator.CreateInstance(shellType);
                dynamic shortcut = shell.CreateShortcut(shortcutPath);
                shortcut.TargetPath = targetExe;
                shortcut.WorkingDirectory = workDir;
                shortcut.IconLocation = iconPath;
                shortcut.Description = "Windows Dynamic Island — Центр управления Windows 11";
                shortcut.Save();
            }
            catch
            {
            }
        }
    }
}
