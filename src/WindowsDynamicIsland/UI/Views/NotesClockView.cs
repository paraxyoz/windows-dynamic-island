using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WindowsDynamicIsland.Models;
using WindowsDynamicIsland.Services;

namespace WindowsDynamicIsland.UI.Views
{
    /// <summary>
    /// Unified single-page composition for Notes (primary workspace on the left, ~63% width)
    /// and two compact auxiliary time instruments on the right (~37% width): Countdown Timer and Stopwatch.
    /// Notes work as separate items in a clean vertical card list; clicking any note opens its dedicated
    /// editor where title, content, and reminder time can be edited or the reminder disabled.
    /// </summary>
    public sealed class NotesClockView : UserControl
    {
        private readonly SettingsManager _settings;
        private readonly NotesManager _notes;
        private readonly TimerService _timers;
        private ThemePalette _palette;
        private readonly bool _clockOnly;
        private UIElement _editorEmpty;

        // Notes UI (Master List + Detail Editor)
        private Grid _notesListViewGrid;
        private Grid _noteEditorViewGrid;
        private TextBox _noteSearchBox;
        private StackPanel _notesListPanel;
        private TextBlock _notesCountText;

        private TextBox _noteTitleBox;
        private TextBox _noteContentBox;
        private TextBox _reminderTimeBox;
        private TextBlock _reminderStatusText;
        private Border _disableReminderBtn;
        private Border _pinNoteBtn;
        private System.Windows.Shapes.Path _pinNoteIcon;
        private string _selectedNoteId;
        private bool _isEditingNote;
        private bool _suppressNoteAutoSave;

        // Timer UI (Compact Auxiliary Block 1)
        private TextBlock _timerDisplayLabel;
        private ProgressBar _timerProgress;
        private TextBlock _timerStatusBadge;
        private TextBox _timerMinBox;
        private TextBox _timerSecBox;
        private TextBlock _timerStartPauseText;

        // Stopwatch UI (Compact Auxiliary Block 2)
        private TextBlock _stopwatchDisplayLabel;
        private TextBlock _stopwatchStartPauseText;
        private StackPanel _lapsListPanel;
        private int _renderedLapCount = -1;

        public NotesClockView(SettingsManager settings, NotesManager notes, TimerService timers, bool clockOnly = false)
        {
            _settings = settings;
            _notes = notes;
            _timers = timers;

            _clockOnly = clockOnly;
            if (clockOnly)
            {
                _timers.TimerTick += RefreshTimerDisplay;
                _timers.StopwatchTick += RefreshStopwatchDisplay;
            }
            else
            {
                _notes.NotesChanged += OnExternalNotesChanged;
            }
            RebuildTheme();
        }

        private void OnExternalNotesChanged()
        {
            if (!_isEditingNote)
            {
                RefreshNotesList();
            }
            else if (_noteContentBox != null && !_noteContentBox.IsKeyboardFocused &&
                     _noteTitleBox != null && !_noteTitleBox.IsKeyboardFocused)
            {
                LoadSelectedNoteIntoEditor();
            }
        }

        public void OpenFirstNoteEditor()
        {
            if (_notes.Notes.Count > 0)
            {
                OpenNoteEditor(_notes.Notes[0].Id, false);
            }
            else CreateNewNoteAndOpenEditor();
        }

        public void ShowNotesList()
        {
            CloseNoteEditorToList();
        }

        public void RebuildTheme()
        {
            _palette = ThemeTokens.GetPalette(_settings.Current.Theme, 1.0);

            var root = new Grid();
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(_clockOnly ? 1 : 0.65, GridUnitType.Star) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            if (_clockOnly)
            {
                var timer = BuildCompactTimerBlock();
                var stopwatch = BuildCompactStopwatchBlock();
                Grid.SetColumn(stopwatch, 2);
                root.Children.Add(timer);
                root.Children.Add(stopwatch);
                Content = root;
                RefreshTimerDisplay();
                RefreshStopwatchDisplay();
                RefreshStopwatchLaps();
                return;
            }

            var list = ThemeTokens.CreateCard(_palette, 15, new Thickness(12));
            _notesListViewGrid = BuildNotesListView();
            list.Child = _notesListViewGrid;
            root.Children.Add(list);

            var editor = ThemeTokens.CreateCard(_palette, 15, new Thickness(14));
            var editorHost = new Grid();
            _noteEditorViewGrid = BuildNoteEditorView();
            _editorEmpty = ThemeTokens.CreateEmptyState(_palette, VectorIcons.Notes, "Место для мысли",
                "Выберите заметку слева или создайте новую.");
            editorHost.Children.Add(_editorEmpty);
            editorHost.Children.Add(_noteEditorViewGrid);
            editor.Child = editorHost;
            Grid.SetColumn(editor, 2);
            root.Children.Add(editor);
            Content = root;
            _isEditingNote = _selectedNoteId != null && _notes.Notes.Any(n => n.Id == _selectedNoteId);
            _noteEditorViewGrid.Visibility = _isEditingNote ? Visibility.Visible : Visibility.Collapsed;
            _editorEmpty.Visibility = _isEditingNote ? Visibility.Collapsed : Visibility.Visible;
            if (_isEditingNote) LoadSelectedNoteIntoEditor();
            RefreshNotesList();
        }

