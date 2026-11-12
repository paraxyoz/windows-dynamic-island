using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WindowsDynamicIsland.Models;
using WindowsDynamicIsland.Services;

namespace WindowsDynamicIsland.UI
{
    public sealed class ThemePalette
    {
        public bool IsLight { get; set; }
        public bool IsGlass { get; set; }
        public Brush CapsuleBackground { get; set; }
        public Brush PanelBackground { get; set; }
        public Brush CardBackground { get; set; }
        public Brush RaisedBackground { get; set; }
        public Brush HoverBackground { get; set; }
        public Brush HairlineBorder { get; set; }
        public Brush SubtleBorder { get; set; }
        public Color ShadowColor { get; set; }
        public double ShadowOpacity { get; set; }
        public SolidColorBrush PrimaryText { get; set; }
        public SolidColorBrush SecondaryText { get; set; }
        public SolidColorBrush MutedText { get; set; }
        public Brush Accent { get; set; }
        public Brush AccentSubtle { get; set; }
        public SolidColorBrush OnAccentText { get; set; }
        public SolidColorBrush Positive { get; set; }
        public SolidColorBrush Warning { get; set; }
        public SolidColorBrush Destructive { get; set; }
        public SolidColorBrush DestructiveSubtle { get; set; }
    }

    public static class ThemeTokens
    {
        public static readonly FontFamily UiFont = new FontFamily("Segoe UI Variable Text, Segoe UI Variable Display, Segoe UI");
        public static readonly FontFamily MonoFont = new FontFamily("Cascadia Mono, Consolas");

