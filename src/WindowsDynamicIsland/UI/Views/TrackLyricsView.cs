using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;
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
        private int _activeWord = -2;
        private static readonly DependencyProperty FollowOffsetProperty = DependencyProperty.Register(
            "FollowOffset", typeof(double), typeof(TrackLyricsView), new PropertyMetadata(0.0, (target, change) =>
            {
                ((TrackLyricsView)target).ScrollToVerticalOffset((double)change.NewValue);
            }));

        public TrackLyricsView(MediaSessionService media, ThemePalette palette)
        {
            _media = media;
            _palette = palette;
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            Content = _lines;
            PreviewMouseWheel += (s, e) => BeginAnimation(FollowOffsetProperty, null);
            PanningMode = PanningMode.VerticalOnly;
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
                BeginAnimation(FollowOffsetProperty, null);
                ScrollToTop();
                if (!LyricsService.CanLookup(_media.Title, _media.TrackArtist))
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
            _lines.Margin = new Thickness(0);
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
            _lines.Margin = new Thickness(0, 10, 0, 42);
            foreach (var line in _lyrics.Lines)
            {
                var text = CreateText(line.Words.Count == 0 ? line.Text : string.Empty, 20);
                text.Margin = new Thickness(0, 0, 12, 13);
                text.FontWeight = FontWeights.SemiBold;
                text.Foreground = _palette.PrimaryText;
                text.Opacity = 0.52;
                foreach (var word in line.Words)
                    text.Inlines.Add(new Run(word.Text));

                if (line.Text.Length == 0)
                    text.Visibility = Visibility.Collapsed;
                else
                    text.MouseLeftButtonUp += async (s, e) =>
                    {
                        if (_media.CanSeek && _trackVersion == _media.TrackVersion)
                            await _media.SeekAsync(line.Time, _trackVersion);
                    };
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
            var position = _media.Position;
            int active = _lyrics.ActiveLine(position);
            if (active != _active)
            {
                if (_active >= 0)
                {
                    var previous = (TextBlock)_lines.Children[_active];
                    FadeLine(previous, 0.52);
                    foreach (var run in previous.Inlines) run.ClearValue(TextElement.ForegroundProperty);
                }
                _active = active;
                _activeWord = -2;
                if (active < 0) { Follow(0); return; }

                var line = (TextBlock)_lines.Children[active];
                FadeLine(line, 1);
                if (line.Visibility == Visibility.Visible)
                {
                    Dispatcher.InvokeAsync(() =>
                    {
                        if (!IsVisible || _active != active || !_lines.Children.Contains(line)) return;
                        double top = line.TranslatePoint(new Point(), _lines).Y + _lines.Margin.Top;
                        Follow(top - Math.Max(0, (ViewportHeight - line.ActualHeight) * 0.30));
                    }, DispatcherPriority.Loaded);
                }
            }
            if (active < 0) return;
            var words = _lyrics.Lines[active].Words;
            if (words.Count == 0) return;
            int current = 0;
            while (current < words.Count && position >= words[current].End) current++;
            int highlighted = current < words.Count && position >= words[current].Start ? current : -1;
            int wordState = current * 2 + (highlighted >= 0 ? 1 : 0);
            if (_activeWord == wordState) return;
            _activeWord = wordState;

            int index = 0;
            foreach (var run in ((TextBlock)_lines.Children[active]).Inlines)
            {
                if (index == highlighted && SystemParameters.ClientAreaAnimation &&
                    _palette.Accent is SolidColorBrush accent && _palette.MutedText is SolidColorBrush muted)
                {
                    var glow = new SolidColorBrush(accent.Color);
                    run.Foreground = glow;
                    glow.BeginAnimation(SolidColorBrush.ColorProperty,
                        new ColorAnimation(muted.Color, accent.Color, TimeSpan.FromMilliseconds(105))
                        { FillBehavior = FillBehavior.Stop });
                }
                else
                {
                    run.Foreground = index == highlighted ? _palette.Accent :
                        index < current ? _palette.PrimaryText : _palette.MutedText;
                }
                index++;
            }
        }

        private void Follow(double offset)
        {
            offset = Math.Max(0, Math.Min(ScrollableHeight, offset));
            if (!SystemParameters.ClientAreaAnimation)
            {
                ScrollToVerticalOffset(offset);
                return;
            }
            BeginAnimation(FollowOffsetProperty, new DoubleAnimation(VerticalOffset, offset, TimeSpan.FromMilliseconds(380))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
        }

        private static void FadeLine(TextBlock line, double opacity)
        {
            double from = line.Opacity;
            line.BeginAnimation(OpacityProperty, null);
            line.Opacity = opacity;
            if (SystemParameters.ClientAreaAnimation)
                line.BeginAnimation(OpacityProperty, new DoubleAnimation(from, opacity, TimeSpan.FromMilliseconds(220))
                {
                    FillBehavior = FillBehavior.Stop
                });
        }
    }
}
