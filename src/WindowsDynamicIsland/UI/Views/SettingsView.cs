using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using WindowsDynamicIsland.Models;
using WindowsDynamicIsland.Services;
using CheckBox = System.Windows.Controls.CheckBox;
using Cursors = System.Windows.Input.Cursors;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Orientation = System.Windows.Controls.Orientation;
using UserControl = System.Windows.Controls.UserControl;

namespace WindowsDynamicIsland.UI.Views
{
    public sealed class SettingsView : UserControl
    {
        private readonly SettingsManager _settings;
        private readonly SystemIntegrationService _system;
        private readonly Action _onSettingsApplied;
        private ThemePalette _palette;
        private int _activeCategory; // 0 = Внешний вид, 1 = Поведение, 2 = Кастомизация и приватность
        private Grid _categoryHost;
        private Border[] _categoryButtons;

        public SettingsView(SettingsManager settings, SystemIntegrationService system, Action onSettingsApplied)
        {
            _settings = settings;
            _system = system;
            _onSettingsApplied = onSettingsApplied;
            RebuildTheme();
        }

        public void RebuildTheme()
        {
            _palette = ThemeTokens.GetPalette(_settings.Current.Theme, 1.0);

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // Top category bar + Reset Defaults button
            var topBar = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            topBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var tabsStack = new StackPanel { Orientation = Orientation.Horizontal };
            string[] cats = { "Внешний вид", "Поведение и экран", "Кастомизация и приватность" };
            _categoryButtons = new Border[cats.Length];

            for (int i = 0; i < cats.Length; i++)
            {
                int idx = i;
                var tb = new TextBlock
                {
                    Text = cats[i],
                    FontFamily = ThemeTokens.UiFont,
                    FontSize = 12,
                    FontWeight = FontWeights.Medium
                };
                var btn = new Border
                {
                    CornerRadius = new CornerRadius(8),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(0),
                    Margin = new Thickness(0, 0, 6, 0),
                    Cursor = Cursors.Hand,
                    SnapsToDevicePixels = true,
                    UseLayoutRounding = true,
                    Child = ThemeTokens.WrapContentWithGlassLayers(_palette, tb, 8, idx == _activeCategory, new Thickness(12, 6, 12, 6))
                };
                btn.MouseEnter += (s, e) =>
                {
                    if (_activeCategory != idx)
                    {
                        btn.Background = _palette.HoverBackground;
                        btn.BorderBrush = _palette.SubtleBorder;
                    }
                };
                btn.MouseLeave += (s, e) =>
                {
                    ApplyCategoryButtonStyle(idx);
                };
                btn.MouseLeftButtonDown += (s, e) => e.Handled = true;
                btn.MouseLeftButtonUp += (s, e) =>
                {
                    e.Handled = true;
                    SwitchCategory(idx);
                };

                _categoryButtons[i] = btn;
                tabsStack.Children.Add(btn);
            }

            Grid.SetColumn(tabsStack, 0);
            topBar.Children.Add(tabsStack);

            var resetBtn = ThemeTokens.CreateInteractiveButton(
                _palette,
                new TextBlock
                {
                    Text = "По умолчанию",
                    FontFamily = ThemeTokens.UiFont,
                    FontSize = 12,
                    Foreground = _palette.Warning
                },
                () =>
                {
                    _settings.ResetToDefaults();
                    _onSettingsApplied?.Invoke();
                    RebuildTheme();
                },
                false, false, 8, new Thickness(12, 6, 12, 6), "Восстановить все настройки по умолчанию");
            Grid.SetColumn(resetBtn, 2);
            topBar.Children.Add(resetBtn);

            Grid.SetRow(topBar, 0);
            root.Children.Add(topBar);

            _categoryHost = new Grid();
            Grid.SetRow(_categoryHost, 1);
            root.Children.Add(_categoryHost);

            Content = root;
            SwitchCategory(_activeCategory);
        }

