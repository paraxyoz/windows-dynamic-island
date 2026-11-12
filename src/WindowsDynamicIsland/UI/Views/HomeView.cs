using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using WindowsDynamicIsland.Models;
using WindowsDynamicIsland.Services;
using WindowsDynamicIsland.Win32;

namespace WindowsDynamicIsland.UI.Views
{
    public sealed class HomeView : UserControl
    {
        private sealed class SearchCommandItem
        {
            public string Title { get; set; }
            public string Subtitle { get; set; }
            public string Category { get; set; }
            public string Keywords { get; set; }
            public Action Execute { get; set; }
        }

        private readonly SettingsManager _settings;
        private readonly MediaSessionService _media;
        private readonly SystemIntegrationService _sys;
        private readonly ClipboardManager _clipboard;
        private readonly FileShelfManager _shelf;
        private readonly NotesManager _notes;
        private readonly TimerService _timers;
        private readonly WeatherService _weather;
        private readonly Action<int> _navigateToTab;
        private readonly Action<int> _navigateToSettingsCategory;
        private readonly Action _onSettingsChanged;
        private readonly Func<IntPtr> _getHwnd;

        private ThemePalette _palette;

        // Search UI & Keyboard Navigation State
        private TextBox _searchBox;
        private StackPanel _searchResultsPanel;
        private Border _searchResultsPopup;
        private readonly List<Action> _currentResultActions = new List<Action>();
        private readonly List<Border> _currentResultRows = new List<Border>();
        private int _selectedSearchIndex = -1;

        private Image _albumImage;
        private Border _albumFallbackIcon;
        private TextBlock _trackTitleText;
        private TextBlock _trackArtistText;
        private TextBlock _timePositionText;
        private TextBlock _timeDurationText;
        private System.Windows.Shapes.Path _playPauseIcon;
        private Grid _dashboardDetails;
        private Border _lyricsCard;
        private TrackLyricsView _lyricsView;
        private Button _lyricsButton;
        private Slider _seekSlider;
        private TextBlock _seekStatus;
        private bool _showLyrics;
        private bool _updatingSeekSlider;
        private int? _dragTrackVersion;
        private int _renderedTrackVersion = -1;

        private Slider _volumeSlider;
        private TextBlock _volumeValueText;
        private Border _muteBtn;
        private System.Windows.Shapes.Path _muteIcon;
        private bool _updatingVolumeSlider;

        private TextBlock _layoutCodeText;
        private TextBlock _layoutNameText;
        private Border _capsBadge;
        private TextBlock _capsBadgeText;
        private Border _numBadge;
        private TextBlock _numBadgeText;

        private StackPanel _powerNormalPanel;
        private StackPanel _powerConfirmPanel;
        private StackPanel _powerCountdownPanel;
        private TextBlock _powerConfirmText;
        private TextBlock _powerCountdownText;
        private PowerActionKind _confirmingPowerKind = PowerActionKind.None;

        public HomeView(
            SettingsManager settings,
            MediaSessionService media,
            SystemIntegrationService sys,
            ClipboardManager clipboard,
            FileShelfManager shelf,
            NotesManager notes,
            TimerService timers,
            WeatherService weather,
            Action<int> navigateToTab,
            Func<IntPtr> getHwnd,
            Action<int> navigateToSettingsCategory = null,
            Action onSettingsChanged = null)
        {
            _settings = settings;
            _media = media;
            _sys = sys;
            _clipboard = clipboard;
            _shelf = shelf;
            _notes = notes;
            _timers = timers;
            _weather = weather;
            _navigateToTab = navigateToTab;
            _getHwnd = getHwnd;
            _navigateToSettingsCategory = navigateToSettingsCategory;
            _onSettingsChanged = onSettingsChanged;

            _media.MediaStateChanged += RefreshMediaUi;
            _media.TimelineChanged += RefreshTimelineUi;
            _sys.VolumeStateChanged += OnVolumeStateChanged;
            _sys.KeyboardStateChanged += RefreshKeyboardUi;
            _sys.PowerStateChanged += RefreshPowerUi;

            RebuildTheme();
        }

        public void FocusSearchBox()
        {
            _searchBox?.Focus();
        }