        public static ThemePalette GetPalette(AppThemeMode mode, double opacity = 0.98)
        {
            if (mode == AppThemeMode.Ios26Glass)
            {
                byte tint = (byte)Math.Round(196 + Math.Max(0.0, Math.Min(1.0, opacity)) * 31);
                return new ThemePalette
                {
                    IsGlass = true,
                    CapsuleBackground = MakeLinearGradient(
                        new Point(0, 0), new Point(0.35, 1),
                        (0.0, Color.FromArgb(tint, 45, 49, 58)),
                        (0.54, Color.FromArgb(tint, 23, 26, 34)),
                        (1.0, Color.FromArgb(tint, 32, 35, 45))),
                    PanelBackground = MakeLinearGradient(
                        new Point(0, 0), new Point(0.3, 1),
                        (0.0, Color.FromArgb(tint, 42, 46, 55)),
                        (0.38, Color.FromArgb(tint, 26, 29, 37)),
                        (1.0, Color.FromArgb(tint, 30, 33, 42))),
                    CardBackground = MakeBrush(21, 244, 248, 255),
                    RaisedBackground = MakeBrush(27, 244, 248, 255),
                    HoverBackground = MakeBrush(53, 244, 248, 255),
                    HairlineBorder = MakeBrush(22, 255, 255, 255),
                    SubtleBorder = MakeLinearGradient(
                        new Point(0, 0), new Point(0.35, 1),
                        (0.0, Color.FromArgb(104, 255, 255, 255)),
                        (0.37, Color.FromArgb(37, 255, 255, 255)),
                        (1.0, Color.FromArgb(55, 227, 235, 253))),
                    ShadowColor = Color.FromRgb(0, 0, 0),
                    ShadowOpacity = 0.28,
                    PrimaryText = MakeBrush(255, 249, 250, 255),
                    SecondaryText = MakeBrush(255, 210, 216, 229),
                    MutedText = MakeBrush(255, 181, 189, 208),
                    Accent = MakeBrush(255, 210, 229, 255),
                    AccentSubtle = MakeBrush(47, 176, 209, 255),
                    OnAccentText = MakeBrush(255, 24, 38, 58),
                    Positive = MakeBrush(255, 140, 236, 180),
                    Warning = MakeBrush(255, 255, 203, 132),
                    Destructive = MakeBrush(255, 255, 136, 142),
                    DestructiveSubtle = MakeBrush(95, 220, 60, 70)
                };
            }

            bool useLight = mode == AppThemeMode.Light ||
                            (mode == AppThemeMode.System && SettingsManager.IsWindowsInLightTheme());

            byte alpha = (byte)Math.Round(Math.Max(0.65, Math.Min(1.0, opacity)) * 255);

            if (useLight)
            {
                // PREMIUM LIGHT / SOFT GRAY (Default Apple / macOS Hardware Aesthetic)
                // Main Bg: #F5F5F7, Surface: #FFFFFF, Secondary Surface: #EEEEF1,
                // Primary Text: #202124, Secondary Text: #63636B, Muted: #85858D, Border: #E5E5E9, Accent: #2B2B30
                return new ThemePalette
                {
                    IsLight = true,
                    CapsuleBackground = MakeBrush(alpha, 245, 245, 247),   // #F5F5F7
                    PanelBackground = MakeBrush(alpha, 245, 245, 247),     // #F5F5F7
                    CardBackground = MakeBrush(255, 255, 255, 255),        // #FFFFFF
                    RaisedBackground = MakeBrush(255, 238, 238, 241),      // #EEEEF1
                    HoverBackground = MakeBrush(255, 228, 228, 233),       // #E4E4E9
                    HairlineBorder = MakeBrush(255, 229, 229, 233),        // #E5E5E9
                    SubtleBorder = MakeBrush(255, 212, 212, 218),          // #D4D4DA
                    ShadowColor = Color.FromRgb(24, 24, 30),
                    ShadowOpacity = 0.16,
                    PrimaryText = MakeBrush(255, 32, 33, 36),              // #202124
                    SecondaryText = MakeBrush(255, 95, 96, 102),           // #5F6066
                    MutedText = MakeBrush(255, 133, 133, 141),             // #85858D
                    Accent = MakeBrush(255, 43, 43, 48),                   // #2B2B30 (Graphite Charcoal)
                    AccentSubtle = MakeBrush(255, 230, 230, 235),          // #E6E6EB
                    OnAccentText = MakeBrush(255, 255, 255, 255),          // #FFFFFF
                    Positive = MakeBrush(255, 46, 125, 82),                // #2E7D52
                    Warning = MakeBrush(255, 180, 105, 30),                // #B4691E
                    Destructive = MakeBrush(255, 197, 57, 57),             // #C53939
                    DestructiveSubtle = MakeBrush(255, 251, 236, 236)      // #FBECEC
                };
            }

            // PURE BLACK DARK THEME (True #000000 OLED Black + Soft Non-Accent Matte White)
            return new ThemePalette
            {
                IsLight = false,
                CapsuleBackground = MakeBrush(255, 0, 0, 0),                // #000000 Pure Pitch Black
                PanelBackground = MakeBrush(alpha, 0, 0, 0),                // #000000 Pure Pitch Black
                CardBackground = MakeBrush(255, 8, 8, 10),                  // #08080A Deep Obsidian Card
                RaisedBackground = MakeBrush(255, 16, 16, 20),              // #101014 Subtle Control Surface
                HoverBackground = MakeBrush(255, 25, 25, 31),               // #19191F Soft Dark Hover
                HairlineBorder = MakeBrush(255, 26, 26, 32),                // #1A1A20 Crisp Dark Hairline
                SubtleBorder = MakeBrush(255, 52, 52, 62),                  // #34343E Active Pill Border
                ShadowColor = Color.FromRgb(0, 0, 0),
                ShadowOpacity = 0.65,
                PrimaryText = MakeBrush(255, 232, 232, 237),                // #E8E8ED Soft Non-Accent White
                SecondaryText = MakeBrush(255, 152, 152, 162),              // #9898A2 Calm Secondary
                MutedText = MakeBrush(255, 100, 100, 110),                  // #64646E Muted Hint
                Accent = MakeBrush(255, 30, 30, 38),                        // #1E1E26 Non-glare Dark Active Surface
                AccentSubtle = MakeBrush(255, 22, 22, 28),                  // #16161C Subtle Selection Fill
                OnAccentText = MakeBrush(255, 238, 238, 243),               // #EEEEF3 Soft Matte White on Active
                Positive = MakeBrush(255, 78, 159, 114),                    // #4E9F72
                Warning = MakeBrush(255, 212, 138, 59),                     // #D48A3B
                Destructive = MakeBrush(255, 217, 83, 83),                  // #D95353
                DestructiveSubtle = MakeBrush(255, 44, 20, 22)              // #2C1416
            };
        }

        public static SolidColorBrush MakeBrush(byte a, byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
            brush.Freeze();
            return brush;
        }

        public static LinearGradientBrush MakeLinearGradient(Point start, Point end, params (double offset, Color color)[] stops)
        {
            var lgb = new LinearGradientBrush
            {
                StartPoint = start,
                EndPoint = end
            };
            if (stops != null)
            {
                for (int i = 0; i < stops.Length; i++)
                {
                    lgb.GradientStops.Add(new GradientStop(stops[i].color, stops[i].offset));
                }
            }
            lgb.Freeze();
            return lgb;
        }

