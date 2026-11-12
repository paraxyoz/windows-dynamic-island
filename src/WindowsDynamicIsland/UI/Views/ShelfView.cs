using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using WindowsDynamicIsland.Models;
using WindowsDynamicIsland.Services;
using WindowsDynamicIsland.Win32;

namespace WindowsDynamicIsland.UI.Views
{
    public sealed class ShelfView : UserControl
    {
        private readonly SettingsManager _settings;
        private readonly FileShelfManager _shelf;
        private ThemePalette _palette;

        private StackPanel _itemsPanel;
        private ScrollViewer _shelfScroll;
        private Border _dropZoneBorder;
        private TextBlock _statusText;
        private Border _confirmDeleteBanner;
        private TextBlock _confirmDeleteLabel;
        private ShelfItem _pendingDeleteItem;

        private Point _dragStartPoint;
        private bool _isDraggingOut;

        public ShelfView(SettingsManager settings, FileShelfManager shelf)
        {
            _settings = settings;
            _shelf = shelf;
            _shelf.ShelfChanged += RefreshItems;
            AllowDrop = true;
            RebuildTheme();
        }

        public void RebuildTheme()
        {
            _palette = ThemeTokens.GetPalette(_settings.Current.Theme, 1.0);

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Toolbar
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Confirm Delete Banner
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Shelf Drop/List
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Footer

            // 1. Toolbar
            var toolbar = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var headerText = new TextBlock
            {
                Text = "Файловая полка",
                FontFamily = ThemeTokens.UiFont,
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = _palette.SecondaryText,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 0, 10, 0)
            };
            Grid.SetColumn(headerText, 0);
            toolbar.Children.Add(headerText);

            var addFileContent = new StackPanel { Orientation = Orientation.Horizontal };
            addFileContent.Children.Add(VectorIcons.Create(VectorIcons.Plus, _palette.OnAccentText, 13));
            addFileContent.Children.Add(new TextBlock
            {
                Text = "Добавить файл",
                FontFamily = ThemeTokens.UiFont,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = _palette.OnAccentText,
                Margin = new Thickness(6, 0, 0, 0)
            });

            var addBtn = ThemeTokens.CreateInteractiveButton(
                _palette,
                addFileContent,
                OpenAddFileDialog,
                true, false, 8, new Thickness(12, 6, 12, 6), "Выбрать файлы для добавления на полку");
            Grid.SetColumn(addBtn, 1);
            toolbar.Children.Add(addBtn);

            var clearBtn = ThemeTokens.CreateInteractiveButton(
                _palette,
                new TextBlock
                {
                    Text = "Очистить полку",
                    FontFamily = ThemeTokens.UiFont,
                    FontSize = 12,
                    Foreground = _palette.SecondaryText
                },
                () => _shelf.ClearAll(),
                false, false, 8, new Thickness(12, 6, 12, 6), "Убрать все ссылки с полки (оригинальные файлы останутся на ПК)");
            Grid.SetColumn(clearBtn, 3);
            toolbar.Children.Add(clearBtn);

            Grid.SetRow(toolbar, 0);
            root.Children.Add(toolbar);

            // 2. Mandatory Delete Confirmation Banner
            _confirmDeleteBanner = new Border
            {
                Background = _palette.DestructiveSubtle,
                BorderBrush = _palette.Destructive,
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 0, 0, 8),
                Visibility = Visibility.Collapsed
            };