        public void RebuildTheme()
        {
            _palette = ThemeTokens.GetPalette(_settings.Current.Theme, 1.0);

            var root = new Grid { Margin = new Thickness(0) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Search Bar
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Main Grid

            // --- 1. Top Search Bar ---
            var searchBarContainer = BuildSearchBar();
            Grid.SetRow(searchBarContainer, 0);
            root.Children.Add(searchBarContainer);

            // --- 2. Asymmetric Main Dashboard Grid ---
            var contentGrid = new Grid { Margin = new Thickness(0, 8, 0, 0) };
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.22, GridUnitType.Star) });
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });

            // Left Column: Rich Media Session Player + System Audio Mixer
            var leftGrid = new Grid();
            leftGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            leftGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) });
            leftGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var mediaCard = BuildMediaPlayerCard();
            Grid.SetRow(mediaCard, 0);
            leftGrid.Children.Add(mediaCard);

            var volumeCard = BuildVolumeCard();
            Grid.SetRow(volumeCard, 2);
            leftGrid.Children.Add(volumeCard);

            Grid.SetColumn(leftGrid, 0);
            contentGrid.Children.Add(leftGrid);

            // Right Column: Keyboard Layout & Locks + Power Management & AI Watcher
            var rightGrid = _dashboardDetails = new Grid();
            rightGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rightGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) });
            rightGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var kbCard = BuildKeyboardCard();
            Grid.SetRow(kbCard, 0);
            rightGrid.Children.Add(kbCard);

            var powerCard = BuildPowerAndAiCard();
            Grid.SetRow(powerCard, 2);
            rightGrid.Children.Add(powerCard);

            Grid.SetColumn(rightGrid, 2);
            contentGrid.Children.Add(rightGrid);

            _lyricsView = new TrackLyricsView(_media, _palette);
            var lyricsLayout = new DockPanel();
            var lyricsTitle = new TextBlock
            {
                Text = "Текст песни",
                FontFamily = ThemeTokens.UiFont,
                FontSize = 12,
                Foreground = _palette.SecondaryText,
                Margin = new Thickness(0, 0, 0, 12)
            };
            DockPanel.SetDock(lyricsTitle, Dock.Top);
            lyricsLayout.Children.Add(lyricsTitle);
            lyricsLayout.Children.Add(_lyricsView);
            _lyricsCard = ThemeTokens.CreateCard(_palette, 14, new Thickness(16, 13, 10, 12));
            _lyricsCard.Child = lyricsLayout;
            Grid.SetColumn(_lyricsCard, 2);
            contentGrid.Children.Add(_lyricsCard);
            UpdateLyricsVisibility();

            Grid.SetRow(contentGrid, 1);
            root.Children.Add(contentGrid);

            // Overlay for search results
            _searchResultsPanel = new StackPanel();
            _searchResultsPopup = new Border
            {
                Background = _palette.IsGlass ? ThemeTokens.MakeBrush(255, 31, 34, 43) : _palette.CardBackground,
                BorderBrush = _palette.HairlineBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(8),
                Margin = new Thickness(0, 4, 0, 0),
                Visibility = Visibility.Collapsed,
                MaxHeight = 276,
                VerticalAlignment = VerticalAlignment.Top,
                Child = new ScrollViewer
                {
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Content = _searchResultsPanel
                }
            };
            Grid.SetRow(_searchResultsPopup, 1);
            Panel.SetZIndex(_searchResultsPopup, 50);
            root.Children.Add(_searchResultsPopup);

            Content = root;

            RefreshMediaUi();
            RefreshVolumeUi(_sys.Audio.GetVolumePercent(), _sys.Audio.GetMuted());
            RefreshKeyboardUi();
            RefreshPowerUi();
        }

        private UIElement BuildSearchBar()
        {
            var bar = new Border
            {
                Background = _palette.CardBackground,
                BorderBrush = _palette.HairlineBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, 5, 8, 5)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var icon = VectorIcons.Create(VectorIcons.Search, _palette.MutedText, 14);
            icon.Margin = new Thickness(0, 0, 8, 0);
            Grid.SetColumn(icon, 0);
            grid.Children.Add(icon);

            var inputHost = ThemeTokens.CreateInputWithPlaceholder(
                _palette,
                "Поиск: громкость, автозагрузка, тема, секундомер, заметки...",
                out _searchBox,
                12,
                false);

            _searchBox.TextChanged += (s, e) => UpdateSearchResults(_searchBox.Text);
            _searchBox.PreviewKeyDown += OnSearchBoxPreviewKeyDown;

            Grid.SetColumn(inputHost, 1);
            grid.Children.Add(inputHost);

            var hintRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var keycap = ThemeTokens.CreateInteractiveButton(
                _palette,
                new TextBlock
                {
                    Text = "Команды",
                    FontFamily = ThemeTokens.UiFont,
                    FontSize = 10.5,
                    Foreground = _palette.SecondaryText
                },
                () =>
                {
                    if (_searchResultsPopup.Visibility == Visibility.Visible && string.IsNullOrEmpty(_searchBox.Text))
                    {
                        _searchResultsPopup.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        ShowSuggestedCommands();
                        _searchBox.Focus();
                    }
                },
                false, false, 5, new Thickness(7, 2, 7, 2), "Показать список быстрых команд и разделов");
            hintRow.Children.Add(keycap);

            Grid.SetColumn(hintRow, 2);
            grid.Children.Add(hintRow);

            bar.Child = grid;
            return bar;
        }

        private void OnSearchBoxPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                _searchBox.Text = string.Empty;
                _searchResultsPopup.Visibility = Visibility.Collapsed;
                Keyboard.ClearFocus();
                e.Handled = true;
                return;
            }

            if (_searchResultsPopup.Visibility != Visibility.Visible || _currentResultActions.Count == 0)
            {
                if (e.Key == Key.Down)
                {
                    ShowSuggestedCommands();
                    e.Handled = true;
                }
                return;
            }

            if (e.Key == Key.Down)
            {
                _selectedSearchIndex = (_selectedSearchIndex + 1) % _currentResultActions.Count;
                UpdateSearchSelectionVisuals();
                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                _selectedSearchIndex = (_selectedSearchIndex - 1 + _currentResultActions.Count) % _currentResultActions.Count;
                UpdateSearchSelectionVisuals();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                int idx = _selectedSearchIndex >= 0 && _selectedSearchIndex < _currentResultActions.Count ? _selectedSearchIndex : 0;
                var action = _currentResultActions[idx];
                _searchBox.Text = string.Empty;
                _searchResultsPopup.Visibility = Visibility.Collapsed;
                action?.Invoke();
                e.Handled = true;
            }
        }

        private void UpdateSearchSelectionVisuals()
        {
            for (int i = 0; i < _currentResultRows.Count; i++)
            {
                bool sel = i == _selectedSearchIndex;
                _currentResultRows[i].Background = sel ? _palette.HoverBackground : _palette.RaisedBackground;
                _currentResultRows[i].BorderBrush = sel ? _palette.Accent : _palette.HairlineBorder;
                if (sel)
                {
                    _currentResultRows[i].BringIntoView();
                }
            }
        }

        private List<SearchCommandItem> BuildComprehensiveRegistry()
        {
            var list = new List<SearchCommandItem>
            {
                // --- АУДИО И ГРОМКОСТЬ ---
                new SearchCommandItem
                {
                    Title = _sys.Audio.GetMuted() ? "Включить системный звук (Unmute)" : "Отключить системный звук (Mute)",
                    Subtitle = $"Текущая громкость: {_sys.Audio.GetVolumePercent()}%",
                    Category = "ЗВУК",
                    Keywords = "громкость звук тишина динамики микшер аудио выключить звук mute unmute volume audio sound speaker",
                    Execute = () =>
                    {
                        _sys.Audio.ToggleMute();
                        RefreshVolumeUi(_sys.Audio.GetVolumePercent(), _sys.Audio.GetMuted());
                    }
                },
                new SearchCommandItem
                {
                    Title = "Установить громкость 50%",
                    Subtitle = "Сбалансированный уровень системной громкости",
                    Category = "ЗВУК",
                    Keywords = "громкость 50 звук тише громче уровень volume 50%",
                    Execute = () =>
                    {
                        _sys.Audio.SetVolumePercent(50);
                        RefreshVolumeUi(50, _sys.Audio.GetMuted());
                    }
                },
                new SearchCommandItem
                {
                    Title = "Установить громкость 100%",
                    Subtitle = "Максимальная системная громкость",
                    Category = "ЗВУК",
                    Keywords = "громкость 100 максимум звук громче volume max 100%",
                    Execute = () =>
                    {
                        _sys.Audio.SetVolumePercent(100);
                        RefreshVolumeUi(100, _sys.Audio.GetMuted());
                    }
                },

                // --- МЕДИАПЛЕЕР ---
                new SearchCommandItem
                {
                    Title = _media.IsPlaying ? "Поставить музыку на паузу" : "Воспроизвести музыку (Play)",
                    Subtitle = $"Плеер Windows: {_media.Title}",
                    Category = "ПЛЕЕР",
                    Keywords = "музыка плеер пауза старт играть трек песня воспроизведение music player play pause audio",
                    Execute = async () => await _media.TogglePlayPauseAsync()
                },
                new SearchCommandItem
                {
                    Title = "Следующий трек",
                    Subtitle = "Переключить композицию вперёд",
                    Category = "ПЛЕЕР",
                    Keywords = "следующий трек песня вперёд музыка next track skip",
                    Execute = async () => await _media.SkipNextAsync()
                },
                new SearchCommandItem
                {
                    Title = "Предыдущий трек",
                    Subtitle = "Вернуться к предыдущей композиции",
                    Category = "ПЛЕЕР",
                    Keywords = "предыдущий трек песня назад музыка previous prev track",
                    Execute = async () => await _media.SkipPreviousAsync()
                },

                // --- КЛАВИАТУРА И РАСКЛАДКА ---
                new SearchCommandItem
                {
                    Title = $"Переключить раскладку клавиатуры ({_sys.CurrentKeyboardLayoutShort})",
                    Subtitle = $"Текущий язык: {_sys.CurrentKeyboardLayoutFull}",
                    Category = "ВВОД",
                    Keywords = "раскладка клавиатура язык переключить русский английский ru en keyboard layout language",
                    Execute = () => _sys.SwitchKeyboardLayoutAndNotify(_getHwnd())
                },
                new SearchCommandItem
                {
                    Title = _sys.IsCapsLockOn ? "Выключить Caps Lock" : "Включить Caps Lock",
                    Subtitle = "Переключение верхнего регистра клавиатуры",
                    Category = "ВВОД",
                    Keywords = "капс лок заглавные буквы регистр клавиатура caps lock uppercase",
                    Execute = () => _sys.ToggleCapsLockAndNotify()
                },

                // --- УПРАВЛЕНИЕ ПИТАНИЕМ ---
                new SearchCommandItem
                {
                    Title = "Перезагрузка компьютера",
                    Subtitle = "Безопасная перезагрузка с подтверждением и таймером отмены",
                    Category = "ПИТАНИЕ",
                    Keywords = "перезагрузка перезагрузить рестарт компьютер пк питание restart reboot power",
                    Execute = () => PromptPowerConfirmation(PowerActionKind.Restart)
                },
                new SearchCommandItem
                {
                    Title = "Выключение компьютера",
                    Subtitle = "Завершение работы Windows с подтверждением и таймером отмены",
                    Category = "ПИТАНИЕ",
                    Keywords = "выключение выключить компьютер пк питание завершение работы shutdown power off",
                    Execute = () => PromptPowerConfirmation(PowerActionKind.Shutdown)
                },

                // --- ЧАСЫ, ТАЙМЕР И СЕКУНДОМЕР ---
                new SearchCommandItem
                {
                    Title = "Секундомер",
                    Subtitle = _timers != null && _timers.IsStopwatchRunning ? $"Запущен: {_timers.FormattedStopwatch}" : "Открыть секундомер и фиксацию кругов",
                    Category = "ЧАСЫ",
                    Keywords = "секундомер круги время отсчёт часы старт stopwatch laps clock",
                    Execute = () => _navigateToTab?.Invoke(3)
                },
                new SearchCommandItem
                {
                    Title = _timers != null && _timers.IsStopwatchRunning ? "Остановить секундомер" : "Запустить секундомер",
                    Subtitle = "Мгновенный старт или пауза секундомера",
                    Category = "ЧАСЫ",
                    Keywords = "секундомер запустить старт пауза stopwatch start pause",
                    Execute = () =>
                    {
                        if (_timers != null)
                        {
                            if (_timers.IsStopwatchRunning) _timers.PauseStopwatch();
                            else _timers.StartStopwatch();
                        }
                        _navigateToTab?.Invoke(3);
                    }
                },
                new SearchCommandItem
                {
                    Title = "Таймер обратного отсчёта",
                    Subtitle = _timers != null && _timers.IsTimerActive ? $"Осталось: {_timers.FormattedTimerRemaining}" : "Открыть настройку и запуск таймера",
                    Category = "ЧАСЫ",
                    Keywords = "таймер обратный отсчёт время часы будильник минуты timer countdown alarm",
                    Execute = () => _navigateToTab?.Invoke(3)
                },
                new SearchCommandItem
                {
                    Title = "Запустить таймер на 5 минут",
                    Subtitle = "Быстрый старт таймера с отображением в капсуле",
                    Category = "ЧАСЫ",
                    Keywords = "таймер 5 минут быстрый старт timer 5m",
                    Execute = () =>
                    {
                        if (_timers != null)
                        {
                            _timers.ResetTimer();
                            _timers.SetTimerDuration(0, 5, 0);
                            _timers.StartTimer();
                        }
                    }
                },

                // --- РАЗДЕЛЫ DYNAMIC ISLAND ---
                new SearchCommandItem
                {
                    Title = "Буфер обмена (История)",
                    Subtitle = "Просмотр скопированного текста, поиск и перетаскивание",
                    Category = "РАЗДЕЛ",
                    Keywords = "буфер обмена история скопировать текст вставка clipboard copy history paste",
                    Execute = () => _navigateToTab?.Invoke(1)
                },
                new SearchCommandItem
                {
                    Title = "Файловая полка",
                    Subtitle = "Быстрый доступ к файлам и перетаскивание без перемещения оригиналов",
                    Category = "РАЗДЕЛ",
                    Keywords = "полка файлы документы ярлыки перетащить проводник shelf files drag drop",
                    Execute = () => _navigateToTab?.Invoke(2)
                },
                new SearchCommandItem
                {
                    Title = "Заметки и напоминания",
                    Subtitle = "Быстрые заметки с автосохранением и напоминаниями ко времени",
                    Category = "РАЗДЕЛ",
                    Keywords = "заметки блокнот текст запись напоминание создать заметку notes reminder",
                    Execute = () => _navigateToTab?.Invoke(3)
                },
                new SearchCommandItem
                {
                    Title = "Создать новую заметку",
                    Subtitle = "Мгновенно открыть чистую заметку для записи",
                    Category = "ЗАМЕТКА",
                    Keywords = "создать новую заметку блокнот добавить запись new note add",
                    Execute = () =>
                    {
                        _notes?.CreateNote("Новая заметка", "");
                        _navigateToTab?.Invoke(3);
                    }
                },
                new SearchCommandItem
                {
                    Title = "Погода и прогноз на 7 дней",
                    Subtitle = "Текущая температура, почасовой и недельный прогноз",
                    Category = "РАЗДЕЛ",
                    Keywords = "погода температура прогноз осадки ветер влажность город weather forecast temperature",
                    Execute = () => _navigateToTab?.Invoke(4)
                },

                // --- ВСЕ НАСТРОЙКИ (ТЕМА, АВТОЗАГРУЗКА, МАСШТАБ, УВЕДОМЛЕНИЯ, ПРИВАТНОСТЬ, ИИ) ---
                new SearchCommandItem
                {
                    Title = "Тема оформления: Светлая / Тёмная / Системная",
                    Subtitle = $"Текущая тема: {(_settings.Current.Theme == AppThemeMode.Light ? "Светлая" : (_settings.Current.Theme == AppThemeMode.Dark ? "Тёмная" : "Системная"))}",
                    Category = "НАСТРОЙКА",
                    Keywords = "тема оформления цвет светлая тёмная системная внешний вид палитра theme light dark appearance color",
                    Execute = () =>
                    {
                        _navigateToSettingsCategory?.Invoke(0);
                        _navigateToTab?.Invoke(5);
                    }
                },
                new SearchCommandItem
                {
                    Title = "Включить светлую тему (Premium Light)",
                    Subtitle = "Переключить интерфейс на светлую палитру",
                    Category = "НАСТРОЙКА",
                    Keywords = "светлая тема белый серый включить светлую light theme mode",
                    Execute = () =>
                    {
                        _settings.Current.Theme = AppThemeMode.Light;
                        _settings.Save();
                        _onSettingsChanged?.Invoke();
                    }
                },
                new SearchCommandItem
                {
                    Title = "Включить тёмную тему (Graphite Dark)",
                    Subtitle = "Переключить интерфейс на графитовую тёмную палитру",
                    Category = "НАСТРОЙКА",
                    Keywords = "тёмная тема темная графит чёрный dark theme mode",
                    Execute = () =>
                    {
                        _settings.Current.Theme = AppThemeMode.Dark;
                        _settings.Save();
                        _onSettingsChanged?.Invoke();
                    }
                },
                new SearchCommandItem
                {
                    Title = "Автозагрузка вместе с Windows",
                    Subtitle = _settings.Current.AutoStartWithWindows ? "Сейчас: ВКЛЮЧЕНА (нажмите для настройки)" : "Сейчас: ВЫКЛЮЧЕНА (нажмите для настройки)",
                    Category = "НАСТРОЙКА",
                    Keywords = "автозагрузка автозапуск старт вместе с windows реестр запуск startup autorun boot",
                    Execute = () =>
                    {
                        _navigateToSettingsCategory?.Invoke(1);
                        _navigateToTab?.Invoke(5);
                    }
                },
                new SearchCommandItem
                {
                    Title = "Масштаб, прозрачность и скругление острова",
                    Subtitle = $"Масштаб: {(int)(_settings.Current.IslandScale * 100)}%, Радиус: {(int)_settings.Current.CollapsedCornerRadius} px",
                    Category = "НАСТРОЙКА",
                    Keywords = "масштаб размер прозрачность радиус скругление отступ позиция ширина высота scale zoom opacity radius size",
                    Execute = () =>
                    {
                        _navigateToSettingsCategory?.Invoke(0);
                        _navigateToTab?.Invoke(5);
                    }
                },
                new SearchCommandItem
                {
                    Title = "Настройки уведомлений и анимаций",
                    Subtitle = "Задержка раскрытия, скорость пружины, системные уведомления и звук",
                    Category = "НАСТРОЙКА",
                    Keywords = "уведомления анимация скорость задержка наведение курсор полноэкранный игры монитор экран notifications animation hover fullscreen monitor",
                    Execute = () =>
                    {
                        _navigateToSettingsCategory?.Invoke(1);
                        _navigateToTab?.Invoke(5);
                    }
                },
                new SearchCommandItem
                {
                    Title = "Стиль смайлика ^_^ и формат часов",
                    Subtitle = "Выбор пресета смайлика, 24-часовой формат, секунды и единицы температуры",
                    Category = "НАСТРОЙКА",
                    Keywords = "смайлик компаньон часы секунды цельсий фаренгейт горячие клавиши хоткеи smiley clock hotkeys",
                    Execute = () =>
                    {
                        _navigateToSettingsCategory?.Invoke(2);
                        _navigateToTab?.Invoke(5);
                    }
                },
                new SearchCommandItem
                {
                    Title = "Приватность буфера и фильтрация паролей",
                    Subtitle = "Исключение паролей/токенов и лимит истории буфера обмена",
                    Category = "НАСТРОЙКА",
                    Keywords = "приватность безопасность пароли токены фильтр история буфера очистка privacy password filter",
                    Execute = () =>
                    {
                        _navigateToSettingsCategory?.Invoke(2);
                        _navigateToTab?.Invoke(5);
                    }
                },
                new SearchCommandItem
                {
                    Title = "Авто-скрытие за верхний край в бездействии",
                    Subtitle = _settings.Current.AutoRetractOnIdle ? "Сейчас: ВКЛЮЧЕНО (остров прячется вверх, не мешая кнопкам)" : "Сейчас: ВЫКЛЮЧЕНО",
                    Category = "ПОВЕДЕНИЕ",
                    Keywords = "автоскрытие спрятать прятать бездействие верхний край мешает кнопки капля retract idle hide edge",
                    Execute = () =>
                    {
                        _settings.Current.AutoRetractOnIdle = !_settings.Current.AutoRetractOnIdle;
                        _settings.Save();
                        _onSettingsChanged?.Invoke();
                    }
                },
                new SearchCommandItem
                {
                    Title = "Сбросить настройки по умолчанию",
                    Subtitle = "Восстановить исходные параметры внешнего вида и поведения",
                    Category = "НАСТРОЙКА",
                    Keywords = "сброс по умолчанию восстановить настройки стандартные reset defaults",
                    Execute = () =>
                    {
                        _settings.ResetToDefaults();
                        _onSettingsChanged?.Invoke();
                    }
                }
            };

            return list;
        }

        private void ShowSuggestedCommands()
        {
            _searchResultsPanel.Children.Clear();
            _currentResultActions.Clear();
            _currentResultRows.Clear();
            _selectedSearchIndex = -1;

            var registry = BuildComprehensiveRegistry();
            foreach (var cmd in registry.Take(8))
            {
                var c = cmd;
                AddSearchResultRow(c.Title, c.Subtitle, c.Category, () =>
                {
                    _searchBox.Text = string.Empty;
                    _searchResultsPopup.Visibility = Visibility.Collapsed;
                    c.Execute?.Invoke();
                });
            }

            if (_currentResultRows.Count > 0)
            {
                _selectedSearchIndex = 0;
                UpdateSearchSelectionVisuals();
            }

            _searchResultsPopup.Visibility = Visibility.Visible;
        }

        private void UpdateSearchResults(string query)
        {
            _searchResultsPanel.Children.Clear();
            _currentResultActions.Clear();
            _currentResultRows.Clear();
            _selectedSearchIndex = -1;

            if (string.IsNullOrWhiteSpace(query))
            {
                _searchResultsPopup.Visibility = Visibility.Collapsed;
                return;
            }

            string q = query.Trim();
            string[] tokens = q.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            int count = 0;

            // 1. Search Comprehensive Command & Settings Registry
            foreach (var cmd in BuildComprehensiveRegistry())
            {
                string haystack = $"{cmd.Title} {cmd.Subtitle} {cmd.Category} {cmd.Keywords}";
                bool match = tokens.All(t => haystack.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0);
                if (match)
                {
                    var c = cmd;
                    AddSearchResultRow(c.Title, c.Subtitle, c.Category, () =>
                    {
                        _searchBox.Text = string.Empty;
                        _searchResultsPopup.Visibility = Visibility.Collapsed;
                        c.Execute?.Invoke();
                    });
                    count++;
                }
            }

            // 2. Search Weather Cities
            if (_weather != null)
            {
                foreach (var city in _weather.Cities)
                {
                    if (city.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        "погода город weather".IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        var snap = _weather.GetWeatherForCity(city.Id);
                        string sub = snap != null
                            ? $"{_weather.FormatTemperature(snap.CurrentTemp)} • {snap.ConditionDescription}"
                            : "Открыть прогноз погоды";
                        AddSearchResultRow($"Погода: {city.Name}", sub, "ПОГОДА", () =>
                        {
                            _searchBox.Text = string.Empty;
                            _searchResultsPopup.Visibility = Visibility.Collapsed;
                            _navigateToTab?.Invoke(4);
                        });
                        count++;
                    }
                }
            }

            // 3. Search Saved Notes
            foreach (var note in _notes.Search(q).Take(4))
            {
                var n = note;
                AddSearchResultRow(n.DisplayTitle, n.Content, "ЗАМЕТКА", () =>
                {
                    _searchBox.Text = string.Empty;
                    _searchResultsPopup.Visibility = Visibility.Collapsed;
                    _navigateToTab?.Invoke(3);
                });
                count++;
            }

            // 4. Search Shelf Files
            foreach (var file in _shelf.Search(q).Take(4))
            {
                var f = file;
                AddSearchResultRow(f.FileName, $"{f.FormattedSize} • {f.FilePath}", "ПОЛКА", () =>
                {
                    _searchBox.Text = string.Empty;
                    _searchResultsPopup.Visibility = Visibility.Collapsed;
                    _navigateToTab?.Invoke(2);
                });
                count++;
            }

            // 5. Search Clipboard History
            foreach (var clip in _clipboard.Search(q).Take(4))
            {
                var c = clip;
                AddSearchResultRow(c.SingleLinePreview, $"Скопировать снова ({c.FormattedTime})", "БУФЕР", () =>
                {
                    _clipboard.CopyEntryToSystemClipboard(c);
                    _searchBox.Text = string.Empty;
                    _searchResultsPopup.Visibility = Visibility.Collapsed;
                });
                count++;
            }

            if (count == 0)
            {
                _searchResultsPanel.Children.Add(new TextBlock
                {
                    Text = "Ничего не найдено по запросу «" + q + "»",
                    FontFamily = ThemeTokens.UiFont,
                    FontSize = 11.5,
                    Foreground = _palette.MutedText,
                    Margin = new Thickness(6, 4, 6, 4)
                });
            }
            else
            {
                _selectedSearchIndex = 0;
                UpdateSearchSelectionVisuals();
            }

            _searchResultsPopup.Visibility = Visibility.Visible;
        }

        private void AddSearchResultRow(string title, string subtitle, string badge, Action onSelect)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = title,
                FontFamily = ThemeTokens.UiFont,
                FontSize = 12,
                FontWeight = FontWeights.Medium,
                Foreground = _palette.PrimaryText,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            if (!string.IsNullOrWhiteSpace(subtitle))
            {
                stack.Children.Add(new TextBlock
                {
                    Text = subtitle.Replace("\r\n", " ").Replace("\n", " "),
                    FontFamily = ThemeTokens.UiFont,
                    FontSize = 10.5,
                    Foreground = _palette.MutedText,
                    TextTrimming = TextTrimming.CharacterEllipsis
                });
            }

            Grid.SetColumn(stack, 0);
            grid.Children.Add(stack);

            var tag = ThemeTokens.CreateKeycap(_palette, badge, false);
            tag.VerticalAlignment = VerticalAlignment.Center;
            tag.Margin = new Thickness(8, 0, 0, 0);
            Grid.SetColumn(tag, 1);
            grid.Children.Add(tag);

            var row = ThemeTokens.CreateInteractiveButton(_palette, grid, onSelect, false, false, 6, new Thickness(8, 5, 8, 5));
            row.Margin = new Thickness(0, 0, 0, 3);
            _currentResultActions.Add(onSelect);
            _currentResultRows.Add(row);
            _searchResultsPanel.Children.Add(row);
        }

        private UIElement BuildMediaPlayerCard()
        {
            var card = ThemeTokens.CreateCard(_palette, 12, new Thickness(14, 11, 14, 11));
            var layoutGrid = new Grid { VerticalAlignment = VerticalAlignment.Stretch };
            layoutGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Row 0: Header + Live Status Pill
            layoutGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Row 1: Artwork + Metadata
            layoutGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Row 2: Progress + Timestamps
            layoutGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Row 3: Full-width Transport Bar

            // --- Row 0: Top Header & Live Status Badge ---
            var headerRow = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var headerTitleStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            headerTitleStack.Children.Add(VectorIcons.Create(VectorIcons.Music, _palette.SecondaryText, 12.5));
            headerTitleStack.Children.Add(new TextBlock
            {
                Text = "Плеер",
                FontFamily = ThemeTokens.UiFont,
                FontSize = 11.5,
                FontWeight = FontWeights.Medium,
                Foreground = _palette.SecondaryText,
                Margin = new Thickness(6, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            Grid.SetColumn(headerTitleStack, 0);
            headerRow.Children.Add(headerTitleStack);

            _lyricsButton = new Button
            {
                Content = "Текст песни",
                Foreground = _palette.PrimaryText,
                Background = _palette.RaisedBackground,
                BorderBrush = _palette.HairlineBorder,
                FontFamily = ThemeTokens.UiFont,
                FontSize = 11.5,
                Padding = new Thickness(9, 3, 9, 3),
                ToolTip = "Поиск текста в LRCLIB по названию и исполнителю"
            };
            _lyricsButton.SetResourceReference(StyleProperty, "IslandPlayerButton");
            _lyricsButton.Click += (s, e) =>
            {
                _showLyrics = !_showLyrics;
                UpdateLyricsVisibility();
            };
            Grid.SetColumn(_lyricsButton, 1);
            headerRow.Children.Add(_lyricsButton);
            Grid.SetRow(headerRow, 0);
            layoutGrid.Children.Add(headerRow);

            // --- Row 1: 60x60 Cover Art + Track Info ---
            var topGrid = new Grid { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 2, 0, 6) };
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var coverBox = new Border
            {
                Width = 60,
                Height = 60,
                CornerRadius = new CornerRadius(10),
                Background = _palette.RaisedBackground,
                BorderBrush = _palette.HairlineBorder,
                BorderThickness = new Thickness(1),
                ClipToBounds = true
            };

            var coverGrid = new Grid();
            _albumFallbackIcon = new Border
            {
                Child = VectorIcons.Create(VectorIcons.Music, _palette.SecondaryText, 22)
            };
            _albumImage = new Image
            {
                Stretch = Stretch.UniformToFill,
                Visibility = Visibility.Collapsed
            };
            coverGrid.SizeChanged += (s, e) =>
            {
                if (e.NewSize.Width > 0 && e.NewSize.Height > 0)
                    coverGrid.Clip = new RectangleGeometry(new Rect(e.NewSize), 9, 9);
            };
            coverGrid.Children.Add(_albumFallbackIcon);
            coverGrid.Children.Add(_albumImage);
            coverBox.Child = coverGrid;
            Grid.SetColumn(coverBox, 0);
            topGrid.Children.Add(coverBox);

            var metaStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            _trackTitleText = new TextBlock
            {
                Text = _media.Title,
                FontFamily = ThemeTokens.UiFont,
                FontSize = 13.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = _palette.PrimaryText,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            metaStack.Children.Add(_trackTitleText);

            _trackArtistText = new TextBlock
            {
                Text = _media.Artist,
                FontFamily = ThemeTokens.UiFont,
                FontSize = 12,
                Foreground = _palette.SecondaryText,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 2, 0, 0)
            };
            metaStack.Children.Add(_trackArtistText);

            Grid.SetColumn(metaStack, 2);
            topGrid.Children.Add(metaStack);
            Grid.SetRow(topGrid, 1);
            layoutGrid.Children.Add(topGrid);

            // --- Row 2: Progress Bar + Timestamps ---
            var timelineStack = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
            _seekSlider = new Slider
            {
                IsMoveToPointEnabled = true,
                SmallChange = 5,
                LargeChange = 15,
                Maximum = 0,
                Margin = new Thickness(0, 0, 0, 3)
            };
            System.Windows.Automation.AutomationProperties.SetName(_seekSlider, "Позиция воспроизведения, секунды");
            ToolTipService.SetShowOnDisabled(_seekSlider, true);
            _seekSlider.AddHandler(Thumb.DragStartedEvent, new DragStartedEventHandler((s, e) =>
            {
                _dragTrackVersion = _media.TrackVersion;
            }));
            _seekSlider.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler(async (s, e) =>
            {
                int? track = _dragTrackVersion;
                _dragTrackVersion = null;
                if (!e.Canceled && track == _media.TrackVersion)
                    await SeekFromSliderAsync();
                else
                    RefreshTimelineUi();
            }));
            _seekSlider.ValueChanged += async (s, e) =>
            {
                if (_updatingSeekSlider) return;
                if (_dragTrackVersion.HasValue)
                    _timePositionText.Text = FormatMediaTime(TimeSpan.FromSeconds(e.NewValue));
                else
                    await SeekFromSliderAsync();
            };
            timelineStack.Children.Add(_seekSlider);

            var timeGrid = new Grid();
            _timePositionText = new TextBlock
            {
                Text = "00:00",
                FontFamily = ThemeTokens.MonoFont,
                FontSize = 10.5,
                Foreground = _palette.MutedText,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            _timeDurationText = new TextBlock
            {
                Text = "00:00",
                FontFamily = ThemeTokens.MonoFont,
                FontSize = 10.5,
                Foreground = _palette.MutedText,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            _seekStatus = new TextBlock
            {
                FontSize = 10,
                Foreground = _palette.SecondaryText,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            timeGrid.Children.Add(_seekStatus);
            timeGrid.Children.Add(_timePositionText);
            timeGrid.Children.Add(_timeDurationText);
            timelineStack.Children.Add(timeGrid);
            Grid.SetRow(timelineStack, 2);
            layoutGrid.Children.Add(timelineStack);

            // --- Row 3: Full-Width Balanced Transport Controls ---
            var transportGrid = new Grid();
            transportGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            transportGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
            transportGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.35, GridUnitType.Star) });
            transportGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
            transportGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var previous = CreateTransportButton(VectorIcons.Create(VectorIcons.SkipPrev, _palette.PrimaryText, 15),
                "Предыдущий трек", _media.SkipPreviousAsync);
            Grid.SetColumn(previous, 0);
            transportGrid.Children.Add(previous);

            _playPauseIcon = VectorIcons.Create(_media.IsPlaying ? VectorIcons.Pause : VectorIcons.Play, _palette.OnAccentText, 16);
            var playPause = CreateTransportButton(_playPauseIcon, "Воспроизведение / пауза", _media.TogglePlayPauseAsync, true);
            Grid.SetColumn(playPause, 2);
            transportGrid.Children.Add(playPause);

            var next = CreateTransportButton(VectorIcons.Create(VectorIcons.SkipNext, _palette.PrimaryText, 15),
                "Следующий трек", _media.SkipNextAsync);
            Grid.SetColumn(next, 4);
            transportGrid.Children.Add(next);

            Grid.SetRow(transportGrid, 3);
            layoutGrid.Children.Add(transportGrid);

            card.Child = layoutGrid;
            return card;
        }

        private Button CreateTransportButton(
            UIElement icon, string label, Func<System.Threading.Tasks.Task> command, bool primary = false)
        {
            var button = new Button
            {
                Content = icon,
                ToolTip = label,
                Height = 29,
                Background = primary ? _palette.Accent : _palette.RaisedBackground,
                BorderBrush = primary ? _palette.Accent : _palette.HairlineBorder
            };
            button.SetResourceReference(StyleProperty, "IslandPlayerButton");
            System.Windows.Automation.AutomationProperties.SetName(button, label);
            button.Click += async (s, e) => await command();
            return button;
        }

        private UIElement BuildVolumeCard()
        {
            var card = ThemeTokens.CreateCard(_palette, 12, new Thickness(12, 10, 12, 10));
            var stack = new StackPanel();

            var headerGrid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var title = new TextBlock
            {
                Text = "Системная громкость",
                FontFamily = ThemeTokens.UiFont,
                FontSize = 11.5,
                FontWeight = FontWeights.Medium,
                Foreground = _palette.SecondaryText,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(title, 0);
            headerGrid.Children.Add(title);

            _volumeValueText = new TextBlock
            {
                Text = "50%",
                FontFamily = ThemeTokens.MonoFont,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = _palette.PrimaryText,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(_volumeValueText, 1);
            headerGrid.Children.Add(_volumeValueText);
            stack.Children.Add(headerGrid);

            var controlGrid = new Grid();
            controlGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            controlGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            controlGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            _muteIcon = VectorIcons.Create(VectorIcons.VolumeHigh, _palette.PrimaryText, 14);
            _muteBtn = ThemeTokens.CreateInteractiveButton(
                _palette,
                _muteIcon,
                () =>
                {
                    _sys.Audio.ToggleMute();
                    RefreshVolumeUi(_sys.Audio.GetVolumePercent(), _sys.Audio.GetMuted());
                },
                false, false, 7, new Thickness(8, 5, 8, 5), "Отключить / включить звук");
            Grid.SetColumn(_muteBtn, 0);
            controlGrid.Children.Add(_muteBtn);

            _volumeSlider = new Slider
            {
                Minimum = 0,
                Maximum = 100,
                Value = _sys.Audio.GetVolumePercent(),
                VerticalAlignment = VerticalAlignment.Center,
                IsMoveToPointEnabled = true,
                Cursor = Cursors.Hand
            };
            _volumeSlider.ValueChanged += (s, e) =>
            {
                if (_updatingVolumeSlider) return;
                int pct = (int)Math.Round(e.NewValue);
                _sys.Audio.SetVolumePercent(pct);
                _volumeValueText.Text = $"{pct}%";
            };
            Grid.SetColumn(_volumeSlider, 2);
            controlGrid.Children.Add(_volumeSlider);

            stack.Children.Add(controlGrid);
            card.Child = stack;
            return card;
        }

        private UIElement BuildKeyboardCard()
        {
            var card = ThemeTokens.CreateCard(_palette, 12, new Thickness(12, 10, 12, 10));
            var stack = new StackPanel();

            var topGrid = new Grid();
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var labelStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            labelStack.Children.Add(new TextBlock
            {
                Text = "Раскладка клавиатуры",
                FontFamily = ThemeTokens.UiFont,
                FontSize = 11.5,
                FontWeight = FontWeights.Medium,
                Foreground = _palette.SecondaryText
            });
            _layoutNameText = new TextBlock
            {
                Text = _sys.CurrentKeyboardLayoutFull,
                FontFamily = ThemeTokens.UiFont,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = _palette.PrimaryText,
                Margin = new Thickness(0, 2, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            labelStack.Children.Add(_layoutNameText);
            Grid.SetColumn(labelStack, 0);
            topGrid.Children.Add(labelStack);

            // Precision-centered RU / EN square badge (Requirement 10 fix)
            _layoutCodeText = new TextBlock
            {
                Text = _sys.CurrentKeyboardLayoutShort,
                FontFamily = ThemeTokens.UiFont,
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = _palette.OnAccentText,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0)
            };

            var badgeCenterGrid = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            badgeCenterGrid.Children.Add(_layoutCodeText);

            var switchBtn = ThemeTokens.CreateInteractiveButton(
                _palette,
                badgeCenterGrid,
                () => _sys.SwitchKeyboardLayoutAndNotify(_getHwnd()),
                true, false, 8, new Thickness(0), "Нажмите для мгновенного переключения раскладки (RU / EN)");
            switchBtn.Width = 38;
            switchBtn.Height = 34;
            switchBtn.VerticalAlignment = VerticalAlignment.Center;

            Grid.SetColumn(switchBtn, 1);
            topGrid.Children.Add(switchBtn);
            stack.Children.Add(topGrid);

            // Caps Lock & Num Lock indicators
            var locksRow = new Grid { Margin = new Thickness(0, 8, 0, 0) };
            locksRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            locksRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
            locksRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            _capsBadgeText = new TextBlock
            {
                Text = "CAPS LOCK",
                FontFamily = ThemeTokens.MonoFont,
                FontSize = 10.5,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };
            _capsBadge = ThemeTokens.CreateInteractiveButton(
                _palette,
                _capsBadgeText,
                () => _sys.ToggleCapsLockAndNotify(),
                false, false, 6, new Thickness(6, 4, 6, 4), "Переключить Caps Lock");
            Grid.SetColumn(_capsBadge, 0);
            locksRow.Children.Add(_capsBadge);

            _numBadgeText = new TextBlock
            {
                Text = "NUM LOCK",
                FontFamily = ThemeTokens.MonoFont,
                FontSize = 10.5,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };
            _numBadge = new Border
            {
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(0),
                SnapsToDevicePixels = true,
                Child = ThemeTokens.WrapContentWithGlassLayers(_palette, _numBadgeText, 6, false, new Thickness(6, 4, 6, 4))
            };
            Grid.SetColumn(_numBadge, 2);
            locksRow.Children.Add(_numBadge);

            stack.Children.Add(locksRow);
            card.Child = stack;
            return card;
        }

        private UIElement BuildPowerAndAiCard()
        {
            var card = ThemeTokens.CreateCard(_palette, 12, new Thickness(14, 11, 14, 11));
            var layoutGrid = new Grid { VerticalAlignment = VerticalAlignment.Stretch };
            layoutGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Row 0: Header
            layoutGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Row 1: Power Buttons
            layoutGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Row 2: Quick Tools Grid
            layoutGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Row 3: Auto-Hide Status Bar

            var headerTitle = new TextBlock
            {
                Text = "Управление питанием и быстрые действия",
                FontFamily = ThemeTokens.UiFont,
                FontSize = 11.5,
                FontWeight = FontWeights.Medium,
                Foreground = _palette.SecondaryText,
                Margin = new Thickness(0, 0, 0, 6)
            };
            Grid.SetRow(headerTitle, 0);
            layoutGrid.Children.Add(headerTitle);

            var powerHost = new Grid();

            // 1. Normal Power Buttons
            _powerNormalPanel = new StackPanel();
            var btnGrid = new Grid();
            btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
            btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var shutdownContent = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            shutdownContent.Children.Add(VectorIcons.Create(VectorIcons.Power, _palette.Destructive, 13));
            shutdownContent.Children.Add(new TextBlock
            {
                Text = "Выключить",
                FontFamily = ThemeTokens.UiFont,
                FontSize = 11.5,
                FontWeight = FontWeights.Medium,
                Foreground = _palette.PrimaryText,
                Margin = new Thickness(5, 0, 0, 0)
            });

            var shutdownBtn = ThemeTokens.CreateInteractiveButton(
                _palette,
                shutdownContent,
                () => PromptPowerConfirmation(PowerActionKind.Shutdown),
                false, false, 7, new Thickness(8, 6, 8, 6), "Выключение компьютера с подтверждением");
            Grid.SetColumn(shutdownBtn, 0);
            btnGrid.Children.Add(shutdownBtn);

            var restartContent = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            restartContent.Children.Add(VectorIcons.Create(VectorIcons.Restart, _palette.SecondaryText, 13));
            restartContent.Children.Add(new TextBlock
            {
                Text = "Перезагрузка",
                FontFamily = ThemeTokens.UiFont,
                FontSize = 11.5,
                FontWeight = FontWeights.Medium,
                Foreground = _palette.PrimaryText,
                Margin = new Thickness(5, 0, 0, 0)
            });

            var restartBtn = ThemeTokens.CreateInteractiveButton(
                _palette,
                restartContent,
                () => PromptPowerConfirmation(PowerActionKind.Restart),
                false, false, 7, new Thickness(8, 6, 8, 6), "Перезагрузка компьютера с подтверждением");
            Grid.SetColumn(restartBtn, 2);
            btnGrid.Children.Add(restartBtn);

            _powerNormalPanel.Children.Add(btnGrid);
            powerHost.Children.Add(_powerNormalPanel);

            // 2. Confirmation Panel
            _powerConfirmPanel = new StackPanel { Visibility = Visibility.Collapsed };
            _powerConfirmText = new TextBlock
            {
                Text = "Подтвердите действие:",
                FontFamily = ThemeTokens.UiFont,
                FontSize = 11.5,
                Foreground = _palette.Warning,
                Margin = new Thickness(0, 0, 0, 5)
            };
            _powerConfirmPanel.Children.Add(_powerConfirmText);

            var confirmBtns = new Grid();
            confirmBtns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            confirmBtns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
            confirmBtns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var yesBtn = ThemeTokens.CreateInteractiveButton(
                _palette,
                new TextBlock { Text = "Подтвердить (10с)", FontFamily = ThemeTokens.UiFont, FontSize = 11.5, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center },
                () =>
                {
                    var kind = _confirmingPowerKind;
                    _confirmingPowerKind = PowerActionKind.None;
                    _sys.SchedulePowerAction(kind, 10);
                },
                false, true, 6, new Thickness(6, 5, 6, 5));
            Grid.SetColumn(yesBtn, 0);
            confirmBtns.Children.Add(yesBtn);

            var cancelConfirmBtn = ThemeTokens.CreateInteractiveButton(
                _palette,
                new TextBlock { Text = "Отмена", FontFamily = ThemeTokens.UiFont, FontSize = 11.5, Foreground = _palette.PrimaryText, HorizontalAlignment = HorizontalAlignment.Center },
                () =>
                {
                    _confirmingPowerKind = PowerActionKind.None;
                    RefreshPowerUi();
                },
                false, false, 6, new Thickness(6, 5, 6, 5));
            Grid.SetColumn(cancelConfirmBtn, 2);
            confirmBtns.Children.Add(cancelConfirmBtn);

            _powerConfirmPanel.Children.Add(confirmBtns);
            powerHost.Children.Add(_powerConfirmPanel);

            // 3. Active Countdown Cancellation Panel
            _powerCountdownPanel = new StackPanel { Visibility = Visibility.Collapsed };
            _powerCountdownText = new TextBlock
            {
                Text = "Выключение через 10 сек...",
                FontFamily = ThemeTokens.MonoFont,
                FontSize = 11.5,
                Foreground = _palette.Destructive,
                Margin = new Thickness(0, 0, 0, 5)
            };
            _powerCountdownPanel.Children.Add(_powerCountdownText);

            var abortBtn = ThemeTokens.CreateInteractiveButton(
                _palette,
                new TextBlock
                {
                    Text = "Отменить действие",
                    FontFamily = ThemeTokens.UiFont,
                    FontSize = 11.5,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = _palette.OnAccentText,
                    HorizontalAlignment = HorizontalAlignment.Center
                },
                () => _sys.CancelPowerAction(),
                true, false, 6, new Thickness(8, 5, 8, 5));
            _powerCountdownPanel.Children.Add(abortBtn);
            powerHost.Children.Add(_powerCountdownPanel);

            Grid.SetRow(powerHost, 1);
            layoutGrid.Children.Add(powerHost);

            // --- Row 2: 3-Column Quick Productivity & Theme Shortcuts ---
            var shortcutsGrid = new Grid { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 6, 0, 6) };
            shortcutsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            shortcutsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
            shortcutsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            shortcutsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
            shortcutsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var noteShortcutContent = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            noteShortcutContent.Children.Add(VectorIcons.Create(VectorIcons.Notes, _palette.SecondaryText, 12));
            noteShortcutContent.Children.Add(new TextBlock
            {
                Text = "Заметка",
                FontFamily = ThemeTokens.UiFont,
                FontSize = 11,
                Foreground = _palette.PrimaryText,
                Margin = new Thickness(4.5, 0, 0, 0)
            });
            var noteShortcutBtn = ThemeTokens.CreateInteractiveButton(
                _palette,
                noteShortcutContent,
                () => _navigateToTab?.Invoke(3),
                false, false, 7, new Thickness(6, 5, 6, 5), "Открыть раздел заметок");
            Grid.SetColumn(noteShortcutBtn, 0);
            shortcutsGrid.Children.Add(noteShortcutBtn);

            var timerShortcutContent = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            timerShortcutContent.Children.Add(VectorIcons.Create(VectorIcons.Clock, _palette.SecondaryText, 12));
            timerShortcutContent.Children.Add(new TextBlock
            {
                Text = "Таймер",
                FontFamily = ThemeTokens.UiFont,
                FontSize = 11,
                Foreground = _palette.PrimaryText,
                Margin = new Thickness(4.5, 0, 0, 0)
            });
            var timerShortcutBtn = ThemeTokens.CreateInteractiveButton(
                _palette,
                timerShortcutContent,
                () => _navigateToTab?.Invoke(3),
                false, false, 7, new Thickness(6, 5, 6, 5), "Открыть таймер и секундомер");
            Grid.SetColumn(timerShortcutBtn, 2);
            shortcutsGrid.Children.Add(timerShortcutBtn);

            string currentThemeLabel = _settings.Current.Theme == AppThemeMode.Ios26Glass
                ? "Glass"
                : (_settings.Current.Theme == AppThemeMode.Dark ? "OLED" : "Светлая");
            var themeShortcutContent = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            themeShortcutContent.Children.Add(VectorIcons.Create(VectorIcons.Sun, _palette.SecondaryText, 12));
            themeShortcutContent.Children.Add(new TextBlock
            {
                Text = currentThemeLabel,
                FontFamily = ThemeTokens.UiFont,
                FontSize = 11,
                Foreground = _palette.PrimaryText,
                Margin = new Thickness(4.5, 0, 0, 0)
            });
            var themeShortcutBtn = ThemeTokens.CreateInteractiveButton(
                _palette,
                themeShortcutContent,
                () =>
                {
                    if (_settings.Current.Theme == AppThemeMode.Light) _settings.Current.Theme = AppThemeMode.Dark;
                    else if (_settings.Current.Theme == AppThemeMode.Dark) _settings.Current.Theme = AppThemeMode.Ios26Glass;
                    else _settings.Current.Theme = AppThemeMode.Light;
                    _settings.Save();
                    _onSettingsChanged?.Invoke();
                },
                false, false, 7, new Thickness(6, 5, 6, 5), "Быстрое переключение темы (Светлая → OLED → iOS 26 Glass)");
            Grid.SetColumn(themeShortcutBtn, 4);
            shortcutsGrid.Children.Add(themeShortcutBtn);

            Grid.SetRow(shortcutsGrid, 2);
            layoutGrid.Children.Add(shortcutsGrid);

            // --- Row 3: Auto-Retract Status Bar ---
            var quickRow = new Grid();
            quickRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            quickRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var quickLabelStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            quickLabelStack.Children.Add(VectorIcons.Create(VectorIcons.Settings, _palette.MutedText, 12.5));
            var autoHideLabel = new TextBlock
            {
                Text = _settings.Current.AutoRetractOnIdle ? "Скрытие за край в покое: ВКЛ" : "Скрытие за край в покое: ВЫКЛ",
                FontFamily = ThemeTokens.UiFont,
                FontSize = 11,
                Foreground = _palette.SecondaryText,
                Margin = new Thickness(6, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            quickLabelStack.Children.Add(autoHideLabel);
            Grid.SetColumn(quickLabelStack, 0);
            quickRow.Children.Add(quickLabelStack);

            var toggleIdleBtn = ThemeTokens.CreateInteractiveButton(
                _palette,
                new TextBlock
                {
                    Text = _settings.Current.AutoRetractOnIdle ? "Активно" : "Включить",
                    FontFamily = ThemeTokens.UiFont,
                    FontSize = 10.5,
                    Foreground = _palette.PrimaryText
                },
                () =>
                {
                    _settings.Current.AutoRetractOnIdle = !_settings.Current.AutoRetractOnIdle;
                    _settings.Save();
                    _onSettingsChanged?.Invoke();
                },
                false, false, 5, new Thickness(7, 3, 7, 3), "Прятать остров вверх за край экрана в бездействии, чтобы не мешать кликам в окнах");
            Grid.SetColumn(toggleIdleBtn, 1);
            quickRow.Children.Add(toggleIdleBtn);

            Grid.SetRow(quickRow, 3);
            layoutGrid.Children.Add(quickRow);

            card.Child = layoutGrid;
            return card;
        }

        private void PromptPowerConfirmation(PowerActionKind kind)
        {
            _confirmingPowerKind = kind;
            _powerConfirmText.Text = kind == PowerActionKind.Shutdown
                ? "Подтвердить выключение компьютера?"
                : "Подтвердить перезагрузку компьютера?";
            RefreshPowerUi();
        }

        private void RefreshPowerUi()
        {
            if (_powerNormalPanel == null) return;
            if (_sys.PendingPowerAction != PowerActionKind.None)
            {
                _powerNormalPanel.Visibility = Visibility.Collapsed;
                _powerConfirmPanel.Visibility = Visibility.Collapsed;
                _powerCountdownPanel.Visibility = Visibility.Visible;
                string act = _sys.PendingPowerAction == PowerActionKind.Shutdown ? "Выключение" : "Перезагрузка";
                _powerCountdownText.Text = $"{act} через {_sys.PowerCountdownSeconds} сек";
            }
            else if (_confirmingPowerKind != PowerActionKind.None)
            {
                _powerNormalPanel.Visibility = Visibility.Collapsed;
                _powerConfirmPanel.Visibility = Visibility.Visible;
                _powerCountdownPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                _powerNormalPanel.Visibility = Visibility.Visible;
                _powerConfirmPanel.Visibility = Visibility.Collapsed;
                _powerCountdownPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateLyricsVisibility()
        {
            _dashboardDetails.Visibility = _showLyrics ? Visibility.Collapsed : Visibility.Visible;
            _lyricsCard.Visibility = _showLyrics ? Visibility.Visible : Visibility.Collapsed;
            _lyricsView.Visibility = _lyricsCard.Visibility;
            _lyricsButton.Content = _showLyrics ? "Скрыть текст" : "Текст песни";
        }

        private void RefreshMediaUi()
        {
            if (_trackTitleText == null) return;
            if (_renderedTrackVersion != _media.TrackVersion)
            {
                _renderedTrackVersion = _media.TrackVersion;
                _seekStatus.Text = string.Empty;
            }
            _trackTitleText.Text = _media.Title;
            _trackArtistText.Text = _media.Artist;
            _albumImage.Source = _media.AlbumCover;
            _albumImage.Visibility = _media.AlbumCover == null ? Visibility.Collapsed : Visibility.Visible;
            _albumFallbackIcon.Visibility = _media.AlbumCover == null ? Visibility.Visible : Visibility.Collapsed;
            _playPauseIcon.Data = Geometry.Parse(_media.IsPlaying ? VectorIcons.Pause : VectorIcons.Play);
            RefreshTimelineUi();
        }

        private void RefreshTimelineUi()
        {
            if (_seekSlider == null) return;
            _lyricsView?.Refresh();
            if (_dragTrackVersion.HasValue) return;

            _updatingSeekSlider = true;
            try
            {
                _seekSlider.Maximum = _media.Duration.TotalSeconds;
                _seekSlider.Value = _media.Position.TotalSeconds;
                _seekSlider.IsEnabled = _media.CanSeek;
                _seekSlider.Opacity = _media.CanSeek ? 1.0 : 0.55;
                _seekSlider.ToolTip = _media.CanSeek ? "Перемотка трека" : "Плеер не разрешает перемотку";
            }
            finally
            {
                _updatingSeekSlider = false;
            }
            _timePositionText.Text = FormatMediaTime(_media.Position);
            _timeDurationText.Text = FormatMediaTime(_media.Duration);
        }

        private async System.Threading.Tasks.Task SeekFromSliderAsync()
        {
            int track = _media.TrackVersion;
            _seekStatus.Text = string.Empty;
            bool accepted = await _media.SeekAsync(TimeSpan.FromSeconds(_seekSlider.Value), track);
            if (track == _media.TrackVersion && !accepted)
                _seekStatus.Text = "Перемотка недоступна";
            RefreshTimelineUi();
        }

        private static string FormatMediaTime(TimeSpan ts)
        {
            if (ts < TimeSpan.Zero) ts = TimeSpan.Zero;
            return $"{(int)ts.TotalMinutes:00}:{ts.Seconds:00}";
        }

        private void OnVolumeStateChanged(int volumePercent, bool isMuted)
        {
            RefreshVolumeUi(volumePercent, isMuted);
        }

        private void RefreshVolumeUi(int volumePercent, bool isMuted)
        {
            if (_volumeSlider == null) return;
            _updatingVolumeSlider = true;
            _volumeSlider.Value = volumePercent;
            _updatingVolumeSlider = false;

            _volumeValueText.Text = isMuted ? "MUTE" : $"{volumePercent}%";
            _volumeValueText.Foreground = isMuted ? _palette.Warning : _palette.PrimaryText;
            if (_muteIcon != null)
            {
                _muteIcon.Data = Geometry.Parse(isMuted || volumePercent == 0 ? VectorIcons.VolumeMute : VectorIcons.VolumeHigh);
                _muteIcon.Stroke = isMuted ? _palette.Warning : _palette.PrimaryText;
            }
        }

        private void RefreshKeyboardUi()
        {
            if (_layoutCodeText == null) return;
            _layoutCodeText.Text = _sys.CurrentKeyboardLayoutShort;
            _layoutNameText.Text = _sys.CurrentKeyboardLayoutFull;

            bool caps = _sys.IsCapsLockOn;
            _capsBadge.Background = caps ? _palette.AccentSubtle : _palette.RaisedBackground;
            _capsBadge.BorderBrush = caps ? _palette.Accent : _palette.HairlineBorder;
            if (_capsBadgeText != null)
            {
                _capsBadgeText.Text = caps ? "CAPS: ВКЛ" : "CAPS: ВЫКЛ";
                _capsBadgeText.Foreground = caps ? _palette.PrimaryText : _palette.MutedText;
                _capsBadgeText.FontWeight = caps ? FontWeights.SemiBold : FontWeights.Normal;
            }

            bool num = _sys.IsNumLockOn;
            _numBadge.Background = num ? _palette.RaisedBackground : _palette.CardBackground;
            _numBadge.BorderBrush = _palette.HairlineBorder;
            if (_numBadgeText != null)
            {
                _numBadgeText.Text = num ? "NUM: ВКЛ" : "NUM: ВЫКЛ";
                _numBadgeText.Foreground = num ? _palette.SecondaryText : _palette.MutedText;
            }
        }
    }
}