        public static UIElement BuildIos26GlassOverlay(double cornerRadius, bool isActive = false)
        {
            return new Border
            {
                IsHitTestVisible = false,
                CornerRadius = new CornerRadius(cornerRadius),
                Background = MakeLinearGradient(
                    new Point(0, 0), new Point(0, 1),
                    (0.0, Color.FromArgb((byte)(isActive ? 54 : 24), 255, 255, 255)),
                    (0.46, Color.FromArgb(0, 255, 255, 255)),
                    (1.0, Color.FromArgb(17, 0, 0, 0)))
            };
        }

        public static UIElement WrapContentWithGlassLayers(
            ThemePalette p,
            UIElement content,
            double cornerRadius,
            bool isActive = false,
            Thickness? contentPadding = null)
        {
            if (p == null || !p.IsGlass || content == null)
            {
                if (contentPadding.HasValue && content != null)
                {
                    return new Border
                    {
                        Padding = contentPadding.Value,
                        Child = content
                    };
                }
                return content;
            }

            var grid = new Grid();
            grid.Children.Add(BuildIos26GlassOverlay(cornerRadius, isActive));
            if (contentPadding.HasValue)
            {
                grid.Children.Add(new Border
                {
                    Padding = contentPadding.Value,
                    Child = content
                });
            }
            else
            {
                grid.Children.Add(content);
            }
            return grid;
        }

        public static Border CreateCard(ThemePalette p, double cornerRadius = 10, Thickness? padding = null)
        {
            var card = new Border
            {
                Background = p.CardBackground,
                BorderBrush = p.HairlineBorder,
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(cornerRadius),
                Padding = padding ?? new Thickness(12),
                SnapsToDevicePixels = true,
                UseLayoutRounding = true
            };
            return card;
        }

        public static Border CreateKeycap(ThemePalette p, string text, bool isHighlighted = false)
        {
            var label = new TextBlock
            {
                Text = text,
                FontFamily = MonoFont,
                FontSize = 10.5,
                FontWeight = FontWeights.Medium,
                Foreground = isHighlighted ? p.PrimaryText : p.MutedText,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };

            return new Border
            {
                Background = isHighlighted ? p.AccentSubtle : p.RaisedBackground,
                BorderBrush = isHighlighted && p.IsGlass ? p.SubtleBorder : p.HairlineBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(0),
                SnapsToDevicePixels = true,
                Child = WrapContentWithGlassLayers(p, label, 5, isHighlighted, new Thickness(6, 2, 6, 2))
            };
        }

        public static Border CreateInteractiveButton(
            ThemePalette p, UIElement content, Action onClick, bool isPrimary = false,
            bool isDestructive = false, double cornerRadius = 7, Thickness? padding = null,
            string tooltip = null)
        {
            var button = new Button
            {
                Content = content,
                Background = isDestructive ? p.DestructiveSubtle : isPrimary ? p.Accent : p.RaisedBackground,
                Foreground = isPrimary ? p.OnAccentText : p.PrimaryText,
                HorizontalContentAlignment = content is Grid ? HorizontalAlignment.Stretch : HorizontalAlignment.Center,
                Padding = padding ?? new Thickness(10, 5, 10, 5),
                ToolTip = tooltip
            };
            button.SetResourceReference(FrameworkElement.StyleProperty, "IslandPlayerButton");
            if (!string.IsNullOrWhiteSpace(tooltip))
                System.Windows.Automation.AutomationProperties.SetName(button, tooltip);
            button.Click += (s, e) => { if (e.Source == button) onClick?.Invoke(); };
            var host = new Border { CornerRadius = new CornerRadius(9), Child = button };
            host.Background = button.Background;
            button.SetBinding(Control.BackgroundProperty, new System.Windows.Data.Binding(nameof(Border.Background)) { Source = host });
            // The button paints the rounded surface; the host also exposes the color to existing callers.
            return host;
        }

        public static UIElement CreateEmptyState(ThemePalette palette, string icon, string title, string hint, UIElement action = null)
        {
            var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var emblem = new Border
            {
                Width = 48, Height = 48, CornerRadius = new CornerRadius(15),
                Background = palette.AccentSubtle, HorizontalAlignment = HorizontalAlignment.Center,
                Child = VectorIcons.Create(icon, palette.PrimaryText, 21)
            };
            stack.Children.Add(emblem);
            stack.Children.Add(new TextBlock
            {
                Text = title, FontFamily = UiFont, FontSize = 18, FontWeight = FontWeights.SemiBold,
                Foreground = palette.PrimaryText, HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 9, 0, 4)
            });
            stack.Children.Add(new TextBlock
            {
                Text = hint, Foreground = palette.SecondaryText, FontSize = 12, MaxWidth = 330,
                TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap
            });
            if (action is FrameworkElement button)
            {
                button.HorizontalAlignment = HorizontalAlignment.Center;
                button.Margin = new Thickness(0, 12, 0, 0);
                stack.Children.Add(button);
            }
            return stack;
        }