        private Grid BuildNotesListView()
        {
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header + Search + New Note Button
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Vertical List of Separate Note Cards

            var topBar = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            topBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            topBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            topBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var titleStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            titleStack.Children.Add(VectorIcons.Create(VectorIcons.Notes, _palette.SecondaryText, 13));
            titleStack.Children.Add(new TextBlock
            {
                Text = "Заметки",
                FontFamily = ThemeTokens.UiFont,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = _palette.PrimaryText,
                Margin = new Thickness(6, 0, 5, 0),
                VerticalAlignment = VerticalAlignment.Center
            });

            _notesCountText = new TextBlock
            {
                Text = _notes.Notes.Count.ToString(),
                FontFamily = ThemeTokens.MonoFont,
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = _palette.SecondaryText,
                VerticalAlignment = VerticalAlignment.Center
            };
            var countBadge = new Border
            {
                Background = _palette.RaisedBackground,
                BorderBrush = _palette.HairlineBorder,
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(5, 1, 5, 1),
                VerticalAlignment = VerticalAlignment.Center,
                Child = _notesCountText
            };
            titleStack.Children.Add(countBadge);
            Grid.SetColumn(titleStack, 0);
            topBar.Children.Add(titleStack);

            var searchBorder = new Border
            {
                Background = _palette.RaisedBackground,
                BorderBrush = _palette.HairlineBorder,
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(8, 4, 8, 4)
            };
            var searchHost = ThemeTokens.CreateInputWithPlaceholder(_palette, "Поиск", out _noteSearchBox, 11.5, false);
            _noteSearchBox.TextChanged += (s, e) => RefreshNotesList();
            searchBorder.Child = searchHost;
            Grid.SetColumn(searchBorder, 2);
            topBar.Children.Add(searchBorder);

            var newBtnContent = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            newBtnContent.Children.Add(VectorIcons.Create(VectorIcons.Plus, _palette.OnAccentText, 11.5));
            newBtnContent.Children.Add(new TextBlock
            {
                Text = "Новая",
                FontFamily = ThemeTokens.UiFont,
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = _palette.OnAccentText,
                Margin = new Thickness(4, 0, 0, 0)
            });

            var addNoteBtn = ThemeTokens.CreateInteractiveButton(
                _palette,
                newBtnContent,
                CreateNewNoteAndOpenEditor,
                true, false, 7, new Thickness(9, 4, 9, 4), "Создать новую отдельную заметку");
            Grid.SetColumn(addNoteBtn, 4);
            topBar.Children.Add(addNoteBtn);

            Grid.SetRow(topBar, 0);
            grid.Children.Add(topBar);

            _notesListPanel = new StackPanel();
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = _notesListPanel
            };
            Grid.SetRow(scroll, 1);
            grid.Children.Add(scroll);

            return grid;
        }

