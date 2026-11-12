using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using WindowsDynamicIsland.Models;
using WindowsDynamicIsland.Services;

namespace WindowsDynamicIsland.UI
{
    /// <summary>
    /// Expressive, living typographic & emoji companion for the Dynamic Island.
    /// Features natural blinking/double-blinking, idle micro-expressions (glancing left/right, winking, humming),
    /// continuous rhythmic music grooving with dancing notes, direct hover/click Easter-egg reactions,
    /// and instant contextual reactions to system and user events.
    /// </summary>
    public sealed class SmileyBadgeControl : Border
    {
        private readonly SettingsManager _settings;
        private readonly TextBlock _glyphText;
        private readonly UIElement _glassOverlay;
        private readonly TranslateTransform _translate;
        private readonly ScaleTransform _scale;
        private readonly RotateTransform _rotate;
        private readonly DispatcherTimer _lifeTimer;
        private readonly DispatcherTimer _musicGrooveTimer;
        private static readonly Random _rng = new Random();

        private SmileyMood _currentMood = SmileyMood.Normal;
        private ThemePalette _lastPalette;
        private DateTime _nextBlinkUtc;
        private DateTime _nextMicroEmoteUtc;
        private DateTime _reactionLockUntilUtc = DateTime.MinValue;
        private int _musicBeatStep;
        private int _clickComboIndex;
        private bool _isHovered;

        public SmileyBadgeControl(SettingsManager settings)
        {
            _settings = settings;
            _translate = new TranslateTransform(0, 0);
            _scale = new ScaleTransform(1, 1);
            _rotate = new RotateTransform(0);

            var group = new TransformGroup();
            group.Children.Add(_scale);
            group.Children.Add(_rotate);
            group.Children.Add(_translate);

            RenderTransformOrigin = new Point(0.5, 0.5);
            RenderTransform = group;

            CornerRadius = new CornerRadius(11);
            Padding = new Thickness(0);
            Width = 52;
            BorderThickness = new Thickness(0);
            VerticalAlignment = VerticalAlignment.Center;
            Cursor = Cursors.Hand;
            SnapsToDevicePixels = true;
            UseLayoutRounding = true;
            ToolTip = "Живой компаньон острова (нажмите для реакции!)";

            _glyphText = new TextBlock
            {
                FontFamily = ThemeTokens.MonoFont,
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(2, 2, 2, 2),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };

            _glassOverlay = ThemeTokens.BuildIos26GlassOverlay(11, false);
            var innerGrid = new Grid();
            innerGrid.Children.Add(_glassOverlay);
            innerGrid.Children.Add(_glyphText);

            Child = innerGrid;
            ScheduleNextBlink();
            ScheduleNextMicroEmote();

            RefreshFromSettings(ThemeTokens.GetPalette(_settings.Current.Theme, _settings.Current.Opacity));

            // Interactive hover & click reactions on the companion badge itself
            MouseEnter += OnBadgeMouseEnter;
            MouseLeave += OnBadgeMouseLeave;
            MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                AnimateTransform(0, 0.5, 0.92, 0.92, 0, 70);
            };
            MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                React(SmileyReactionType.UserClicked);
            };

