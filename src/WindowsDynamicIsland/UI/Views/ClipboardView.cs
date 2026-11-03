using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using WindowsDynamicIsland.Models;
using WindowsDynamicIsland.Services;

namespace WindowsDynamicIsland.UI.Views
{
    public sealed class ClipboardView : UserControl
    {
        private readonly SettingsManager _settings;
        private readonly ClipboardManager _clipboard;
        private ThemePalette _palette;

        private TextBox _searchBox;
        private StackPanel _listPanel;
        private TextBlock _footerHintText;
        private TextBlock _statusToastText;
        private DispatcherTimer _copiedStatusTimer;
        private Point _dragStartPoint;
        private bool _isDragging;

        public ClipboardView(SettingsManager settings, ClipboardManager clipboard)
        {
            _settings = settings;
            _clipboard = clipboard;
            _clipboard.HistoryChanged += RefreshList;
            RebuildTheme();
        }

        public void RebuildTheme()
        {
            _palette = ThemeTokens.GetPalette(_settings.Current.Theme, 1.0);

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Top Toolbar: Search + Privacy Toggle + Clear History
            var toolbar = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var searchBorder = new Border
            {
                Background = _palette.CardBackground,
                BorderBrush = _palette.HairlineBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(10, 5, 10, 5)
            };

            var searchGrid = new Grid();
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var sIcon = VectorIcons.Create(VectorIcons.Search, _palette.MutedText, 13.5);
            sIcon.Margin = new Thickness(0, 0, 8, 0);
            Grid.SetColumn(sIcon, 0);
            searchGrid.Children.Add(sIcon);

            var inputHost = ThemeTokens.CreateInputWithPlaceholder(_palette, "Поиск в истории буфера...", out _searchBox, 12, false);
            _searchBox.TextChanged += (s, e) => RefreshList();
            Grid.SetColumn(inputHost, 1);
            searchGrid.Children.Add(inputHost);
            searchBorder.Child = searchGrid;
            Grid.SetColumn(searchBorder, 0);
            toolbar.Children.Add(searchBorder);

            bool monitoring = _settings.Current.ClipboardMonitoringEnabled;
            var monitorBtn = ThemeTokens.CreateInteractiveButton(
                _palette,
                new TextBlock
                {
                    Text = monitoring ? "Запись: ВКЛ" : "Запись: ПАУЗА",
                    FontFamily = ThemeTokens.MonoFont,
                    FontSize = 11,
                    Foreground = monitoring ? _palette.Positive : _palette.Warning
                },
                () =>
                {
                    _settings.Current.ClipboardMonitoringEnabled = !_settings.Current.ClipboardMonitoringEnabled;
                    _settings.Save();
                    RebuildTheme();
                },
                false, false, 8, new Thickness(10, 6, 10, 6), "Включить или приостановить мониторинг буфера обмена");
            Grid.SetColumn(monitorBtn, 2);
            toolbar.Children.Add(monitorBtn);

            var clearBtn = ThemeTokens.CreateInteractiveButton(
                _palette,
                new TextBlock
                {
                    Text = "Очистить",
                    FontFamily = ThemeTokens.UiFont,
                    FontSize = 12,
                    Foreground = _palette.SecondaryText
                },
                () => _clipboard.ClearAll(keepPinned: true),
                false, false, 8, new Thickness(12, 6, 12, 6), "Очистить незакреплённые записи");
            Grid.SetColumn(clearBtn, 4);
            toolbar.Children.Add(clearBtn);

            Grid.SetRow(toolbar, 0);
            root.Children.Add(toolbar);

            // Scrollable list of clipboard entries
            _listPanel = new StackPanel();
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = _listPanel
            };
            Grid.SetRow(scroll, 1);
            root.Children.Add(scroll);

            // Bottom hint bar
            var footer = new Grid { Margin = new Thickness(0, 6, 0, 0) };

            _footerHintText = new TextBlock
            {
                Text = "Нажмите для копирования или перетащите запись мышью в нужное окно",
                FontFamily = ThemeTokens.UiFont,
                FontSize = 11,
                Foreground = _palette.MutedText,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            footer.Children.Add(_footerHintText);

            _statusToastText = new TextBlock
            {
                Text = "Скопировано в буфер ✓",
                FontFamily = ThemeTokens.UiFont,
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = _palette.Positive,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed
            };
            footer.Children.Add(_statusToastText);

            Grid.SetRow(footer, 2);
            root.Children.Add(footer);

            Content = root;
            RefreshList();
        }

