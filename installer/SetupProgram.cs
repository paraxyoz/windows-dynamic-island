using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace WindowsDynamicIslandSetup
{
    public static class SetupProgram
    {
        [DllImport("shell32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern void SHChangeNotify(int wEventId, int uFlags, IntPtr dwItem1, IntPtr dwItem2);

        private const int SHCNE_ASSOCCHANGED = 0x08000000;
        private const int SHCNF_IDLIST = 0x0000;

        [STAThread]
        public static int Main(string[] args)
        {
            string defaultDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs",
                "WindowsDynamicIsland");

            if (args != null && args.Any(a => string.Equals(a, "--silent", StringComparison.OrdinalIgnoreCase)))
            {
                return PerformInstall(defaultDir, createDesktopShortcut: true, enableAutoStart: true,
                    launchNow: true, showErrors: false) ? 0 : 1;
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
                Width = 550,
                Height = 390,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                ResizeMode = ResizeMode.NoResize
            };

            try
            {
                using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("Payload.app.ico"))
                {
                    if (s != null)
                    {
                        win.Icon = BitmapFrame.Create(s);
                    }
                }
            }
            catch
            {
            }

            var shell = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(18, 18, 22)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(48, 48, 56)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(24)
            };

            var stack = new StackPanel();

            // Header Row (Grabbable for window moving, containing logo, titles, and close button)
            var headerGrid = new Grid
            {
                Margin = new Thickness(0, 0, 0, 14),
                Background = Brushes.Transparent
            };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            headerGrid.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ButtonState == MouseButtonState.Pressed && !(e.OriginalSource is Button) && !(e.OriginalSource is TextBox))
                {
                    try { win.DragMove(); } catch { }
                }
            };

            try
            {
                using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("Payload.logo.png"))
                {
                    if (s != null)
                    {
                        var bi = new BitmapImage();
                        bi.BeginInit();
                        bi.StreamSource = s;
                        bi.CacheOption = BitmapCacheOption.OnLoad;
                        bi.EndInit();
                        bi.Freeze();

                        var logoImg = new Image
                        {
                            Source = bi,
                            Width = 52,
                            Height = 52,
                            Margin = new Thickness(0, 0, 16, 0),
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        Grid.SetColumn(logoImg, 0);
                        headerGrid.Children.Add(logoImg);
                    }
                }
            }
            catch
            {
            }

            var titleCol = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(titleCol, 1);

            var pill = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(12, 12, 16)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(42, 42, 52)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(10, 3, 10, 3),
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, 4),
                Child = new TextBlock
                {
                    Text = "^_^  •  Windows Dynamic Island Beta",
                    FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(167, 139, 250))
                }
            };
            titleCol.Children.Add(pill);

            titleCol.Children.Add(new TextBlock
            {
                Text = "Установка Windows Dynamic Island",
                FontFamily = new FontFamily("Segoe UI Variable Display, Segoe UI"),
                FontSize = 19,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(240, 240, 244))
            });

            headerGrid.Children.Add(titleCol);

            // Close (X) button in header
            var closeBtn = new Border
            {
                Width = 28,
                Height = 28,
                CornerRadius = new CornerRadius(14),
                Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Right,
                Child = new TextBlock
                {
                    Text = "✕",
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 190)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            closeBtn.MouseEnter += (s, e) => closeBtn.Background = new SolidColorBrush(Color.FromArgb(45, 255, 255, 255));
            closeBtn.MouseLeave += (s, e) => closeBtn.Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255));
            closeBtn.MouseLeftButtonUp += (s, e) => win.Close();
            Grid.SetColumn(closeBtn, 2);
            headerGrid.Children.Add(closeBtn);

            stack.Children.Add(headerGrid);

            stack.Children.Add(new TextBlock
            {
                Text = "Нативный центр управления для Windows 10 и 11: системный плеер, приватный буфер обмена с drag-and-drop, файловая полка, карточки заметок, таймеры и погода.",
                FontFamily = new FontFamily("Segoe UI Variable Display, Segoe UI"),
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(161, 161, 170)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 12)
            });

            // Path Selection Row
            var pathLabel = new TextBlock
            {
                Text = "Папка установки:",
                FontFamily = new FontFamily("Segoe UI Variable Display, Segoe UI"),
                FontSize = 12,
                FontWeight = FontWeights.Medium,
                Foreground = new SolidColorBrush(Color.FromRgb(210, 210, 220)),
                Margin = new Thickness(0, 0, 0, 6)
            };
            stack.Children.Add(pathLabel);

            var pathRow = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            pathRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            pathRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var pathTextBox = new TextBox
            {
                Text = targetDir,
                FontFamily = new FontFamily("Cascadia Mono, Consolas, Segoe UI"),
                FontSize = 11.5,
                Foreground = new SolidColorBrush(Color.FromRgb(230, 230, 235)),
                Background = new SolidColorBrush(Color.FromRgb(26, 26, 32)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(55, 55, 65)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 7, 10, 7),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(pathTextBox, 0);
            pathRow.Children.Add(pathTextBox);

            var browseBtn = CreateStyledButton("Обзор...", Color.FromRgb(38, 38, 46), Color.FromRgb(220, 220, 230), 12, new Thickness(14, 7, 14, 7));
            browseBtn.Margin = new Thickness(8, 0, 0, 0);
            browseBtn.Click += (s, e) =>
            {
                try
                {
                    using (var fbd = new System.Windows.Forms.FolderBrowserDialog())
                    {
                        fbd.Description = "Выберите папку для установки Windows Dynamic Island";
                        fbd.SelectedPath = Directory.Exists(pathTextBox.Text) ? pathTextBox.Text : targetDir;
                        fbd.ShowNewFolderButton = true;
                        if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
                        {
                            pathTextBox.Text = fbd.SelectedPath;
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Ошибка при открытии проводника: " + ex.Message, "Windows Dynamic Island", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            };
            Grid.SetColumn(browseBtn, 1);
            pathRow.Children.Add(browseBtn);

            stack.Children.Add(pathRow);

            var cbShortcut = new CheckBox
            {
                Content = "Создать ярлык на Рабочем столе и в меню Пуск",
                IsChecked = true,
                Foreground = new SolidColorBrush(Color.FromRgb(220, 220, 226)),
                Margin = new Thickness(0, 0, 0, 6)
            };
            var cbAutoStart = new CheckBox
            {
                Content = "Запускать автоматически вместе с Windows",
                IsChecked = true,
                Foreground = new SolidColorBrush(Color.FromRgb(220, 220, 226)),
                Margin = new Thickness(0, 0, 0, 16)
            };
            stack.Children.Add(cbShortcut);
            stack.Children.Add(cbAutoStart);

            var btnRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

            var cancelBtn = CreateStyledButton("Отмена", Color.FromRgb(35, 35, 42), Color.FromRgb(220, 220, 226), 13, new Thickness(18, 9, 18, 9));
            cancelBtn.Margin = new Thickness(0, 0, 10, 0);
            cancelBtn.Click += (s, e) => win.Close();

            var installBtn = CreateStyledButton("Установить и запустить", Color.FromRgb(167, 139, 250), Color.FromRgb(10, 10, 14), 13, new Thickness(22, 9, 22, 9), isPrimary: true);
            installBtn.Click += (s, e) =>
            {
                string chosenPath = pathTextBox.Text.Trim();
                if (string.IsNullOrWhiteSpace(chosenPath))
                {
                    MessageBox.Show("Пожалуйста, укажите путь для установки.", "Windows Dynamic Island", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                installBtn.IsEnabled = false;
                installBtn.Content = "Установка...";
                if (PerformInstall(chosenPath, cbShortcut.IsChecked == true, cbAutoStart.IsChecked == true, launchNow: true))
                    win.Close();
                else
                {
                    installBtn.Content = "Установить и запустить";
                    installBtn.IsEnabled = true;
                }
            };

            btnRow.Children.Add(cancelBtn);
            btnRow.Children.Add(installBtn);
            stack.Children.Add(btnRow);

            shell.Child = stack;
            win.Content = shell;
            return win;
        }

        private static Button CreateStyledButton(string text, Color bg, Color fg, double fontSize, Thickness padding, bool isPrimary = false)
        {
            var btn = new Button
            {
                Content = text,
                FontSize = fontSize,
                Foreground = new SolidColorBrush(fg),
                FontWeight = isPrimary ? FontWeights.SemiBold : FontWeights.Normal,
                Cursor = Cursors.Hand,
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                Padding = padding
            };

            var template = new ControlTemplate(typeof(Button));
            var fef = new FrameworkElementFactory(typeof(Border));
            fef.Name = "border";
            fef.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            fef.SetValue(Border.BackgroundProperty, new SolidColorBrush(bg));
            fef.SetValue(Border.PaddingProperty, padding);

            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            fef.AppendChild(cp);

            var triggerIsMouseOver = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
            Color hoverColor = isPrimary ? Color.FromRgb(192, 168, 255) : Color.FromRgb(50, 50, 60);
            triggerIsMouseOver.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(hoverColor), "border"));

            var triggerIsPressed = new Trigger { Property = Button.IsPressedProperty, Value = true };
            Color pressColor = isPrimary ? Color.FromRgb(140, 110, 230) : Color.FromRgb(28, 28, 34);
            triggerIsPressed.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(pressColor), "border"));

            template.VisualTree = fef;
            template.Triggers.Add(triggerIsMouseOver);
            template.Triggers.Add(triggerIsPressed);
            btn.Template = template;

            return btn;
        }

        private static bool PerformInstall(string installDir, bool createDesktopShortcut, bool enableAutoStart, bool launchNow, bool showErrors = true)
        {
            try
            {
                installDir = Path.GetFullPath(installDir);

                foreach (var p in Process.GetProcessesByName("WindowsDynamicIsland"))
                {
                    using (p)
                    {
                        if (p.HasExited) continue;
                        p.Kill();
                        if (!p.WaitForExit(1500))
                            throw new IOException("Не удалось завершить работающий Windows Dynamic Island.");
                    }
                }

                if (!Directory.Exists(installDir))
                {
                    Directory.CreateDirectory(installDir);
                }

                string exeTarget = Path.Combine(installDir, "WindowsDynamicIsland.exe");
                string icoTarget = Path.Combine(installDir, "app.ico");

                ExtractResource("Payload.app.ico", icoTarget);

                // Extract notification audio files out of the box
                string soundsDir = Path.Combine(installDir, "Assets", "Sounds");
                if (!Directory.Exists(soundsDir)) Directory.CreateDirectory(soundsDir);
                string[] soundFiles = new[] { "applepay.mp3", "phonk.mp3", "plum.mp3", "trum.mp3" };
                foreach (var sf in soundFiles)
                    ExtractResource("Payload.Sounds." + sf, Path.Combine(soundsDir, sf));
                ExtractResource("Payload.WindowsDynamicIsland.exe", exeTarget);

                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
                {
                    if (enableAutoStart) key.SetValue("WindowsDynamicIsland", $"\"{exeTarget}\"");
                    else key.DeleteValue("WindowsDynamicIsland", false);
                }

                if (createDesktopShortcut)
                {
                    string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                    CreateShortcut(Path.Combine(desktop, "Windows Dynamic Island.lnk"), exeTarget, installDir, icoTarget);

                    string startMenu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Windows Dynamic Island.lnk");
                    CreateShortcut(startMenu, exeTarget, installDir, icoTarget);
                }

                // Notify Windows Shell to instantly refresh icon cache for desktop and taskbar
                try
                {
                    SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
                }
                catch
                {
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
                return true;
            }
            catch (Exception ex)
            {
                if (showErrors)
                    MessageBox.Show("Ошибка установки: " + ex.Message, "Windows Dynamic Island", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private static void ExtractResource(string resourceName, string destPath)
        {
            string stagingPath = destPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (Stream source = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
                {
                    if (source == null) throw new FileNotFoundException(resourceName);
                    using (var file = new FileStream(stagingPath, FileMode.CreateNew))
                        source.CopyTo(file);
                }
                if (File.Exists(destPath)) File.Replace(stagingPath, destPath, null);
                else File.Move(stagingPath, destPath);
            }
            finally
            {
                if (File.Exists(stagingPath)) File.Delete(stagingPath);
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
                shortcut.IconLocation = iconPath + ",0";
                shortcut.Description = "Windows Dynamic Island — Центр управления Windows 11";
                shortcut.Save();
            }
            catch
            {
            }
        }
    }
}