        private void ApplyCategoryButtonStyle(int i)
        {
            if (_categoryButtons == null || i < 0 || i >= _categoryButtons.Length) return;
            bool active = i == _activeCategory;
            _categoryButtons[i].Background = active ? _palette.Accent : _palette.RaisedBackground;
            _categoryButtons[i].BorderBrush = active ? (_palette.IsLight ? _palette.Accent : _palette.SubtleBorder) : _palette.HairlineBorder;
            TextBlock tb = _categoryButtons[i].Child as TextBlock;
            if (tb == null && _categoryButtons[i].Child is Border b)
            {
                tb = b.Child as TextBlock;
            }
            if (tb == null && _categoryButtons[i].Child is Grid g && g.Children.Count > 1)
            {
                if (_palette.IsGlass)
                {
                    g.Children.RemoveAt(0);
                    g.Children.Insert(0, ThemeTokens.BuildIos26GlassOverlay(8, active));
                }
                if (g.Children[1] is Border innerB) tb = innerB.Child as TextBlock;
                else tb = g.Children[1] as TextBlock;
            }
            if (tb != null)
            {
                tb.Foreground = active ? _palette.OnAccentText : _palette.SecondaryText;
                tb.FontWeight = active ? FontWeights.SemiBold : FontWeights.Medium;
            }
        }

        public void SwitchToCategory(int index)
        {
            SwitchCategory(index);
        }

        private void SwitchCategory(int index)
        {
            _activeCategory = Math.Max(0, Math.Min(2, index));
            for (int i = 0; i < _categoryButtons.Length; i++)
            {
                ApplyCategoryButtonStyle(i);
            }

            _categoryHost.Children.Clear();
            if (_activeCategory == 0) _categoryHost.Children.Add(BuildAppearanceCategory());
            else if (_activeCategory == 1) _categoryHost.Children.Add(BuildBehaviorCategory());
            else _categoryHost.Children.Add(BuildCustomizationCategory());
        }

        // ==================== 1. ВНЕШНИЙ ВИД ====================
        private UIElement BuildAppearanceCategory()
        {
            var s = _settings.Current;
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Left Card: Theme, Opacity, Scale, Corner Radius, Position
            var leftCard = ThemeTokens.CreateCard(_palette, 12, new Thickness(14, 10, 14, 10));
            var leftStack = new StackPanel();

            leftStack.Children.Add(CreateSectionTitle("Тема оформления"));
            var themeRow = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 10) };
            AddChoicePill(themeRow, "Светлая", s.Theme == AppThemeMode.Light, () => { s.Theme = AppThemeMode.Light; ApplyAndRefresh(); });
            AddChoicePill(themeRow, "Тёмная (OLED)", s.Theme == AppThemeMode.Dark, () => { s.Theme = AppThemeMode.Dark; ApplyAndRefresh(); });
            AddChoicePill(themeRow, "iOS 26 Glass", s.Theme == AppThemeMode.Ios26Glass, () => { s.Theme = AppThemeMode.Ios26Glass; ApplyAndRefresh(); });
            AddChoicePill(themeRow, "Системная", s.Theme == AppThemeMode.System, () => { s.Theme = AppThemeMode.System; ApplyAndRefresh(); });
            leftStack.Children.Add(themeRow);

            leftStack.Children.Add(CreateSliderSetting(
                "Прозрачность капсулы",
                $"{(int)Math.Round(s.Opacity * 100)}%",
                55, 100, s.Opacity * 100,
                val => { s.Opacity = val / 100.0; SaveQuiet(); }));

            leftStack.Children.Add(CreateSliderSetting(
                "Масштаб острова",
                $"{(int)Math.Round(s.IslandScale * 100)}%",
                85, 130, s.IslandScale * 100,
                val => { s.IslandScale = val / 100.0; SaveQuiet(); }));

            leftStack.Children.Add(CreateSliderSetting(
                "Радиус скругления",
                $"{(int)s.CollapsedCornerRadius} px",
                10, 26, s.CollapsedCornerRadius,
                val =>
                {
                    s.CollapsedCornerRadius = Math.Round(val);
                    s.ExpandedCornerRadius = Math.Round(val + 2);
                    SaveQuiet();
                }));

            leftStack.Children.Add(CreateSliderSetting(
                "Отступ от верхнего края (0 = капля)",
                $"{(int)s.TopOffsetPx} px",
                0, 32, s.TopOffsetPx,
                val => { s.TopOffsetPx = Math.Round(val); SaveQuiet(); }));