        private Grid BuildNoteEditorView()
        {
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Row 0: Back to list + Pin + Delete + Done
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Row 1: Note Title Input
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Row 2: Multi-line Content Input
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Row 3: Reminder Time Picker & Disable Reminder Bar

            // --- Row 0: Navigation & Note Actions ---
            var navBar = new Grid { Margin = new Thickness(0, 0, 0, 7) };
            navBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            navBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            navBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            navBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
            navBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            navBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
            navBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var backBtn = ThemeTokens.CreateInteractiveButton(
                _palette,
                new TextBlock
                {
                    Text = "Закрыть",
                    FontFamily = ThemeTokens.UiFont,
                    FontSize = 11,
                    FontWeight = FontWeights.Medium,
                    Foreground = _palette.PrimaryText
                },
                CloseNoteEditorToList,
                false, false, 6, new Thickness(8, 4, 8, 4), "Вернуться ко всем заметкам");
            Grid.SetColumn(backBtn, 0);
            navBar.Children.Add(backBtn);

            _pinNoteIcon = VectorIcons.Create(VectorIcons.Pin, _palette.SecondaryText, 12);
            _pinNoteBtn = ThemeTokens.CreateInteractiveButton(
                _palette,
                _pinNoteIcon,
                () =>
                {
                    if (!string.IsNullOrEmpty(_selectedNoteId))
                    {
                        _notes.TogglePin(_selectedNoteId);
                        UpdatePinButtonState();
                    }
                },
                false, false, 6, new Thickness(7, 4, 7, 4), "Закрепить заметку наверху списка");
            Grid.SetColumn(_pinNoteBtn, 2);
            navBar.Children.Add(_pinNoteBtn);

            var delNoteBtn = ThemeTokens.CreateInteractiveButton(
                _palette,
                VectorIcons.Create(VectorIcons.Trash, _palette.Destructive, 12),
                () =>
                {
                    if (!string.IsNullOrEmpty(_selectedNoteId))
                    {
                        _notes.DeleteNote(_selectedNoteId);
                        CloseNoteEditorToList();
                    }
                },
                false, false, 6, new Thickness(7, 4, 7, 4), "Удалить эту заметку");
            Grid.SetColumn(delNoteBtn, 4);
            navBar.Children.Add(delNoteBtn);

            var doneBtn = ThemeTokens.CreateInteractiveButton(
                _palette,
                new TextBlock
                {
                    Text = "Готово",
                    FontFamily = ThemeTokens.UiFont,
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = _palette.OnAccentText
                },
                CloseNoteEditorToList,
                true, false, 6, new Thickness(9, 4, 9, 4), "Сохранить и вернуться к списку");
            Grid.SetColumn(doneBtn, 6);
            navBar.Children.Add(doneBtn);

            Grid.SetRow(navBar, 0);
            grid.Children.Add(navBar);

            // --- Row 1: Note Title Box ---
            var titleContainer = new Border
            {
                Background = _palette.RaisedBackground,
                BorderBrush = _palette.HairlineBorder,
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(9, 5, 9, 5),
                Margin = new Thickness(0, 0, 0, 6)
            };
            var titleHost = ThemeTokens.CreateInputWithPlaceholder(_palette, "Заголовок заметки...", out _noteTitleBox, 13, false);
            _noteTitleBox.FontWeight = FontWeights.SemiBold;
            _noteTitleBox.TextChanged += OnEditorTextChanged;
            titleContainer.Child = titleHost;
            Grid.SetRow(titleContainer, 1);
            grid.Children.Add(titleContainer);

            // --- Row 2: Multi-line Note Body ---
            var bodyContainer = new Border
            {
                Background = _palette.RaisedBackground,
                BorderBrush = _palette.HairlineBorder,
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(9, 7, 9, 7)
            };
            var bodyHost = ThemeTokens.CreateInputWithPlaceholder(_palette, "Введите текст заметки...", out _noteContentBox, 12, true);
            _noteContentBox.TextChanged += OnEditorTextChanged;
            bodyContainer.Child = bodyHost;
            Grid.SetRow(bodyContainer, 2);
            grid.Children.Add(bodyContainer);

            // --- Row 3: Reminder Management Bar (Change Time or Disable Reminder) ---
            var reminderBar = new Grid { Margin = new Thickness(0, 7, 0, 0) };
            reminderBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Bell
            reminderBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Status Text
            reminderBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // +5m
            reminderBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // +15m
            reminderBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // +1h
            reminderBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // HH:mm input
            reminderBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Set time btn
            reminderBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Disable reminder btn

            var bellIcon = VectorIcons.Create(VectorIcons.Bell, _palette.SecondaryText, 12.5);
            bellIcon.Margin = new Thickness(0, 0, 5, 0);
            Grid.SetColumn(bellIcon, 0);
            reminderBar.Children.Add(bellIcon);

            _reminderStatusText = new TextBlock
            {
                Text = "Без напоминания",
                FontFamily = ThemeTokens.UiFont,
                FontSize = 10.5,
                Foreground = _palette.MutedText,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 0, 6, 0)
            };
            Grid.SetColumn(_reminderStatusText, 1);
            reminderBar.Children.Add(_reminderStatusText);

            var plus5Btn = ThemeTokens.CreateInteractiveButton(
                _palette,
                new TextBlock { Text = "+5м", FontFamily = ThemeTokens.MonoFont, FontSize = 10, Foreground = _palette.PrimaryText },
                () => SetQuickReminderMinutes(5),
                false, false, 5, new Thickness(5, 3, 5, 3), "Напомнить через 5 минут");
            plus5Btn.Margin = new Thickness(0, 0, 3, 0);
            Grid.SetColumn(plus5Btn, 2);
            reminderBar.Children.Add(plus5Btn);

            var plus15Btn = ThemeTokens.CreateInteractiveButton(
                _palette,
                new TextBlock { Text = "+15м", FontFamily = ThemeTokens.MonoFont, FontSize = 10, Foreground = _palette.PrimaryText },
                () => SetQuickReminderMinutes(15),
                false, false, 5, new Thickness(5, 3, 5, 3), "Напомнить через 15 минут");
            plus15Btn.Margin = new Thickness(0, 0, 3, 0);
            Grid.SetColumn(plus15Btn, 3);
            reminderBar.Children.Add(plus15Btn);

            var plus60Btn = ThemeTokens.CreateInteractiveButton(
                _palette,
                new TextBlock { Text = "+1ч", FontFamily = ThemeTokens.MonoFont, FontSize = 10, Foreground = _palette.PrimaryText },
                () => SetQuickReminderMinutes(60),
                false, false, 5, new Thickness(5, 3, 5, 3), "Напомнить через 1 час");
            plus60Btn.Margin = new Thickness(0, 0, 4, 0);
            Grid.SetColumn(plus60Btn, 4);
            reminderBar.Children.Add(plus60Btn);