        public static void Reveal(FrameworkElement element, double distance = 7, int milliseconds = 220)
        {
            if (!SystemParameters.ClientAreaAnimation) return;
            var offset = new TranslateTransform();
            element.RenderTransform = offset;
            var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
            var duration = TimeSpan.FromMilliseconds(milliseconds);
            offset.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(distance, 0, duration)
                { EasingFunction = easing, FillBehavior = FillBehavior.Stop });
            element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.35, 1, duration)
                { EasingFunction = easing, FillBehavior = FillBehavior.Stop });
        }

        public static TextBox CreateStyledTextBox(ThemePalette p, string placeholder, double fontSize = 12.5, bool multiline = false)
        {
            var tb = new TextBox
            {
                FontFamily = UiFont,
                FontSize = fontSize,
                Foreground = p.PrimaryText,
                CaretBrush = p.PrimaryText,
                SelectionBrush = MakeBrush(70, 120, 120, 132),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                Margin = new Thickness(0),
                AcceptsReturn = multiline,
                TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
                VerticalScrollBarVisibility = multiline ? ScrollBarVisibility.Auto : ScrollBarVisibility.Hidden,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
                VerticalAlignment = multiline ? VerticalAlignment.Stretch : VerticalAlignment.Center,
                VerticalContentAlignment = multiline ? VerticalAlignment.Top : VerticalAlignment.Center,
                SnapsToDevicePixels = true,
                UseLayoutRounding = true
            };
            TextOptions.SetTextFormattingMode(tb, TextFormattingMode.Display);
            TextOptions.SetTextRenderingMode(tb, TextRenderingMode.ClearType);
            return tb;
        }

        /// <summary>
        /// Creates a clean input container with a TextBox and a properly managed placeholder that hides immediately
        /// when text is non-empty or when focused, completely preventing caret-over-text rendering artifacts.
        /// </summary>
        public static Grid CreateInputWithPlaceholder(ThemePalette p, string placeholderText, out TextBox textBox, double fontSize = 12.5, bool multiline = false)
        {
            var host = new Grid
            {
                Background = Brushes.Transparent,
                VerticalAlignment = multiline ? VerticalAlignment.Stretch : VerticalAlignment.Center
            };

            var placeholder = new TextBlock
            {
                Text = placeholderText ?? string.Empty,
                FontFamily = UiFont,
                FontSize = fontSize,
                Foreground = p.MutedText,
                VerticalAlignment = multiline ? VerticalAlignment.Top : VerticalAlignment.Center,
                Margin = new Thickness(2, multiline ? 1 : 0, 0, 0),
                IsHitTestVisible = false,
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            var tb = CreateStyledTextBox(p, placeholderText, fontSize, multiline);

            Action syncPlaceholder = () =>
            {
                placeholder.Visibility = (string.IsNullOrEmpty(tb.Text) && !tb.IsKeyboardFocused)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            };

            tb.TextChanged += (s, e) => syncPlaceholder();
            tb.GotKeyboardFocus += (s, e) => syncPlaceholder();
            tb.LostKeyboardFocus += (s, e) => syncPlaceholder();
            tb.Loaded += (s, e) => syncPlaceholder();

            host.Children.Add(placeholder);
            host.Children.Add(tb);

            textBox = tb;
            return host;
        }

        public static Grid CreateInputWithPlaceholder(ThemePalette p, TextBox tb, string placeholderText, double fontSize = 12.5, bool multiline = false)
        {
            var host = new Grid
            {
                Background = Brushes.Transparent,
                VerticalAlignment = multiline ? VerticalAlignment.Stretch : VerticalAlignment.Center
            };

            var placeholder = new TextBlock
            {
                Text = placeholderText ?? string.Empty,
                FontFamily = UiFont,
                FontSize = fontSize,
                Foreground = p.MutedText,
                VerticalAlignment = multiline ? VerticalAlignment.Top : VerticalAlignment.Center,
                Margin = new Thickness(2, multiline ? 1 : 0, 0, 0),
                IsHitTestVisible = false,
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            Action syncPlaceholder = () =>
            {
                placeholder.Visibility = (string.IsNullOrEmpty(tb.Text) && !tb.IsKeyboardFocused)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            };

            tb.TextChanged += (s, e) => syncPlaceholder();
            tb.GotKeyboardFocus += (s, e) => syncPlaceholder();
            tb.LostKeyboardFocus += (s, e) => syncPlaceholder();
            tb.Loaded += (s, e) => syncPlaceholder();

            host.Children.Add(placeholder);
            host.Children.Add(tb);
            return host;
        }
    }
}