            leftCard.Child = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = leftStack };
            Grid.SetColumn(leftCard, 0);
            grid.Children.Add(leftCard);

            // Right Card: Visible Collapsed Elements & Smiley Animation
            var rightCard = ThemeTokens.CreateCard(_palette, 12, new Thickness(14, 10, 14, 10));
            var rightStack = new StackPanel();

            rightStack.Children.Add(CreateSectionTitle("Элементы в спокойном состоянии"));
            rightStack.Children.Add(CreateToggleRow("Показывать смайлик ^_^", s.ShowSmileyInCollapsed, v => { s.ShowSmileyInCollapsed = v; SaveQuiet(); }));
            rightStack.Children.Add(CreateToggleRow("Анимация реакций смайлика", s.EnableSmileyAnimation, v => { s.EnableSmileyAnimation = v; SaveQuiet(); }));
            rightStack.Children.Add(CreateToggleRow("Показывать текущую погоду", s.ShowWeatherInCollapsed, v => { s.ShowWeatherInCollapsed = v; SaveQuiet(); }));
            rightStack.Children.Add(CreateToggleRow("Показывать центральные часы", s.ShowClockInCollapsed, v => { s.ShowClockInCollapsed = v; SaveQuiet(); }));
            rightStack.Children.Add(CreateToggleRow("Иконка быстрого открытия заметок", s.ShowNotesIconInCollapsed, v => { s.ShowNotesIconInCollapsed = v; SaveQuiet(); }));
            rightStack.Children.Add(CreateToggleRow("Показывать активный таймер", s.ShowTimerInCollapsed, v => { s.ShowTimerInCollapsed = v; SaveQuiet(); }));

            rightStack.Children.Add(CreateSliderSetting(
                "Смещение по горизонтали",
                $"{(int)s.HorizontalOffsetPx} px",
                -300, 300, s.HorizontalOffsetPx,
                val => { s.HorizontalOffsetPx = Math.Round(val); SaveQuiet(); }));

            rightCard.Child = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = rightStack };
            Grid.SetColumn(rightCard, 2);
            grid.Children.Add(rightCard);

            return grid;
        }

        // ==================== 2. ПОВЕДЕНИЕ И ЭКРАН ====================
        private UIElement BuildBehaviorCategory()
        {
            var s = _settings.Current;
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var leftCard = ThemeTokens.CreateCard(_palette, 12, new Thickness(14, 10, 14, 10));
            var leftStack = new StackPanel();

            leftStack.Children.Add(CreateSectionTitle("Раскрытие и уход за край экрана"));
            leftStack.Children.Add(CreateToggleRow("Прятать вверх за край в бездействии", s.AutoRetractOnIdle, v => { s.AutoRetractOnIdle = v; SaveQuiet(); }));
            leftStack.Children.Add(CreateSliderSetting(
                "Время бездействия до ухода вверх",
                $"{s.IdleRetractSeconds} сек",
                2, 15, s.IdleRetractSeconds,
                val => { s.IdleRetractSeconds = (int)Math.Round(val); SaveQuiet(); }));

            leftStack.Children.Add(CreateToggleRow("Раскрывать при наведении курсора", s.ExpandOnHover, v => { s.ExpandOnHover = v; SaveQuiet(); }));

            leftStack.Children.Add(CreateSliderSetting(
                "Задержка раскрытия (обычная)",
                $"{s.ExpandDelayMs} мс",
                0, 600, s.ExpandDelayMs,
                val => { s.ExpandDelayMs = (int)Math.Round(val); SaveQuiet(); }));

            leftStack.Children.Add(CreateSliderSetting(
                "Задержка раскрытия после выхода из-за края",
                $"{s.WakeExpandDelayMs} мс",
                200, 2000, s.WakeExpandDelayMs,
                val => { s.WakeExpandDelayMs = (int)Math.Round(val); SaveQuiet(); }));

            leftStack.Children.Add(CreateSliderSetting(
                "Скорость пружинной анимации",
                $"{s.AnimationSpeedMultiplier:0.0}x",
                0.5, 2.0, s.AnimationSpeedMultiplier,
                val => { s.AnimationSpeedMultiplier = Math.Round(val, 1); SaveQuiet(); }));

            leftCard.Child = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = leftStack };
            Grid.SetColumn(leftCard, 0);
            grid.Children.Add(leftCard);

            var rightCard = ThemeTokens.CreateCard(_palette, 12, new Thickness(14, 10, 14, 10));
            var rightStack = new StackPanel();

            rightStack.Children.Add(CreateSectionTitle("Интеграция с Windows 11"));
            rightStack.Children.Add(CreateToggleRow("Запускать вместе с Windows", s.AutoStartWithWindows, v => { s.AutoStartWithWindows = v; SaveQuiet(); }));
            rightStack.Children.Add(CreateToggleRow("Отображать системные уведомления", s.ShowSystemNotifications, v => { s.ShowSystemNotifications = v; SaveQuiet(); }));
            var notificationSettings = new System.Windows.Controls.Button
            {
                Content = "Доступ к уведомлениям Windows",
                Foreground = _palette.PrimaryText,
                Background = _palette.RaisedBackground,
                BorderBrush = _palette.HairlineBorder,
                Padding = new Thickness(9, 5, 9, 5),
                Margin = new Thickness(0, 4, 0, 8),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            notificationSettings.SetResourceReference(StyleProperty, "IslandPlayerButton");
            notificationSettings.Click += (sender, args) =>
            {
                try
                {
                    using (System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("ms-settings:privacy-notifications")
                        { UseShellExecute = true })) { }
                }
                catch (Exception ex) { StorageService.LogError("OpenNotificationSettings", ex); }
            };
            rightStack.Children.Add(notificationSettings);
            if (s.ShowSystemNotifications && !_system.NotificationsListenerActive)
            {
                rightStack.Children.Add(new TextBlock
                {
                    Text = "Windows не предоставила этой сборке доступ к уведомлениям.",
                    Foreground = _palette.SecondaryText,
                    FontSize = 11,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 8)
                });
            }
            rightStack.Children.Add(CreateToggleRow("Работать поверх полноэкранных окон", s.AllowOverFullscreenApps, v => { s.AllowOverFullscreenApps = v; SaveQuiet(); }));
            rightStack.Children.Add(CreateToggleRow("Плавно прятать в полноэкранных играх", s.HideInFullscreenApps, v => { s.HideInFullscreenApps = v; SaveQuiet(); }));

            // Monitor Selector
            rightStack.Children.Add(CreateSectionTitle("Выбор монитора"));
            var monRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 4) };
            var screens = Screen.AllScreens;
            for (int i = 0; i < screens.Length; i++)
            {
                int idx = i;
                AddChoicePill(monRow, $"Монитор {i + 1}", s.SelectedMonitorIndex == idx, () =>
                {
                    s.SelectedMonitorIndex = idx;
                    ApplyAndRefresh();
                });
            }
            rightStack.Children.Add(monRow);

            rightCard.Child = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = rightStack };
            Grid.SetColumn(rightCard, 2);
            grid.Children.Add(rightCard);

            return grid;
        }

        // ==================== 3. КАСТОМИЗАЦИЯ И ПРИВАТНОСТЬ ====================
        private UIElement BuildCustomizationCategory()
        {
            var s = _settings.Current;
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var leftCard = ThemeTokens.CreateCard(_palette, 12, new Thickness(14, 10, 14, 10));
            var leftStack = new StackPanel();

            leftStack.Children.Add(CreateSectionTitle("Стиль смайлика-компаньона"));
            var smileyRow = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 6) };
            AddChoicePill(smileyRow, "^_^", s.SmileyStyle == SmileyStylePreset.Classic, () => { s.SmileyStyle = SmileyStylePreset.Classic; ApplyAndRefresh(); });
            AddChoicePill(smileyRow, "•ᴗ•", s.SmileyStyle == SmileyStylePreset.Minimal, () => { s.SmileyStyle = SmileyStylePreset.Minimal; ApplyAndRefresh(); });
            AddChoicePill(smileyRow, "[•_•]", s.SmileyStyle == SmileyStylePreset.Cyber, () => { s.SmileyStyle = SmileyStylePreset.Cyber; ApplyAndRefresh(); });
            AddChoicePill(smileyRow, "=^..^=", s.SmileyStyle == SmileyStylePreset.Cat, () => { s.SmileyStyle = SmileyStylePreset.Cat; ApplyAndRefresh(); });
            AddChoicePill(smileyRow, "◕‿◕", s.SmileyStyle == SmileyStylePreset.Kawaii, () => { s.SmileyStyle = SmileyStylePreset.Kawaii; ApplyAndRefresh(); });
            leftStack.Children.Add(smileyRow);

            leftStack.Children.Add(CreateSectionTitle("Формат часов, погоды и звука"));
            leftStack.Children.Add(CreateToggleRow("24-часовой формат времени", s.ClockUse24HourFormat, v => { s.ClockUse24HourFormat = v; SaveQuiet(); }));
            leftStack.Children.Add(CreateToggleRow("Показывать секунды в часах", s.ClockShowSeconds, v => { s.ClockShowSeconds = v; SaveQuiet(); }));
            leftStack.Children.Add(CreateToggleRow("Температура в градусах Цельсия (°C)", s.WeatherUseCelsius, v => { s.WeatherUseCelsius = v; SaveQuiet(); }));
            leftStack.Children.Add(CreateToggleRow("Звуковой сигнал уведомлений", s.EnableNotificationSounds, v => { s.EnableNotificationSounds = v; ApplyAndRefresh(); }));

            if (s.EnableNotificationSounds)
            {
                var soundTitleRow = new Grid { Margin = new Thickness(0, 4, 0, 2) };
                soundTitleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                soundTitleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                soundTitleRow.Children.Add(new TextBlock
                {
                    Text = "Мелодия уведомлений",
                    FontFamily = ThemeTokens.UiFont,
                    FontSize = 11,
                    Foreground = _palette.SecondaryText,
                    VerticalAlignment = VerticalAlignment.Center
                });

                var playPreviewBtn = ThemeTokens.CreateInteractiveButton(
                    _palette,
                    new TextBlock
                    {
                        Text = "▶ Послушать",
                        FontFamily = ThemeTokens.UiFont,
                        FontSize = 10,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = _palette.Positive
                    },
                    () => NotificationSoundService.PlaySound(s.NotificationSoundPreset),
                    false, false, 5, new Thickness(7, 2, 7, 2), "Воспроизвести выбранную мелодию");
                Grid.SetColumn(playPreviewBtn, 1);
                soundTitleRow.Children.Add(playPreviewBtn);
                leftStack.Children.Add(soundTitleRow);

                var soundRow = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 4) };
                var presets = NotificationSoundService.GetAvailablePresets();
                foreach (var p in presets)
                {
                    string k = p.Key;
                    bool sel = string.Equals(s.NotificationSoundPreset, k, StringComparison.OrdinalIgnoreCase);
                    AddChoicePill(soundRow, p.DisplayName, sel, () =>
                    {
                        s.NotificationSoundPreset = k;
                        NotificationSoundService.PlaySound(k);
                        ApplyAndRefresh();
                    });
                }
                AddChoicePill(soundRow, "Без звука", string.Equals(s.NotificationSoundPreset, "none", StringComparison.OrdinalIgnoreCase), () =>
                {
                    s.NotificationSoundPreset = "none";
                    ApplyAndRefresh();
                });
                leftStack.Children.Add(soundRow);
            }

            leftCard.Child = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = leftStack };
            Grid.SetColumn(leftCard, 0);
            grid.Children.Add(leftCard);

            var rightCard = ThemeTokens.CreateCard(_palette, 12, new Thickness(14, 10, 14, 10));
            var rightStack = new StackPanel();

            rightStack.Children.Add(CreateSectionTitle("Приватность буфера обмена"));
            rightStack.Children.Add(CreateToggleRow("Мониторинг истории буфера", s.ClipboardMonitoringEnabled, v => { s.ClipboardMonitoringEnabled = v; SaveQuiet(); }));
            rightStack.Children.Add(CreateToggleRow("Не сохранять пароли и токены", s.ClipboardFilterSensitiveData, v => { s.ClipboardFilterSensitiveData = v; SaveQuiet(); }));

            rightStack.Children.Add(CreateSliderSetting(
                "Макс. размер истории буфера",
                $"{s.ClipboardMaxHistory} зап.",
                10, 200, s.ClipboardMaxHistory,
                val => { s.ClipboardMaxHistory = (int)Math.Round(val); SaveQuiet(); }));

            rightStack.Children.Add(CreateSectionTitle("Горячие клавиши"));
            rightStack.Children.Add(CreateKeybindRow("Показать / скрыть остров", s.HotkeyToggleIsland));
            rightStack.Children.Add(CreateKeybindRow("Быстрая заметка", s.HotkeyQuickNote));
            rightStack.Children.Add(CreateKeybindRow("Открыть буфер обмена", s.HotkeyClipboard));

            rightCard.Child = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = rightStack };
            Grid.SetColumn(rightCard, 2);
            grid.Children.Add(rightCard);

            return grid;
        }

        private UIElement CreateSectionTitle(string title)
        {
            return new TextBlock
            {
                Text = title,
                FontFamily = ThemeTokens.UiFont,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = _palette.SecondaryText,
                Margin = new Thickness(0, 4, 0, 4)
            };
        }

        private void AddChoicePill(System.Windows.Controls.Panel parent, string text, bool selected, Action onSelect)
        {
            var tb = new TextBlock
            {
                Text = text,
                FontFamily = ThemeTokens.UiFont,
                FontSize = 11.5,
                FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal,
                Foreground = selected ? _palette.OnAccentText : _palette.PrimaryText
            };
            var btn = ThemeTokens.CreateInteractiveButton(
                _palette,
                tb,
                onSelect,
                selected, false, 6, new Thickness(9, 4, 9, 4));
            btn.Margin = new Thickness(0, 0, 5, 4);
            parent.Children.Add(btn);
        }

        private UIElement CreateToggleRow(string label, bool isChecked, Action<bool> onChanged)
        {
            var grid = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            grid.Children.Add(new TextBlock
            {
                Text = label,
                FontFamily = ThemeTokens.UiFont,
                FontSize = 12,
                Foreground = _palette.PrimaryText,
                VerticalAlignment = VerticalAlignment.Center
            });

            var cb = new CheckBox
            {
                IsChecked = isChecked,
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand
            };
            cb.Checked += (s, e) => onChanged?.Invoke(true);
            cb.Unchecked += (s, e) => onChanged?.Invoke(false);
            Grid.SetColumn(cb, 1);
            grid.Children.Add(cb);

            return grid;
        }

        private UIElement CreateSliderSetting(string label, string initialValueText, double min, double max, double current, Action<double> onValueChanged)
        {
            var stack = new StackPanel { Margin = new Thickness(0, 4, 0, 6) };
            var top = new Grid();
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            top.Children.Add(new TextBlock
            {
                Text = label,
                FontFamily = ThemeTokens.UiFont,
                FontSize = 11.5,
                Foreground = _palette.PrimaryText
            });

            var valTb = new TextBlock
            {
                Text = initialValueText,
                FontFamily = ThemeTokens.MonoFont,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = _palette.PrimaryText
            };
            Grid.SetColumn(valTb, 1);
            top.Children.Add(valTb);
            stack.Children.Add(top);

            var slider = new Slider
            {
                Minimum = min,
                Maximum = max,
                Value = current,
                Margin = new Thickness(0, 2, 0, 0),
                IsMoveToPointEnabled = true,
                Cursor = Cursors.Hand
            };
            slider.ValueChanged += (s, e) =>
            {
                onValueChanged?.Invoke(e.NewValue);
                if (initialValueText.EndsWith("%")) valTb.Text = $"{(int)Math.Round(e.NewValue)}%";
                else if (initialValueText.EndsWith("мс")) valTb.Text = $"{(int)Math.Round(e.NewValue)} мс";
                else if (initialValueText.EndsWith("сек")) valTb.Text = $"{(int)Math.Round(e.NewValue)} сек";
                else if (initialValueText.EndsWith("px")) valTb.Text = $"{(int)Math.Round(e.NewValue)} px";
                else if (initialValueText.EndsWith("x")) valTb.Text = $"{e.NewValue:0.0}x";
                else valTb.Text = $"{(int)Math.Round(e.NewValue)}";
            };
            stack.Children.Add(slider);
            return stack;
        }

        private UIElement CreateKeybindRow(string label, string keyText)
        {
            var grid = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            grid.Children.Add(new TextBlock
            {
                Text = label,
                FontFamily = ThemeTokens.UiFont,
                FontSize = 11.5,
                Foreground = _palette.SecondaryText,
                VerticalAlignment = VerticalAlignment.Center
            });

            var cap = ThemeTokens.CreateKeycap(_palette, keyText, true);
            Grid.SetColumn(cap, 1);
            grid.Children.Add(cap);
            return grid;
        }

        private void SaveQuiet()
        {
            _settings.Save();
            _onSettingsApplied?.Invoke();
        }

        private void ApplyAndRefresh()
        {
            _settings.Save();
            _onSettingsApplied?.Invoke();
            RebuildTheme();
        }
    }
}