            var confirmGrid = new Grid();
            confirmGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            confirmGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            confirmGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            confirmGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _confirmDeleteLabel = new TextBlock
            {
                Text = "Удалить файл с компьютера безвозвратно?",
                FontFamily = ThemeTokens.UiFont,
                FontSize = 12,
                FontWeight = FontWeights.Medium,
                Foreground = _palette.PrimaryText,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(_confirmDeleteLabel, 0);
            confirmGrid.Children.Add(_confirmDeleteLabel);

            var confirmYesBtn = ThemeTokens.CreateInteractiveButton(
                _palette,
                new TextBlock { Text = "Удалить с ПК", FontFamily = ThemeTokens.UiFont, FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White },
                ExecuteConfirmedDiskDelete,
                false, true, 6, new Thickness(10, 5, 10, 5));
            Grid.SetColumn(confirmYesBtn, 1);
            confirmGrid.Children.Add(confirmYesBtn);

            var confirmNoBtn = ThemeTokens.CreateInteractiveButton(
                _palette,
                new TextBlock { Text = "Отмена", FontFamily = ThemeTokens.UiFont, FontSize = 12, Foreground = _palette.PrimaryText },
                () =>
                {
                    _pendingDeleteItem = null;
                    _confirmDeleteBanner.Visibility = Visibility.Collapsed;
                },
                false, false, 6, new Thickness(10, 5, 10, 5));
            Grid.SetColumn(confirmNoBtn, 3);
            confirmGrid.Children.Add(confirmNoBtn);

            _confirmDeleteBanner.Child = confirmGrid;
            Grid.SetRow(_confirmDeleteBanner, 1);
            root.Children.Add(_confirmDeleteBanner);

            // 3. Drop Zone & Items ScrollViewer
            _itemsPanel = new StackPanel();
            var scroll = _shelfScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = _itemsPanel
            };

            _dropZoneBorder = new Border
            {
                Background = _palette.CardBackground,
                BorderBrush = _palette.HairlineBorder,
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(6),
                AllowDrop = true,
                Child = scroll
            };

            _dropZoneBorder.DragEnter += OnShelfDragEnter;
            _dropZoneBorder.DragOver += OnShelfDragOver;
            _dropZoneBorder.DragLeave += OnShelfDragLeave;
            _dropZoneBorder.Drop += OnShelfDrop;

            Grid.SetRow(_dropZoneBorder, 2);
            root.Children.Add(_dropZoneBorder);

            // 4. Footer status
            _statusText = new TextBlock
            {
                Text = "Перетащите файлы сюда из Проводника или обратно в Telegram / Discord • Двойной клик открывает файл",
                FontFamily = ThemeTokens.UiFont,
                FontSize = 11,
                Foreground = _palette.MutedText,
                Margin = new Thickness(2, 6, 2, 2),
                Padding = new Thickness(0, 0, 0, 2)
            };
            Grid.SetRow(_statusText, 3);
            root.Children.Add(_statusText);