            // Fast natural life-cycle timer (checks every 750ms for blinks & idle micro-expressions)
            _lifeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(750) };
            _lifeTimer.Tick += OnLifeTick;
            _lifeTimer.Start();

            // Calm, relaxed music groove cadence (1200ms) when music is playing
            _musicGrooveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
            _musicGrooveTimer.Tick += OnMusicGrooveTick;
        }

        private void ScheduleNextBlink()
        {
            _nextBlinkUtc = DateTime.UtcNow.AddMilliseconds(_rng.Next(2600, 5200));
        }

        private void ScheduleNextMicroEmote()
        {
            _nextMicroEmoteUtc = DateTime.UtcNow.AddMilliseconds(_rng.Next(4200, 7800));
        }

        public void SetMood(SmileyMood mood)
        {
            bool changed = _currentMood != mood;
            _currentMood = mood;

            if (mood == SmileyMood.MusicPlaying)
            {
                if (!_musicGrooveTimer.IsEnabled)
                {
                    _musicBeatStep = 0;
                    _musicGrooveTimer.Start();
                }
            }
            else
            {
                if (_musicGrooveTimer.IsEnabled)
                {
                    _musicGrooveTimer.Stop();
                }
            }

            if (DateTime.UtcNow >= _reactionLockUntilUtc || mood == SmileyMood.MusicPlaying || mood == SmileyMood.NotificationAlert)
            {
                UpdateGlyphText();
            }

            if (_lastPalette != null)
            {
                ApplyColors(_lastPalette);
            }

            if (!_settings.Current.EnableSmileyAnimation || !changed) return;

            switch (mood)
            {
                case SmileyMood.MusicPlaying:
                    AnimateTransform(0, -2.0, 1.08, 1.08, -4.0, 160, autoReverse: true);
                    break;
                case SmileyMood.NotificationAlert:
                    AnimateTransform(0, -1.8, 1.10, 1.10, 5.0, 150, autoReverse: true);
                    break;
                case SmileyMood.TaskCompleted:
                    AnimateTransform(0, -2.2, 1.08, 1.08, 0, 170, autoReverse: true);
                    break;
                default:
                    AnimateTransform(0, 0, 1.0, 1.0, 0, 130);
                    break;
            }
        }

        public async void React(SmileyReactionType reaction, int contextIndex = 0)
        {
            if (!_settings.Current.EnableSmileyAnimation) return;

            var style = _settings.Current.SmileyStyle;
            string face = GetNormalGlyph(style);
            double tx = 0, ty = -2.0, sx = 1.08, sy = 1.08, angle = 0;
            int holdMs = 780;

            switch (reaction)
            {
                case SmileyReactionType.WakeUp:
                    face = GetWakeUpGlyph(style);
                    ty = -2.2;
                    sx = 1.06;
                    sy = 1.10;
                    holdMs = 680;
                    break;

                case SmileyReactionType.Expand:
                    face = GetExcitedGlyph(style);
                    ty = -2.4;
                    sx = 1.10;
                    sy = 1.10;
                    angle = -4.0;
                    holdMs = 700;
                    break;

                case SmileyReactionType.Collapse:
                    face = GetCozyGlyph(style);
                    ty = 0.8;
                    sx = 1.04;
                    sy = 0.94;
                    holdMs = 620;
                    break;

                case SmileyReactionType.TabChanged:
                    face = GetTabGlyph(style, contextIndex);
                    ty = -1.8;
                    sx = 1.07;
                    sy = 1.07;
                    angle = (contextIndex % 2 == 0) ? -4.5 : 4.5;
                    holdMs = 750;
                    break;

                case SmileyReactionType.ClipboardCopied:
                    face = GetClipboardGlyph(style);
                    ty = -2.4;
                    sx = 1.10;
                    sy = 1.10;
                    angle = 5.0;
                    holdMs = 920;
                    break;

                case SmileyReactionType.FileDropped:
                    face = GetFileDroppedGlyph(style);
                    ty = -2.8;
                    sx = 1.12;
                    sy = 1.12;
                    angle = -6.0;
                    holdMs = 1050;
                    break;

                case SmileyReactionType.NoteTyped:
                    face = GetNoteTypedGlyph(style);
                    tx = 1.4;
                    ty = -1.0;
                    sx = 1.04;
                    sy = 1.04;
                    angle = 3.5;
                    holdMs = 680;
                    break;

                case SmileyReactionType.VolumeChanged:
                    face = GetVolumeGlyph(style, contextIndex);
                    ty = -1.8;
                    sx = 1.08;
                    sy = 1.08;
                    holdMs = 680;
                    break;

                case SmileyReactionType.VolumeMuted:
                    face = GetMutedGlyph(style);
                    ty = 0.5;
                    sx = 0.96;
                    sy = 0.96;
                    angle = -5.0;
                    holdMs = 950;
                    break;

                case SmileyReactionType.LayoutSwitched:
                    face = GetWinkGlyph(style);
                    tx = 1.6;
                    ty = -1.5;
                    sx = 1.07;
                    sy = 1.07;
                    angle = 5.5;
                    holdMs = 680;
                    break;

                case SmileyReactionType.UserClicked:
                    _clickComboIndex = (_clickComboIndex + 1) % 5;
                    face = GetClickEasterEggGlyph(style, _clickComboIndex);
                    ty = -3.0;
                    sx = 1.15;
                    sy = 1.15;
                    angle = (_clickComboIndex % 2 == 0) ? 10.0 : -10.0;
                    holdMs = 1150;
                    break;
            }

            _reactionLockUntilUtc = DateTime.UtcNow.AddMilliseconds(holdMs);
            _glyphText.Text = face;
            AnimateTransform(tx, ty, sx, sy, angle, 150, autoReverse: true);

            await Task.Delay(holdMs);
            if (DateTime.UtcNow >= _reactionLockUntilUtc && !_isHovered)
            {
                UpdateGlyphText();
                AnimateTransform(0, 0, 1.0, 1.0, 0, 140);
            }
        }

        private void OnBadgeMouseEnter(object sender, MouseEventArgs e)
        {
            _isHovered = true;
            if (!_settings.Current.EnableSmileyAnimation) return;

            var style = _settings.Current.SmileyStyle;
            _glyphText.Text = _currentMood == SmileyMood.MusicPlaying
                ? GetMusicBeatGlyph(style, _musicBeatStep)
                : GetHoverLoveGlyph(style);
            AnimateTransform(0, -1.5, 1.10, 1.10, -4.0, 140);
        }

        private void OnBadgeMouseLeave(object sender, MouseEventArgs e)
        {
            _isHovered = false;
            UpdateGlyphText();
            AnimateTransform(0, 0, 1.0, 1.0, 0, 150);
        }

        private void OnMusicGrooveTick(object sender, EventArgs e)
        {
            if (_currentMood != SmileyMood.MusicPlaying)
            {
                _musicGrooveTimer.Stop();
                return;
            }

            if (DateTime.UtcNow < _reactionLockUntilUtc) return;

            _musicBeatStep = (_musicBeatStep + 1) % 4;
            var style = _settings.Current.SmileyStyle;

            // Occasional calm relaxed blink
            bool blinkOnBeat = _rng.Next(0, 5) == 0;
            _glyphText.Text = blinkOnBeat ? GetMusicBlinkGlyph(style) : GetMusicBeatGlyph(style, _musicBeatStep);

            if (_settings.Current.EnableSmileyAnimation)
            {
                // Calm, subtle vertical breathing nod (no intense jumping or rapid tilting)
                double bobY = (_musicBeatStep % 2 == 0) ? -0.4 : 0.2;
                double tilt = (_musicBeatStep % 2 == 0) ? -0.6 : 0.6;
                double scale = (_musicBeatStep % 2 == 0) ? 1.01 : 1.0;
                AnimateTransform(0.0, bobY, scale, scale, tilt, 420);
            }
        }

        private async void OnLifeTick(object sender, EventArgs e)
        {
            if (!_settings.Current.EnableSmileyAnimation || _isHovered || DateTime.UtcNow < _reactionLockUntilUtc)
                return;

            if (_currentMood != SmileyMood.Normal && _currentMood != SmileyMood.TimerActive)
                return;

            DateTime now = DateTime.UtcNow;
            var style = _settings.Current.SmileyStyle;

            // 1. Natural Blink or Double-Blink (every 2.6..5.2 seconds)
            if (now >= _nextBlinkUtc)
            {
                ScheduleNextBlink();
                bool doubleBlink = _rng.Next(0, 100) < 35;

                _glyphText.Text = GetBlinkGlyph(style);
                AnimateTransform(0, 0.4, 1.03, 0.86, 0, 75);
                await Task.Delay(120);

                if (_currentMood == SmileyMood.Normal || _currentMood == SmileyMood.TimerActive)
                {
                    UpdateGlyphText();
                    AnimateTransform(0, 0, 1.0, 1.0, 0, 85);
                }

                if (doubleBlink && DateTime.UtcNow >= _reactionLockUntilUtc && !_isHovered)
                {
                    await Task.Delay(95);
                    _glyphText.Text = GetBlinkGlyph(style);
                    AnimateTransform(0, 0.4, 1.03, 0.86, 0, 70);
                    await Task.Delay(110);
                    if (_currentMood == SmileyMood.Normal || _currentMood == SmileyMood.TimerActive)
                    {
                        UpdateGlyphText();
                        AnimateTransform(0, 0, 1.0, 1.0, 0, 85);
                    }
                }
                return;
            }

            // 2. Expressive Idle Micro-Emote (every 4.2..7.8 seconds: glance left/right, wink, joy, curious, hum, cozy)
            if (now >= _nextMicroEmoteUtc && _currentMood == SmileyMood.Normal)
            {
                ScheduleNextMicroEmote();
                int action = _rng.Next(0, 7);
                string emoteFace = GetNormalGlyph(style);
                double tx = 0, ty = 0, sx = 1.0, sy = 1.0, angle = 0;
                int durationMs = 760;

                switch (action)
                {
                    case 0: // Glance Left
                        emoteFace = GetLookLeftGlyph(style);
                        tx = -2.2;
                        angle = -4.0;
                        durationMs = 720;
                        break;
                    case 1: // Glance Right
                        emoteFace = GetLookRightGlyph(style);
                        tx = 2.2;
                        angle = 4.0;
                        durationMs = 720;
                        break;
                    case 2: // Playful Wink
                        emoteFace = GetWinkGlyph(style);
                        ty = -1.2;
                        sx = 1.06;
                        sy = 1.06;
                        angle = 5.5;
                        durationMs = 780;
                        break;
                    case 3: // Happy Squint Bounce
                        emoteFace = GetHappySquintGlyph(style);
                        ty = -2.0;
                        sx = 1.06;
                        sy = 0.96;
                        durationMs = 820;
                        break;
                    case 4: // Curious Perk Up
                        emoteFace = GetCuriousGlyph(style);
                        ty = -1.6;
                        sx = 1.04;
                        sy = 1.08;
                        angle = -3.0;
                        durationMs = 750;
                        break;
                    case 5: // Gentle Hum / Whistle
                        emoteFace = GetWhistleGlyph(style);
                        tx = 1.2;
                        ty = -1.2;
                        angle = 4.0;
                        durationMs = 860;
                        break;
                    default: // Cozy Content Smile
                        emoteFace = GetCozyGlyph(style);
                        ty = 0.5;
                        sx = 1.03;
                        sy = 0.97;
                        durationMs = 840;
                        break;
                }

                _glyphText.Text = emoteFace;
                AnimateTransform(tx, ty, sx, sy, angle, 160);
                await Task.Delay(durationMs);

                if (_currentMood == SmileyMood.Normal && DateTime.UtcNow >= _reactionLockUntilUtc && !_isHovered)
                {
                    UpdateGlyphText();
                    AnimateTransform(0, 0, 1.0, 1.0, 0, 160);
                }
            }
        }

        public void RefreshFromSettings(ThemePalette palette)
        {
            _lastPalette = palette ?? ThemeTokens.GetPalette(_settings.Current.Theme, _settings.Current.Opacity);
            Visibility = _settings.Current.ShowSmileyInCollapsed ? Visibility.Visible : Visibility.Collapsed;
            UpdateGlyphText();
            ApplyColors(_lastPalette);
        }

        private void ApplyColors(ThemePalette palette)
        {
            Background = palette.RaisedBackground;
            BorderBrush = palette.HairlineBorder;
            if (_glassOverlay != null)
            {
                _glassOverlay.Visibility = palette.IsGlass ? Visibility.Visible : Visibility.Collapsed;
            }

            if (_currentMood == SmileyMood.TaskCompleted)
            {
                _glyphText.Foreground = palette.Positive;
            }
            else if (_currentMood == SmileyMood.NotificationAlert)
            {
                _glyphText.Foreground = palette.Warning;
            }
            else
            {
                _glyphText.Foreground = palette.PrimaryText;
            }
        }

        private void UpdateGlyphText()
        {
            var style = _settings.Current.SmileyStyle;
            switch (_currentMood)
            {
                case SmileyMood.MusicPlaying:
                    _glyphText.Text = GetMusicBeatGlyph(style, _musicBeatStep);
                    break;
                case SmileyMood.NotificationAlert:
                    _glyphText.Text = GetAlertGlyph(style);
                    break;
                case SmileyMood.TaskCompleted:
                    _glyphText.Text = GetCompletedGlyph(style);
                    break;
                case SmileyMood.IdleBlink:
                    _glyphText.Text = GetBlinkGlyph(style);
                    break;
                case SmileyMood.TimerActive:
                    _glyphText.Text = GetTimerGlyph(style);
                    break;
                default:
                    _glyphText.Text = GetNormalGlyph(style);
                    break;
            }
        }

        // ==================== EXPRESSIVE TYPOGRAPHIC VOCABULARY ACROSS ALL 5 TEXT STYLES ====================
        private static string GetNormalGlyph(SmileyStylePreset style)
        {
            switch (style)
            {
                case SmileyStylePreset.Minimal: return "•ᴗ•";
                case SmileyStylePreset.Cyber:   return "[•_•]";
                case SmileyStylePreset.Cat:     return "=^..^=";
                case SmileyStylePreset.Kawaii:  return "◕‿◕";
                default:                        return "^_^";
            }
        }

        private static string GetBlinkGlyph(SmileyStylePreset style)
        {
            switch (style)
            {
                case SmileyStylePreset.Minimal: return "-ᴗ-";
                case SmileyStylePreset.Cyber:   return "[-_-]";
                case SmileyStylePreset.Cat:     return "=-..-=";
                case SmileyStylePreset.Kawaii:  return "˘‿˘";
                default:                        return "-_-";
            }
        }

        private static string GetLookLeftGlyph(SmileyStylePreset style)
        {
            switch (style)
            {
                case SmileyStylePreset.Minimal: return "‹ᴗ‹";
                case SmileyStylePreset.Cyber:   return "[<_<]";
                case SmileyStylePreset.Cat:     return "=‹..‹=";
                case SmileyStylePreset.Kawaii:  return "◔‿◔";
                default:                        return "<_<";
            }
        }

        private static string GetLookRightGlyph(SmileyStylePreset style)
        {
            switch (style)
            {
                case SmileyStylePreset.Minimal: return "›ᴗ›";
                case SmileyStylePreset.Cyber:   return "[>_>]";
                case SmileyStylePreset.Cat:     return "=›..›=";
                case SmileyStylePreset.Kawaii:  return "◔◡◔";
                default:                        return ">_>";
            }
        }

        private static string GetWinkGlyph(SmileyStylePreset style)
        {
            switch (style)
            {
                case SmileyStylePreset.Minimal: return "•ᴗ~";
                case SmileyStylePreset.Cyber:   return "[•_~]";
                case SmileyStylePreset.Cat:     return "=^..~=";
                case SmileyStylePreset.Kawaii:  return "◕‿↼";
                default:                        return "^_~";
            }
        }

        private static string GetHappySquintGlyph(SmileyStylePreset style)
        {
            switch (style)
            {
                case SmileyStylePreset.Minimal: return "˘◡˘";
                case SmileyStylePreset.Cyber:   return "[^v^]";
                case SmileyStylePreset.Cat:     return "=^v^=";
                case SmileyStylePreset.Kawaii:  return "≧◡≦";
                default:                        return ">v<";
            }
        }

        private static string GetCuriousGlyph(SmileyStylePreset style)
        {
            switch (style)
            {
                case SmileyStylePreset.Minimal: return "°o°";
                case SmileyStylePreset.Cyber:   return "[0_0]";
                case SmileyStylePreset.Cat:     return "=°..°=";
                case SmileyStylePreset.Kawaii:  return "⊙_⊙";
                default:                        return "o_o";
            }
        }

        private static string GetWhistleGlyph(SmileyStylePreset style)
        {
            switch (style)
            {
                case SmileyStylePreset.Minimal: return "•o•♪";
                case SmileyStylePreset.Cyber:   return "[•o•]♪";
                case SmileyStylePreset.Cat:     return "=^o^=♪";
                case SmileyStylePreset.Kawaii:  return "◕o◕♪";
                default:                        return "^o^♪";
            }
        }

        private static string GetCozyGlyph(SmileyStylePreset style)
        {
            switch (style)
            {
                case SmileyStylePreset.Minimal: return "˘ᴗ˘";
                case SmileyStylePreset.Cyber:   return "[^_^]";
                case SmileyStylePreset.Cat:     return "=˘..˘=";
                case SmileyStylePreset.Kawaii:  return "˘‿˘";
                default:                        return "˘‿˘";
            }
        }

        private static string GetWakeUpGlyph(SmileyStylePreset style)
        {
            switch (style)
            {
                case SmileyStylePreset.Minimal: return "°ᴗ°";
                case SmileyStylePreset.Cyber:   return "[O_•]";
                case SmileyStylePreset.Cat:     return "=°v°=";
                case SmileyStylePreset.Kawaii:  return "◕ヮ◕";
                default:                        return "o_^";
            }
        }

        private static string GetExcitedGlyph(SmileyStylePreset style)
        {
            switch (style)
            {
                case SmileyStylePreset.Minimal: return "✧•◡•";
                case SmileyStylePreset.Cyber:   return "[★_★]";
                case SmileyStylePreset.Cat:     return "=^ω^=";
                case SmileyStylePreset.Kawaii:  return "◕ヮ◕";
                default:                        return "^o^";
            }
        }

        private static string GetHoverLoveGlyph(SmileyStylePreset style)
        {
            switch (style)
            {
                case SmileyStylePreset.Minimal: return "♥ᴗ♥";
                case SmileyStylePreset.Cyber:   return "[♥_♥]";
                case SmileyStylePreset.Cat:     return "=^ω^=♥";
                case SmileyStylePreset.Kawaii:  return "◕‿◕♥";
                default:                        return "^v^";
            }
        }

        private static string GetMusicBeatGlyph(SmileyStylePreset style, int step)
        {
            switch (style)
            {
                case SmileyStylePreset.Minimal:
                    return (step % 2 == 0) ? "♪ •ᴗ•" : "♪ ˘◡˘";
                case SmileyStylePreset.Cyber:
                    return (step % 2 == 0) ? "♪[•_•]" : "♪[^_^]";
                case SmileyStylePreset.Cat:
                    return (step % 2 == 0) ? "♪=^..^=" : "♪=^v^=";
                case SmileyStylePreset.Kawaii:
                    return (step % 2 == 0) ? "♪ ◕‿◕" : "♪ ˘‿˘";
                default:
                    return (step % 2 == 0) ? "♪ ^_^" : "♪ ˘◡˘";
            }
        }

        private static string GetMusicBlinkGlyph(SmileyStylePreset style)
        {
            switch (style)
            {
                case SmileyStylePreset.Minimal: return "♪ -ᴗ-";
                case SmileyStylePreset.Cyber:   return "♪[-_-]";
                case SmileyStylePreset.Cat:     return "♪=-..-=";
                case SmileyStylePreset.Kawaii:  return "♪ ˘‿˘";
                default:                        return "♪ -_-";
            }
        }

        private static string GetAlertGlyph(SmileyStylePreset style)
        {
            switch (style)
            {
                case SmileyStylePreset.Minimal: return "°o°!";
                case SmileyStylePreset.Cyber:   return "[O_O]";
                case SmileyStylePreset.Cat:     return "=O..O=";
                case SmileyStylePreset.Kawaii:  return "⊙o⊙!";
                default:                        return "O_o";
            }
        }

        private static string GetCompletedGlyph(SmileyStylePreset style)
        {
            switch (style)
            {
                case SmileyStylePreset.Minimal: return "•◡• ✓";
                case SmileyStylePreset.Cyber:   return "[^_^]✓";
                case SmileyStylePreset.Cat:     return "=^v^=✓";
                case SmileyStylePreset.Kawaii:  return "≧◡≦ ✓";
                default:                        return "^v^ ✓";
            }
        }

        private static string GetTimerGlyph(SmileyStylePreset style)
        {
            switch (style)
            {
                case SmileyStylePreset.Minimal: return "⊙ᴗ⊙";
                case SmileyStylePreset.Cyber:   return "[⊙_⊙]";
                case SmileyStylePreset.Cat:     return "=⊙..⊙=";
                case SmileyStylePreset.Kawaii:  return "⊙‿⊙";
                default:                        return "⊙_⊙";
            }
        }

        private static string GetTabGlyph(SmileyStylePreset style, int tabIndex)
        {
            switch (tabIndex)
            {
                case 0: return GetHappySquintGlyph(style);
                case 1: return GetWinkGlyph(style);
                case 2: return GetExcitedGlyph(style);
                case 3: return GetNoteTypedGlyph(style);
                case 4: return GetWhistleGlyph(style);
                default: return GetCuriousGlyph(style);
            }
        }

        private static string GetClipboardGlyph(SmileyStylePreset style)
        {
            switch (style)
            {
                case SmileyStylePreset.Minimal: return "•ᴗ~ ✓";
                case SmileyStylePreset.Cyber:   return "[•_~]✓";
                case SmileyStylePreset.Cat:     return "=^v^=✓";
                case SmileyStylePreset.Kawaii:  return "◕‿↼ ✓";
                default:                        return "^_~ ✓";
            }
        }

        private static string GetFileDroppedGlyph(SmileyStylePreset style)
        {
            switch (style)
            {
                case SmileyStylePreset.Minimal: return "✧•o•✧";
                case SmileyStylePreset.Cyber:   return "[★v★]";
                case SmileyStylePreset.Cat:     return "=★ω★=";
                case SmileyStylePreset.Kawaii:  return "★‿★";
                default:                        return "\\^o^/";
            }
        }

        private static string GetNoteTypedGlyph(SmileyStylePreset style)
        {
            switch (style)
            {
                case SmileyStylePreset.Minimal: return "•ᴗ•✎";
                case SmileyStylePreset.Cyber:   return "[•_•]✎";
                case SmileyStylePreset.Cat:     return "=•..•=✎";
                case SmileyStylePreset.Kawaii:  return "◕‿◕✎";
                default:                        return "^_^✎";
            }
        }

        private static string GetVolumeGlyph(SmileyStylePreset style, int volumePct)
        {
            return volumePct >= 75 ? GetExcitedGlyph(style) : GetHappySquintGlyph(style);
        }

        private static string GetMutedGlyph(SmileyStylePreset style)
        {
            switch (style)
            {
                case SmileyStylePreset.Minimal: return "•x•";
                case SmileyStylePreset.Cyber:   return "[X_X]";
                case SmileyStylePreset.Cat:     return "=x..x=";
                case SmileyStylePreset.Kawaii:  return "◕x◕";
                default:                        return "×_×";
            }
        }

        private static string GetClickEasterEggGlyph(SmileyStylePreset style, int combo)
        {
            switch (combo % 5)
            {
                case 0: return "(⌐■_■)";
                case 1: return "♥‿♥";
                case 2: return "★‿★";
                case 3: return "\\^o^/";
                default: return "@_@";
            }
        }

        private void AnimateTransform(double tx, double ty, double sx, double sy, double angle, int durationMs, bool autoReverse = false)
        {
            var dur = TimeSpan.FromMilliseconds(durationMs);
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

            _translate.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(tx, dur) { AutoReverse = autoReverse, EasingFunction = ease });
            _translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(ty, dur) { AutoReverse = autoReverse, EasingFunction = ease });
            _scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(sx, dur) { AutoReverse = autoReverse, EasingFunction = ease });
            _scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(sy, dur) { AutoReverse = autoReverse, EasingFunction = ease });
            _rotate.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(angle, dur) { AutoReverse = autoReverse, EasingFunction = ease });
        }
    }
}
