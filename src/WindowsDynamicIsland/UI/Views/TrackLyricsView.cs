using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using WindowsDynamicIsland.Services;

namespace WindowsDynamicIsland.UI.Views
{
    internal sealed class TrackLyricsView : ScrollViewer
    {
        private readonly MediaSessionService _media;
        private readonly ThemePalette _palette;
        private readonly StackPanel _lines = new StackPanel();
        private TrackLyrics _lyrics;
        private CancellationTokenSource _request;
        private int _trackVersion = -1;
        private int _active = -1;

        public TrackLyricsView(MediaSessionService media, ThemePalette palette)
        {
            _media = media;
            _palette = palette;
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            Content = _lines;
            Visibility = Visibility.Collapsed;
            IsVisibleChanged += (s, e) =>
            {
                if (IsVisible) Refresh();
                else CancelRequest();
            };
            Unloaded += (s, e) => CancelRequest();
        }

        public void Refresh()
        {
            if (!IsVisible) return;
            if (_trackVersion != _media.TrackVersion)
            {
                CancelRequest();
                _trackVersion = _media.TrackVersion;
                _lyrics = null;
                _active = -1;
                ScrollToTop();
                if (string.IsNullOrWhiteSpace(_media.TrackArtist))
                    ShowStatus(_media.HasActiveSession ? "Для поиска текста нужен исполнитель." : "Включите трек, чтобы открыть текст.");
                else
                    _ = LoadAsync();
            }
            UpdateActiveLine();
        }

        private void CancelRequest()
        {
            if (_request == null) return;
            var request = _request;
            _request = null;
            _trackVersion = -1;
            request.Cancel();
        }

        private async Task LoadAsync()
        {
            using (var request = new CancellationTokenSource())
            {
                _request = request;
                ShowStatus("Ищем текст…");
                try
                {
                    var lyrics = await LyricsService.FetchAsync(
                        _media.Title, _media.TrackArtist, _media.Album, _media.Duration, request.Token);
                    if (request != _request || _trackVersion != _media.TrackVersion) return;
                    _lyrics = lyrics;
                    RenderLyrics();
                }
                catch (OperationCanceledException) when (request.IsCancellationRequested)
                {
                }
                catch (Exception ex)
                {
                    if (request != _request) return;
                    StorageService.LogError("TrackLyricsView.Load", ex);
                    ShowStatus("Не удалось загрузить текст.");
                    var retry = new Button
                    {
                        Content = "Повторить",
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Padding = new Thickness(10, 4, 10, 4),
                        Margin = new Thickness(0, 8, 0, 0)
                    };
                    retry.SetResourceReference(StyleProperty, "IslandPlayerButton");
                    retry.Foreground = _palette.PrimaryText;
                    retry.Background = _palette.RaisedBackground;
                    retry.BorderBrush = _palette.HairlineBorder;
                    retry.Click += (s, e) => _ = LoadAsync();
                    _lines.Children.Add(retry);
                }
                finally
                {
                    if (request == _request) _request = null;
                }
            }
        }

        private void ShowStatus(string message)
        {
            _lines.Children.Clear();
            _lines.Children.Add(CreateText(message, 13));
        }

        private void RenderLyrics()
        {
            if (_lyrics.Lines.Count == 0)
            {
                if (_lyrics.Instrumental) ShowStatus("Инструментальный трек");
                else if (string.IsNullOrWhiteSpace(_lyrics.PlainText)) ShowStatus("Текст этого трека не найден.");
                else
                {
                    ShowStatus("Текст без синхронизации");
                    var plain = CreateText(_lyrics.PlainText, 16);
                    plain.Foreground = _palette.PrimaryText;
                    plain.Margin = new Thickness(0, 10, 12, 0);
                    _lines.Children.Add(plain);
                }
                return;
            }

            _lines.Children.Clear();
            foreach (var line in _lyrics.Lines)
            {
                var text = CreateText(line.Text, 18);
                text.Margin = new Thickness(0, 0, 10, 12);
                text.FontWeight = FontWeights.SemiBold;
                text.Foreground = _palette.MutedText;
                if (line.Text.Length == 0) text.Visibility = Visibility.Collapsed;
                _lines.Children.Add(text);
            }
            UpdateActiveLine();
        }

        private TextBlock CreateText(string text, double size)
        {
            return new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                FontFamily = ThemeTokens.UiFont,
                FontSize = size,
                Foreground = _palette.SecondaryText
            };
        }

        private void UpdateActiveLine()
        {
            if (_lyrics == null || _lyrics.Lines.Count == 0) return;
            int active = _lyrics.ActiveLine(_media.Position);
            if (active == _active) return;
            if (_active >= 0) ((TextBlock)_lines.Children[_active]).Foreground = _palette.MutedText;
            _active = active;
            if (active < 0)
            {
                ScrollToTop();
                return;
            }

            var line = (TextBlock)_lines.Children[active];
            line.Foreground = _palette.PrimaryText;
            if (line.Visibility == Visibility.Collapsed) return;
            Dispatcher.InvokeAsync(() =>
            {
                if (!IsVisible || _active != active || !_lines.Children.Contains(line)) return;
                double top = line.TranslatePoint(new Point(), _lines).Y;
                ScrollToVerticalOffset(top - Math.Max(0, (ViewportHeight - line.ActualHeight) * 0.35));
            }, DispatcherPriority.Loaded);
        }
    }
}