        private void RefreshList()
        {
            if (_listPanel == null) return;
            _listPanel.Children.Clear();

            string query = _searchBox?.Text ?? string.Empty;
            var entries = _clipboard.Search(query);

            if (entries.Count == 0)
            {
                var emptyCard = ThemeTokens.CreateCard(_palette, 12, new Thickness(24));
                var emptyStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                emptyStack.Children.Add(VectorIcons.Create(VectorIcons.Clipboard, _palette.MutedText, 24));
                emptyStack.Children.Add(new TextBlock
                {
                    Text = string.IsNullOrWhiteSpace(query)
                        ? "История буфера обмена пуста. Скопируйте любой текст (Ctrl+C)"
                        : "Записи по вашему запросу не найдены",
                    FontFamily = ThemeTokens.UiFont,
                    FontSize = 12.5,
                    Foreground = _palette.MutedText,
                    Margin = new Thickness(0, 8, 0, 0),
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                emptyCard.Child = emptyStack;
                _listPanel.Children.Add(emptyCard);
                return;
            }

            foreach (var item in entries)
            {
                _listPanel.Children.Add(BuildEntryRow(item));
            }
        }

        private UIElement BuildEntryRow(ClipboardEntry entry)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var mainText = new TextBlock
            {
                Text = entry.SingleLinePreview,
                FontFamily = ThemeTokens.UiFont,
                FontSize = 12.5,
                FontWeight = FontWeights.Medium,
                Foreground = _palette.PrimaryText,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            textStack.Children.Add(mainText);

            string meta = entry.FormattedTime;
            if (entry.LineCount > 1) meta += $" • {entry.LineCount} стр.";
            if (!string.IsNullOrWhiteSpace(entry.SourceApp)) meta += $" • {entry.SourceApp}";

            textStack.Children.Add(new TextBlock
            {
                Text = meta,
                FontFamily = ThemeTokens.MonoFont,
                FontSize = 10.5,
                Foreground = _palette.MutedText,
                Margin = new Thickness(0, 2, 0, 0)
            });

            Grid.SetColumn(textStack, 0);
            grid.Children.Add(textStack);

            // Pin button
            var pinBtn = ThemeTokens.CreateInteractiveButton(
                _palette,
                VectorIcons.Create(VectorIcons.Pin, entry.IsPinned ? _palette.Accent : _palette.MutedText, 13),
                () => _clipboard.TogglePin(entry.Id),
                false, false, 6, new Thickness(7, 5, 7, 5), entry.IsPinned ? "Открепить" : "Закрепить");
            pinBtn.Margin = new Thickness(8, 0, 4, 0);
            Grid.SetColumn(pinBtn, 1);
            grid.Children.Add(pinBtn);

            // Delete button
            var delBtn = ThemeTokens.CreateInteractiveButton(
                _palette,
                VectorIcons.Create(VectorIcons.Trash, _palette.MutedText, 13),
                () => _clipboard.DeleteEntry(entry.Id),
                false, false, 6, new Thickness(7, 5, 7, 5), "Удалить запись");
            Grid.SetColumn(delBtn, 2);
            grid.Children.Add(delBtn);

            var card = new Border
            {
                Background = entry.IsPinned ? _palette.RaisedBackground : _palette.CardBackground,
                BorderBrush = entry.IsPinned ? _palette.Accent : _palette.HairlineBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 8, 10, 8),
                Margin = new Thickness(0, 0, 0, 6),
                Cursor = Cursors.Hand,
                Child = grid,
                ToolTip = entry.Text.Length > 400 ? entry.Text.Substring(0, 400) + "…" : entry.Text
            };

            card.MouseEnter += (s, e) => card.Background = _palette.HoverBackground;
            card.MouseLeave += (s, e) => card.Background = entry.IsPinned ? _palette.RaisedBackground : _palette.CardBackground;

            // Support BOTH click-to-copy AND native Windows OLE Drag-and-Drop to external apps!
            card.PreviewMouseLeftButtonDown += (s, e) =>
            {
                _dragStartPoint = e.GetPosition(null);
                _isDragging = false;
            };

            card.PreviewMouseMove += (s, e) =>
            {
                if (e.LeftButton == MouseButtonState.Pressed && !_isDragging)
                {
                    Point pos = e.GetPosition(null);
                    if (Math.Abs(pos.X - _dragStartPoint.X) > SystemParameters.MinimumHorizontalDragDistance ||
                        Math.Abs(pos.Y - _dragStartPoint.Y) > SystemParameters.MinimumVerticalDragDistance)
                    {
                        _isDragging = true;
                        try
                        {
                            var dataObj = new DataObject();
                            dataObj.SetData(DataFormats.UnicodeText, entry.Text);
                            dataObj.SetData(DataFormats.Text, entry.Text);
                            dataObj.SetData(DataFormats.StringFormat, entry.Text);
                            DragDrop.DoDragDrop(card, dataObj, DragDropEffects.Copy);
                        }
                        catch
                        {
                        }
                        finally
                        {
                            _isDragging = false;
                        }
                    }
                }
            };

            card.MouseLeftButtonUp += (s, e) =>
            {
                if (!_isDragging)
                {
                    _clipboard.CopyEntryToSystemClipboard(entry);
                    ShowCopiedFeedback();
                }
            };

            return card;
        }

        private void ShowCopiedFeedback()
        {
            if (_statusToastText != null)
            {
                _statusToastText.Text = "Скопировано в буфер ✓";
                _statusToastText.Visibility = Visibility.Visible;
            }
            if (_footerHintText != null)
            {
                _footerHintText.Visibility = Visibility.Collapsed;
            }
            if (_copiedStatusTimer == null)
            {
                _copiedStatusTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2200) };
                _copiedStatusTimer.Tick += (s, e) =>
                {
                    _copiedStatusTimer.Stop();
                    if (_statusToastText != null) _statusToastText.Visibility = Visibility.Collapsed;
                    if (_footerHintText != null) _footerHintText.Visibility = Visibility.Visible;
                };
            }
            _copiedStatusTimer.Stop();
            _copiedStatusTimer.Start();
        }
    }
}