            var timeInputBorder = new Border
            {
                Background = _palette.RaisedBackground,
                BorderBrush = _palette.HairlineBorder,
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(4, 2, 4, 2),
                Width = 46,
                Margin = new Thickness(0, 0, 3, 0),
                ToolTip = "Введите время напоминания в формате ЧЧ:ММ"
            };
            var timeHost = ThemeTokens.CreateInputWithPlaceholder(_palette, "18:30", out _reminderTimeBox, 10.5, false);
            _reminderTimeBox.FontFamily = ThemeTokens.MonoFont;
            _reminderTimeBox.TextAlignment = TextAlignment.Center;
            _reminderTimeBox.Text = DateTime.Now.AddMinutes(15).ToString("HH:mm");
            _reminderTimeBox.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    ApplyCustomTimeReminder();
                    e.Handled = true;
                }
            };
            timeInputBorder.Child = timeHost;
            Grid.SetColumn(timeInputBorder, 5);
            reminderBar.Children.Add(timeInputBorder);

            var setTimeBtn = ThemeTokens.CreateInteractiveButton(
                _palette,
                new TextBlock { Text = "Задать", FontFamily = ThemeTokens.UiFont, FontSize = 10, FontWeight = FontWeights.Medium, Foreground = _palette.PrimaryText },
                ApplyCustomTimeReminder,
                false, false, 5, new Thickness(6, 3, 6, 3), "Установить или сменить время напоминания на указанное ЧЧ:ММ");
            setTimeBtn.Margin = new Thickness(0, 0, 3, 0);
            Grid.SetColumn(setTimeBtn, 6);
            reminderBar.Children.Add(setTimeBtn);

            _disableReminderBtn = ThemeTokens.CreateInteractiveButton(
                _palette,
                new TextBlock { Text = "Выкл ✕", FontFamily = ThemeTokens.UiFont, FontSize = 10, FontWeight = FontWeights.Medium, Foreground = _palette.Destructive },
                DisableCurrentNoteReminder,
                false, false, 5, new Thickness(6, 3, 6, 3), "Отключить напоминание для этой заметки");
            Grid.SetColumn(_disableReminderBtn, 7);
            reminderBar.Children.Add(_disableReminderBtn);

            Grid.SetRow(reminderBar, 3);
            grid.Children.Add(reminderBar);

            return grid;
        }

        public void CreateNewNoteAndOpenEditor()
        {
            var created = _notes.CreateNote("", "");
            OpenNoteEditor(created.Id, focusTitle: true);
        }

        private void OpenNoteEditor(string noteId, bool focusTitle = false)
        {
            if (string.IsNullOrEmpty(noteId)) return;
            _selectedNoteId = noteId;
            _isEditingNote = true;

            if (_editorEmpty != null) _editorEmpty.Visibility = Visibility.Collapsed;
            if (_noteEditorViewGrid != null)
            {
                _noteEditorViewGrid.Visibility = Visibility.Visible;
                ThemeTokens.Reveal(_noteEditorViewGrid, 4, 170);
            }

            LoadSelectedNoteIntoEditor();
            RefreshNotesList();
            if (focusTitle)
            {
                _noteTitleBox?.Focus();
            }
            else
            {
                _noteContentBox?.Focus();
            }
        }

        private void CloseNoteEditorToList()
        {
            // Auto-clean completely blank untitled notes if the user backed out without typing anything
            if (!string.IsNullOrEmpty(_selectedNoteId))
            {
                var current = _notes.Notes.FirstOrDefault(n => n.Id == _selectedNoteId);
                if (current != null &&
                    string.IsNullOrWhiteSpace(current.Title) &&
                    string.IsNullOrWhiteSpace(current.Content) &&
                    !current.ReminderAt.HasValue)
                {
                    _notes.DeleteNote(_selectedNoteId);
                }
            }

            _isEditingNote = false;
            _selectedNoteId = null;
            if (_editorEmpty != null) _editorEmpty.Visibility = Visibility.Visible;
            if (_noteEditorViewGrid != null) _noteEditorViewGrid.Visibility = Visibility.Collapsed;
            if (_notesListViewGrid != null) _notesListViewGrid.Visibility = Visibility.Visible;
            RefreshNotesList();
        }

        private void RefreshNotesList()
        {
            if (_notesListPanel == null) return;
            _notesListPanel.Children.Clear();

            if (_notesCountText != null)
            {
                _notesCountText.Text = _notes.Notes.Count.ToString();
            }

            string q = _noteSearchBox?.Text ?? string.Empty;
            var list = _notes.Search(q);

            if (list.Count == 0)
            {
                var emptyStack = new StackPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 38, 0, 0)
                };
                emptyStack.Children.Add(new TextBlock
                {
                    Text = string.IsNullOrWhiteSpace(q) ? "Список заметок пуст" : "По вашему запросу заметок не найдено",
                    FontFamily = ThemeTokens.UiFont,
                    FontSize = 12,
                    Foreground = _palette.MutedText,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 8)
                });
                if (string.IsNullOrWhiteSpace(q))
                {
                    var createFirstBtn = ThemeTokens.CreateInteractiveButton(
                        _palette,
                        new TextBlock
                        {
                            Text = "+ Создать первую заметку",
                            FontFamily = ThemeTokens.UiFont,
                            FontSize = 11.5,
                            FontWeight = FontWeights.SemiBold,
                            Foreground = _palette.OnAccentText
                        },
                        CreateNewNoteAndOpenEditor,
                        true, false, 7, new Thickness(12, 6, 12, 6));
                    createFirstBtn.HorizontalAlignment = HorizontalAlignment.Center;
                    emptyStack.Children.Add(createFirstBtn);
                }
                _notesListPanel.Children.Add(emptyStack);
                return;
            }

            foreach (var note in list)
            {
                _notesListPanel.Children.Add(BuildSeparateNoteCard(note));
            }
        }

        private UIElement BuildSeparateNoteCard(NoteItem note)
        {
            string noteId = note.Id;
            bool hasActiveReminder = note.ReminderAt.HasValue && !note.ReminderCompleted;

            var rowGrid = new Grid();
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Left: Title + Timestamp + Content snippet
            var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };

            var headerLine = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            if (note.IsPinned)
            {
                headerLine.Children.Add(new TextBlock
                {
                    Text = "★ ",
                    FontFamily = ThemeTokens.UiFont,
                    FontSize = 11.5,
                    FontWeight = FontWeights.Bold,
                    Foreground = _palette.PrimaryText,
                    VerticalAlignment = VerticalAlignment.Center
                });
            }

            headerLine.Children.Add(new TextBlock
            {
                Text = note.DisplayTitle,
                FontFamily = ThemeTokens.UiFont,
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = _palette.PrimaryText,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 138,
                VerticalAlignment = VerticalAlignment.Center
            });

            headerLine.Children.Add(new TextBlock
            {
                Text = note.UpdatedAt.Date == DateTime.Today
                    ? note.UpdatedAt.ToString("HH:mm")
                    : note.UpdatedAt.ToString("dd.MM HH:mm"),
                FontFamily = ThemeTokens.MonoFont,
                FontSize = 10,
                Foreground = _palette.MutedText,
                Margin = new Thickness(7, 1, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            textStack.Children.Add(headerLine);

            string snippet = (note.Content ?? string.Empty).Replace("\r\n", " ").Replace("\n", " ").Trim();
            if (string.IsNullOrEmpty(snippet))
            {
                snippet = "Нажмите для редактирования или настройки напоминания...";
            }
            textStack.Children.Add(new TextBlock
            {
                Text = snippet,
                FontFamily = ThemeTokens.UiFont,
                FontSize = 11,
                Foreground = string.IsNullOrWhiteSpace(note.Content) ? _palette.MutedText : _palette.SecondaryText,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 2, 0, 0)
            });

            Grid.SetColumn(textStack, 0);
            rowGrid.Children.Add(textStack);

            if (hasActiveReminder)
            {
                var reminder = VectorIcons.Create(VectorIcons.Bell, _palette.Positive, 13);
                reminder.ToolTip = note.ReminderAt.Value.ToString("HH:mm");
                Grid.SetColumn(reminder, 1);
                rowGrid.Children.Add(reminder);
            }

            var cardBtn = ThemeTokens.CreateInteractiveButton(
                _palette,
                rowGrid,
                () => OpenNoteEditor(noteId),
                false, false, 9, new Thickness(11, 8, 11, 8), "Нажмите, чтобы отредактировать заметку и настроить напоминание");
            if (_selectedNoteId == noteId) cardBtn.Background = _palette.AccentSubtle;
            cardBtn.Margin = new Thickness(0, 0, 0, 6);
            return cardBtn;
        }

        // ==================== БЛОК 1: КОМПАКТНЫЙ ТАЙМЕР ====================
        private UIElement BuildCompactTimerBlock()
        {
            var card = ThemeTokens.CreateCard(_palette, 12, new Thickness(12, 10, 12, 10));
            var stack = new StackPanel();

            var header = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            header.Children.Add(new TextBlock
            {
                Text = "Обратный отсчёт",
                FontFamily = ThemeTokens.UiFont,
                FontSize = 17,
                FontWeight = FontWeights.Medium,
                Foreground = _palette.SecondaryText,
                VerticalAlignment = VerticalAlignment.Center
            });

            _timerStatusBadge = new TextBlock
            {
                Text = "Готов",
                FontFamily = ThemeTokens.UiFont,
                FontSize = 10.5,
                Foreground = _palette.MutedText,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(_timerStatusBadge, 1);
            header.Children.Add(_timerStatusBadge);
            stack.Children.Add(header);

            var midGrid = new Grid { Margin = new Thickness(0, 22, 0, 20) };
            midGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            midGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _timerDisplayLabel = new TextBlock
            {
                Text = _timers.FormattedTimerRemaining,
                FontFamily = ThemeTokens.MonoFont,
                FontSize = 44,
                FontWeight = FontWeights.SemiBold,
                Foreground = _palette.PrimaryText,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(_timerDisplayLabel, 0);
            midGrid.Children.Add(_timerDisplayLabel);

            var inputGroup = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var minBorder = CreateCompactTimeUnitBox("05", "Минуты", out _timerMinBox);
            var secBorder = CreateCompactTimeUnitBox("00", "Секунды", out _timerSecBox);
            SyncTimerInputsFromDuration();

            inputGroup.Children.Add(minBorder);
            inputGroup.Children.Add(new TextBlock
            {
                Text = ":",
                FontFamily = ThemeTokens.MonoFont,
                FontSize = 12,
                Foreground = _palette.MutedText,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(2, 0, 2, 0)
            });
            inputGroup.Children.Add(secBorder);

            Grid.SetColumn(inputGroup, 1);
            midGrid.Children.Add(inputGroup);
            stack.Children.Add(midGrid);

            var presetsGrid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            int[] presets = { 1, 5, 10, 25 };
            for (int i = 0; i < presets.Length; i++)
            {
                presetsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                if (i < presets.Length - 1)
                {
                    presetsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
                }

                int mins = presets[i];
                var pill = ThemeTokens.CreateInteractiveButton(
                    _palette,
                    new TextBlock
                    {
                        Text = $"{mins} мин",
                        FontFamily = ThemeTokens.MonoFont,
                        FontSize = 10.5,
                        Foreground = _palette.SecondaryText,
                        HorizontalAlignment = HorizontalAlignment.Center
                    },
                    () =>
                    {
                        _timers.ResetTimer();
                        _timers.SetTimerDuration(0, mins, 0);
                        SyncTimerInputsFromDuration();
                        RefreshTimerDisplay();
                    },
                    false, false, 5, new Thickness(0, 3, 0, 3));
                Grid.SetColumn(pill, i * 2);
                presetsGrid.Children.Add(pill);
            }
            stack.Children.Add(presetsGrid);

            var btnGrid = new Grid();
            btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.3, GridUnitType.Star) });
            btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
            btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            _timerStartPauseText = new TextBlock
            {
                Text = "Старт",
                FontFamily = ThemeTokens.UiFont,
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = _palette.OnAccentText,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            var startBtn = ThemeTokens.CreateInteractiveButton(
                _palette,
                _timerStartPauseText,
                () =>
                {
                    if (_timers.IsTimerRunning)
                    {
                        _timers.PauseTimer();
                    }
                    else if (_timers.IsTimerPaused)
                    {
                        _timers.ResumeTimer();
                    }
                    else
                    {
                        ApplyCustomTimerInputs();
                        _timers.StartTimer();
                    }
                    RefreshTimerDisplay();
                },
                true, false, 6, new Thickness(8, 5, 8, 5));
            Grid.SetColumn(startBtn, 0);
            btnGrid.Children.Add(startBtn);

            var resetBtn = ThemeTokens.CreateInteractiveButton(
                _palette,
                new TextBlock
                {
                    Text = "Сброс",
                    FontFamily = ThemeTokens.UiFont,
                    FontSize = 11.5,
                    Foreground = _palette.SecondaryText,
                    HorizontalAlignment = HorizontalAlignment.Center
                },
                () =>
                {
                    _timers.ResetTimer();
                    RefreshTimerDisplay();
                },
                false, false, 6, new Thickness(8, 5, 8, 5));
            Grid.SetColumn(resetBtn, 2);
            btnGrid.Children.Add(resetBtn);

            stack.Children.Clear();
            stack.Children.Add(header);
            stack.Children.Add(midGrid);
            _timerProgress = new ProgressBar
            {
                Height = 4, Minimum = 0, Maximum = 1, Foreground = _palette.Accent,
                Background = _palette.RaisedBackground, Margin = new Thickness(0, 0, 0, 15)
            };
            _timerProgress.SetResourceReference(StyleProperty, "IslandMeter");
            stack.Children.Add(_timerProgress);
            stack.Children.Add(presetsGrid);
            stack.Children.Add(btnGrid);
            stack.VerticalAlignment = VerticalAlignment.Center;
            card.Child = stack;
            return card;
        }

        // ==================== БЛОК 2: КОМПАКТНЫЙ СЕКУНДОМЕР ====================
        private UIElement BuildCompactStopwatchBlock()
        {
            var card = ThemeTokens.CreateCard(_palette, 12, new Thickness(12, 10, 12, 10));
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var topRow = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            topRow.Children.Add(new TextBlock
            {
                Text = "Секундомер",
                FontFamily = ThemeTokens.UiFont,
                FontSize = 17,
                FontWeight = FontWeights.Medium,
                Foreground = _palette.SecondaryText,
                VerticalAlignment = VerticalAlignment.Center
            });

            _stopwatchDisplayLabel = new TextBlock
            {
                Text = _timers.FormattedStopwatch,
                FontFamily = ThemeTokens.MonoFont,
                FontSize = 24,
                FontWeight = FontWeights.SemiBold,
                Foreground = _palette.PrimaryText,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(_stopwatchDisplayLabel, 1);
            topRow.Children.Add(_stopwatchDisplayLabel);
            Grid.SetRow(topRow, 0);
            grid.Children.Add(topRow);

            var btnGrid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });
            btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
            btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
            btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            _stopwatchStartPauseText = new TextBlock
            {
                Text = "Старт",
                FontFamily = ThemeTokens.UiFont,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = _palette.OnAccentText,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            var swStartBtn = ThemeTokens.CreateInteractiveButton(
                _palette,
                _stopwatchStartPauseText,
                () =>
                {
                    if (_timers.IsStopwatchRunning) _timers.PauseStopwatch();
                    else _timers.StartStopwatch();
                    RefreshStopwatchDisplay();
                },
                true, false, 6, new Thickness(6, 4, 6, 4));
            Grid.SetColumn(swStartBtn, 0);
            btnGrid.Children.Add(swStartBtn);

            var swLapBtn = ThemeTokens.CreateInteractiveButton(
                _palette,
                new TextBlock { Text = "Круг", FontFamily = ThemeTokens.UiFont, FontSize = 11, Foreground = _palette.PrimaryText, HorizontalAlignment = HorizontalAlignment.Center },
                () =>
                {
                    _timers.RecordLap();
                    RefreshStopwatchLaps();
                },
                false, false, 6, new Thickness(6, 4, 6, 4));
            Grid.SetColumn(swLapBtn, 2);
            btnGrid.Children.Add(swLapBtn);

            var swResetBtn = ThemeTokens.CreateInteractiveButton(
                _palette,
                new TextBlock { Text = "Сброс", FontFamily = ThemeTokens.UiFont, FontSize = 11, Foreground = _palette.SecondaryText, HorizontalAlignment = HorizontalAlignment.Center },
                () =>
                {
                    _timers.ResetStopwatch();
                    RefreshStopwatchDisplay();
                    RefreshStopwatchLaps();
                },
                false, false, 6, new Thickness(6, 4, 6, 4));
            Grid.SetColumn(swResetBtn, 4);
            btnGrid.Children.Add(swResetBtn);

            Grid.SetRow(btnGrid, 1);
            grid.Children.Add(btnGrid);

            _lapsListPanel = new StackPanel();
            var lapsScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = _lapsListPanel
            };
            Grid.SetRow(lapsScroll, 2);
            grid.Children.Add(lapsScroll);

            card.Child = grid;
            return card;
        }

        private Border CreateCompactTimeUnitBox(string initial, string tooltip, out TextBox tb)
        {
            var host = ThemeTokens.CreateInputWithPlaceholder(_palette, initial, out tb, 12, false);
            tb.FontFamily = ThemeTokens.MonoFont;
            tb.TextAlignment = TextAlignment.Center;
            tb.Text = initial;
            tb.LostFocus += (s, e) => ApplyCustomTimerInputs();

            return new Border
            {
                Background = _palette.RaisedBackground,
                BorderBrush = _palette.HairlineBorder,
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(4, 2, 4, 2),
                Width = 30,
                ToolTip = tooltip,
                Child = host
            };
        }

        private void UpdatePinButtonState()
        {
            if (_pinNoteBtn == null) return;
            var note = _notes.Notes.FirstOrDefault(n => n.Id == _selectedNoteId);
            bool pinned = note?.IsPinned ?? false;
            _pinNoteBtn.Background = pinned ? _palette.AccentSubtle : _palette.RaisedBackground;
            if (_pinNoteIcon != null)
            {
                _pinNoteIcon.Stroke = pinned ? _palette.PrimaryText : _palette.MutedText;
            }
        }

        private void LoadSelectedNoteIntoEditor()
        {
            if (_noteTitleBox == null || _noteContentBox == null) return;
            _suppressNoteAutoSave = true;

            var note = _notes.Notes.FirstOrDefault(n => n.Id == _selectedNoteId);
            if (note != null)
            {
                _noteTitleBox.Text = note.Title ?? string.Empty;
                _noteContentBox.Text = note.Content ?? string.Empty;
                if (_reminderTimeBox != null)
                {
                    _reminderTimeBox.Text = (note.ReminderAt.HasValue && !note.ReminderCompleted)
                        ? note.ReminderAt.Value.ToString("HH:mm")
                        : DateTime.Now.AddMinutes(15).ToString("HH:mm");
                }
                UpdateReminderStatusLabel(note);
                UpdatePinButtonState();
            }
            else
            {
                _noteTitleBox.Text = string.Empty;
                _noteContentBox.Text = string.Empty;
                if (_reminderStatusText != null) _reminderStatusText.Text = "Без напоминания";
            }

            _suppressNoteAutoSave = false;
        }

        private void OnEditorTextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressNoteAutoSave || string.IsNullOrEmpty(_selectedNoteId)) return;
            _notes.UpdateNote(_selectedNoteId, _noteTitleBox.Text, _noteContentBox.Text);
        }

        private void SetQuickReminderMinutes(int mins)
        {
            if (string.IsNullOrEmpty(_selectedNoteId)) return;
            DateTime target = DateTime.Now.AddMinutes(mins);
            _notes.SetReminder(_selectedNoteId, target);
            if (_reminderTimeBox != null)
            {
                _reminderTimeBox.Text = target.ToString("HH:mm");
            }
            var note = _notes.Notes.FirstOrDefault(n => n.Id == _selectedNoteId);
            UpdateReminderStatusLabel(note);
        }

        private void ApplyCustomTimeReminder()
        {
            if (string.IsNullOrEmpty(_selectedNoteId) || _reminderTimeBox == null) return;
            string raw = _reminderTimeBox.Text.Trim();
            if (TimeSpan.TryParseExact(raw, new[] { @"hh\:mm", @"h\:mm" }, CultureInfo.InvariantCulture, out TimeSpan ts))
            {
                DateTime target = DateTime.Today.Add(ts);
                if (target <= DateTime.Now) target = target.AddDays(1);
                _notes.SetReminder(_selectedNoteId, target);
            }
            var note = _notes.Notes.FirstOrDefault(n => n.Id == _selectedNoteId);
            UpdateReminderStatusLabel(note);
        }

        private void DisableCurrentNoteReminder()
        {
            if (string.IsNullOrEmpty(_selectedNoteId)) return;
            _notes.SetReminder(_selectedNoteId, null);
            var note = _notes.Notes.FirstOrDefault(n => n.Id == _selectedNoteId);
            UpdateReminderStatusLabel(note);
        }

        private void UpdateReminderStatusLabel(NoteItem note)
        {
            if (_reminderStatusText == null) return;
            bool active = note?.ReminderAt != null && !note.ReminderCompleted;
            if (active)
            {
                _reminderStatusText.Text = "Напоминание в " + note.ReminderAt.Value.ToString("HH:mm");
                _reminderStatusText.Foreground = _palette.Positive;
            }
            else
            {
                _reminderStatusText.Text = "Без напоминания";
                _reminderStatusText.Foreground = _palette.MutedText;
            }

            if (_disableReminderBtn != null)
            {
                _disableReminderBtn.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void SyncTimerInputsFromDuration()
        {
            if (_timerMinBox == null || _timerSecBox == null) return;
            var d = _timers.TimerTotalDuration;
            int totalMins = (int)d.TotalMinutes;
            _timerMinBox.Text = totalMins.ToString("00");
            _timerSecBox.Text = d.Seconds.ToString("00");
        }

        private void ApplyCustomTimerInputs()
        {
            int.TryParse(_timerMinBox?.Text, out int m);
            int.TryParse(_timerSecBox?.Text, out int s);
            if (m <= 0 && s <= 0) m = 5;
            _timers.SetTimerDuration(0, m, s);
            RefreshTimerDisplay();
        }

        private void RefreshTimerDisplay()
        {
            if (_timerDisplayLabel != null)
            {
                _timerDisplayLabel.Text = _timers.FormattedTimerRemaining;
                if (_timerProgress != null)
                    _timerProgress.Value = Math.Max(0, Math.Min(1,
                        _timers.TimerRemaining.TotalSeconds / Math.Max(1, _timers.TimerTotalDuration.TotalSeconds)));
            }
            if (_timerStatusBadge != null)
            {
                _timerStatusBadge.Text = _timers.IsTimerRunning ? "Активен" : (_timers.IsTimerPaused ? "Пауза" : "Готов");
                _timerStatusBadge.Foreground = _timers.IsTimerRunning ? _palette.Positive : _palette.MutedText;
            }
            if (_timerStartPauseText != null)
            {
                _timerStartPauseText.Text = _timers.IsTimerRunning ? "Пауза" : (_timers.IsTimerPaused ? "Продолжить" : "Старт");
            }
        }

        private void RefreshStopwatchDisplay()
        {
            if (_stopwatchDisplayLabel != null)
            {
                _stopwatchDisplayLabel.Text = _timers.FormattedStopwatch;
            }
            if (_stopwatchStartPauseText != null)
            {
                _stopwatchStartPauseText.Text = _timers.IsStopwatchRunning
                    ? "Пауза"
                    : (_timers.StopwatchElapsed > TimeSpan.Zero ? "Продолжить" : "Старт");
            }
            if (_timers.Laps.Count != _renderedLapCount)
            {
                RefreshStopwatchLaps();
            }
        }

        private void RefreshStopwatchLaps()
        {
            if (_lapsListPanel == null) return;
            _lapsListPanel.Children.Clear();
            _renderedLapCount = _timers.Laps.Count;

            if (_timers.Laps.Count == 0)
            {
                _lapsListPanel.Children.Add(new Border
                {
                    Padding = new Thickness(6, 30, 6, 18),
                    Child = ThemeTokens.CreateEmptyState(_palette, VectorIcons.Clock, "Круги",
                        "Запустите секундомер и нажмите «Круг», чтобы сохранить промежуточное время.")
                });
                return;
            }

            foreach (var lap in _timers.Laps)
            {
                var row = new Grid { Margin = new Thickness(0, 1, 0, 1) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                row.Children.Add(new TextBlock
                {
                    Text = $"Круг {lap.LapNumber}",
                    FontFamily = ThemeTokens.UiFont,
                    FontSize = 10.5,
                    Foreground = _palette.SecondaryText
                });

                var timeTb = new TextBlock
                {
                    Text = TimerService.FormatStopwatchTime(lap.SplitTime),
                    FontFamily = ThemeTokens.MonoFont,
                    FontSize = 10.5,
                    Foreground = _palette.PrimaryText
                };
                Grid.SetColumn(timeTb, 1);
                row.Children.Add(timeTb);

                _lapsListPanel.Children.Add(row);
            }
        }
    }
}