            Content = root;
            RefreshItems();
        }

        private void OpenAddFileDialog()
        {
            var dlg = new OpenFileDialog
            {
                Title = "Добавить файлы на полку Dynamic Island",
                Multiselect = true,
                CheckFileExists = true
            };
            if (dlg.ShowDialog() == true && dlg.FileNames != null && dlg.FileNames.Length > 0)
            {
                int added = _shelf.AddFiles(dlg.FileNames);
                SetStatus($"Добавлено на полку: {added}");
            }
        }

        private void OnShelfDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Link;
                _dropZoneBorder.BorderBrush = _palette.Accent;
                _dropZoneBorder.Background = _palette.AccentSubtle;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void OnShelfDragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Link;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void OnShelfDragLeave(object sender, DragEventArgs e)
        {
            _dropZoneBorder.BorderBrush = _palette.HairlineBorder;
            _dropZoneBorder.Background = _palette.CardBackground;
        }

        private void OnShelfDrop(object sender, DragEventArgs e)
        {
            _dropZoneBorder.BorderBrush = _palette.HairlineBorder;
            _dropZoneBorder.Background = _palette.CardBackground;

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
                {
                    int added = _shelf.AddFiles(files);
                    SetStatus($"Добавлено на полку: {added}");
                }
            }
            e.Handled = true;
        }

        private void RefreshItems()
        {
            if (_itemsPanel == null) return;
            _itemsPanel.Children.Clear();

            var items = _shelf.Items;
            if (items.Count == 0)
            {
                _dropZoneBorder.Child = ThemeTokens.CreateEmptyState(_palette, VectorIcons.Shelf, "Перетащите файлы сюда",
                    "Остров сохранит ссылки на оригиналы. Перетащите файл с полки в чат или другое приложение.");
                return;
            }

            _dropZoneBorder.Child = _shelfScroll;
            foreach (var item in items)
            {
                item.RefreshMetadata();
                _itemsPanel.Children.Add(BuildShelfRow(item));
            }
        }

        private UIElement BuildShelfRow(ShelfItem item)
        {
            bool exists = item.ExistsOnDisk();

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // 1. File Icon / Thumbnail
            var iconHolder = new Border
            {
                Width = 34,
                Height = 34,
                CornerRadius = new CornerRadius(8),
                Background = _palette.RaisedBackground,
                BorderBrush = _palette.HairlineBorder,
                BorderThickness = new Thickness(0)
            };

            ImageSource shellIcon = exists && !item.IsDirectory ? NativeMethods.ExtractFileIcon(item.FilePath) : null;
            if (shellIcon != null)
            {
                iconHolder.Child = new Image
                {
                    Source = shellIcon,
                    Width = 22,
                    Height = 22,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
            }
            else
            {
                iconHolder.Child = VectorIcons.Create(
                    item.IsDirectory ? VectorIcons.Shelf : VectorIcons.Notes,
                    exists ? _palette.PrimaryText : _palette.Warning,
                    16);
            }
            Grid.SetColumn(iconHolder, 0);
            grid.Children.Add(iconHolder);

            // 2. File Name + Size / Missing status
            var infoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            infoStack.Children.Add(new TextBlock
            {
                Text = item.FileName,
                FontFamily = ThemeTokens.UiFont,
                FontSize = 13,
                FontWeight = FontWeights.Medium,
                Foreground = exists ? _palette.PrimaryText : _palette.Warning,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            infoStack.Children.Add(new TextBlock
            {
                Text = exists ? $"{item.FormattedSize} • {item.FilePath}" : "Оригинальный файл перемещён или удалён",
                FontFamily = ThemeTokens.MonoFont,
                FontSize = 10.5,
                Foreground = exists ? _palette.MutedText : _palette.Warning,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 2, 0, 0)
            });
            Grid.SetColumn(infoStack, 2);
            grid.Children.Add(infoStack);

            // 3. Extension Keycap
            var extTag = ThemeTokens.CreateKeycap(_palette, string.IsNullOrEmpty(item.Extension) ? "ФАЙЛ" : item.Extension);
            extTag.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(extTag, 3);
            grid.Children.Add(extTag);

            // 4. Context Menu "..." Button
            Border moreBtn = null;
            moreBtn = ThemeTokens.CreateInteractiveButton(
                _palette,
                VectorIcons.Create(VectorIcons.MoreDots, _palette.SecondaryText, 15),
                () => OpenItemContextMenu(moreBtn, item),
                false, false, 6, new Thickness(8, 6, 8, 6), "Действия с файлом (...)");
            Grid.SetColumn(moreBtn, 5);
            grid.Children.Add(moreBtn);

            var rowCard = new Border
            {
                Background = _palette.CardBackground,
                BorderBrush = exists ? _palette.HairlineBorder : _palette.Warning,
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, 7, 10, 7),
                Margin = new Thickness(0, 0, 0, 6),
                Cursor = Cursors.Hand,
                Child = grid,
                ToolTip = item.FilePath
            };

            rowCard.MouseEnter += (s, e) => rowCard.Background = _palette.HoverBackground;
            rowCard.MouseLeave += (s, e) => rowCard.Background = _palette.CardBackground;

            // Double-click to open file + Drag-and-Drop file out to Explorer / Telegram / Discord
            rowCard.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ClickCount == 2)
                {
                    e.Handled = true;
                    if (!_shelf.OpenItem(item, out string err))
                    {
                        SetStatus(err);
                    }
                    else
                    {
                        SetStatus($"Открыт: {item.FileName}");
                    }
                    return;
                }
                _dragStartPoint = e.GetPosition(null);
                _isDraggingOut = false;
            };

            rowCard.MouseMove += (s, e) =>
            {
                if (e.LeftButton == MouseButtonState.Pressed && !_isDraggingOut)
                {
                    Point pos = e.GetPosition(null);
                    if (Math.Abs(pos.X - _dragStartPoint.X) > SystemParameters.MinimumHorizontalDragDistance ||
                        Math.Abs(pos.Y - _dragStartPoint.Y) > SystemParameters.MinimumVerticalDragDistance)
                    {
                        if (!item.ExistsOnDisk())
                        {
                            SetStatus("Оригинальный файл был перемещён или удалён.");
                            RefreshItems();
                            return;
                        }

                        _isDraggingOut = true;
                        try
                        {
                            var dataObj = new DataObject(DataFormats.FileDrop, new[] { item.FilePath });
                            DragDrop.DoDragDrop(rowCard, dataObj, DragDropEffects.Copy | DragDropEffects.Move | DragDropEffects.Link);
                        }
                        catch
                        {
                        }
                        finally
                        {
                            _isDraggingOut = false;
                        }
                    }
                }
            };

            return rowCard;
        }

        private void OpenItemContextMenu(FrameworkElement anchor, ShelfItem item)
        {
            var popup = new Popup
            {
                PlacementTarget = anchor,
                Placement = PlacementMode.Bottom,
                StaysOpen = false,
                AllowsTransparency = true
            };

            var menuStack = new StackPanel();

            menuStack.Children.Add(CreateMenuActionRow("Убрать с полки", _palette.PrimaryText, () =>
            {
                popup.IsOpen = false;
                _shelf.RemoveFromShelf(item.Id);
                SetStatus($"Убрано с полки: {item.FileName}");
            }));

            menuStack.Children.Add(CreateMenuActionRow("Показать в проводнике", _palette.PrimaryText, () =>
            {
                popup.IsOpen = false;
                if (!_shelf.RevealInExplorer(item, out string err))
                {
                    SetStatus(err);
                }
            }));

            menuStack.Children.Add(CreateMenuActionRow("Удалить с компьютера...", _palette.Destructive, () =>
            {
                popup.IsOpen = false;
                PromptDiskDelete(item);
            }));

            popup.Child = new Border
            {
                Background = _palette.CardBackground,
                BorderBrush = _palette.HairlineBorder,
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(6),
                MinWidth = 190,
                Child = menuStack
            };

            popup.IsOpen = true;
        }

        private UIElement CreateMenuActionRow(string text, Brush fg, Action onClick)
        {
            var label = new TextBlock
            {
                Text = text,
                FontFamily = ThemeTokens.UiFont,
                FontSize = 12,
                FontWeight = FontWeights.Medium,
                Foreground = fg
            };

            var btn = ThemeTokens.CreateInteractiveButton(
                _palette,
                label,
                onClick,
                false, false, 6, new Thickness(10, 6, 10, 6));
            btn.Margin = new Thickness(0, 0, 0, 2);
            return btn;
        }

        private void PromptDiskDelete(ShelfItem item)
        {
            if (item == null) return;
            _pendingDeleteItem = item;
            _confirmDeleteLabel.Text = $"Удалить «{item.FileName}» с компьютера безвозвратно?";
            _confirmDeleteBanner.Visibility = Visibility.Visible;
        }

        private void ExecuteConfirmedDiskDelete()
        {
            if (_pendingDeleteItem == null) return;
            var target = _pendingDeleteItem;
            _pendingDeleteItem = null;
            _confirmDeleteBanner.Visibility = Visibility.Collapsed;

            if (_shelf.DeleteFromDisk(target, out string err))
            {
                SetStatus($"Файл «{target.FileName}» удалён с компьютера.");
            }
            else
            {
                SetStatus(err);
            }
        }

        private void SetStatus(string message)
        {
            if (_statusText != null && !string.IsNullOrWhiteSpace(message))
            {
                _statusText.Text = message;
            }
        }
    }
}
