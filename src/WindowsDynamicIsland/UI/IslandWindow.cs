using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using WindowsDynamicIsland.Models;
using WindowsDynamicIsland.Services;
using WindowsDynamicIsland.UI.Views;
using WindowsDynamicIsland.Win32;
using Application = System.Windows.Application;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Cursors = System.Windows.Input.Cursors;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Orientation = System.Windows.Controls.Orientation;
using Point = System.Windows.Point;
using TextBox = System.Windows.Controls.TextBox;

namespace WindowsDynamicIsland.UI
{
    public sealed class IslandWindow : Window
    {
        private const int HOTKEY_ID_TOGGLE = 9001;
        private const int HOTKEY_ID_NOTES = 9002;
        private const int HOTKEY_ID_CLIPBOARD = 9003;

        // Compact, calibrated proportions
        private const double BaseRetractedWidth = 144.0;
        private const double BaseRetractedHeight = 20.0;
        private const double BaseCollapsedWidth = 246.0;
        private const double BaseCollapsedHeight = 34.0;
        private const double BaseQuickNoteWidth = 340.0;
        private const double BaseQuickNoteHeight = 236.0;
        private const double BaseToastWidth = 380.0;
        private const double BaseToastHeight = 52.0;
        private const double BaseExpandedWidth = 730.0;
        private const double BaseExpandedHeight = 402.0;

        private readonly SettingsManager _settings;
        private readonly EventQueueManager _eventQueue;
        private readonly ClipboardManager _clipboard;
        private readonly FileShelfManager _shelf;
        private readonly NotesManager _notes;
        private readonly TimerService _timers;
        private readonly WeatherService _weather;
        private readonly MediaSessionService _media;
        private readonly SystemIntegrationService _sys;
        private readonly AnimationEngine _animEngine;

        private IntPtr _hwnd;
        private NotifyIcon _trayIcon;
        private ThemePalette _palette;
        private AppThemeMode _lastAppliedTheme;

        private readonly DispatcherTimer _clockTimer;
        private readonly DispatcherTimer _edgeAndIdleTimer;
        private readonly DispatcherTimer _hoverExpandTimer;
        private readonly DispatcherTimer _hoverCollapseTimer;

        private bool _isExpanded;
        private bool _isQuickNoteOpen;
        private bool _isPinnedOpen;
        private bool _isIdleRetracted;
        private bool _isFullscreenRetracted;
        private IntPtr _fullscreenWindow;
        private IntPtr _fullscreenOverride;
        private DateTime _lastInteractionUtc = DateTime.UtcNow;
        private DateTime _lastWakeFromIdleUtc = DateTime.MinValue;
        private int _activeTabIndex;

        // Outer Capsule Visuals & Liquid Drop Top-Edge Fusion
        private Grid _islandHostGrid;
        private Grid _dropMorphContainer;
        private ScaleTransform _dropScaleTransform;
        private TranslateTransform _dropTranslateTransform;
        private System.Windows.Shapes.Path _shadowPath;
        private System.Windows.Shapes.Path _liquidFillPath;
        private double _currentCornerRadius;
        private DropShadowEffect _shadowEffect;
        private Border _capsuleBorder;
        private Grid _innerRoot;
        private RectangleGeometry _innerClipGeometry;
        private ScaleTransform _scaleTransform;

        // 4 Morphological State Layers inside Capsule
        private Grid _collapsedLayer;
        private Grid _quickNoteLayer;
        private Grid _toastLayer;
        private Grid _expandedLayer;
        private ScaleTransform _expandedScaleTransform;
        private TranslateTransform _expandedTranslateTransform;

        // Collapsed State Elements
        private SmileyBadgeControl _collapsedSmiley;
        private Border _collapsedWeatherPill;
        private System.Windows.Shapes.Path _collapsedWeatherIcon;
        private TextBlock _collapsedWeatherTemp;
        private TextBlock _collapsedClockText;
        private Border _collapsedNotesBtn;
        private System.Windows.Shapes.Path _collapsedNotesIcon;
        private UIElement _collapsedNotesGlassOverlay;
        private Border _collapsedTimerPill;
        private TextBlock _collapsedTimerText;

        // Quick Note Compact State Elements
        private TextBlock _quickNoteHeaderTitle;
        private TextBox _quickNoteEditorBox;
        private TextBlock _quickNoteStatusText;
        private NoteItem _activeQuickNote;
        private bool _suppressQuickNoteTextChanged;

        // Toast State Elements
        private SmileyBadgeControl _toastSmiley;
        private Border _toastBadgeBorder;
        private TextBlock _toastBadgeText;
        private TextBlock _toastTitleText;
        private TextBlock _toastSubtitleText;
        private Border _toastProgressTrack;
        private Border _toastProgressFill;
        private double? _currentProgressRatio;
        private StackPanel _toastActionsPanel;

        // Expanded State Elements
        private SmileyBadgeControl _expandedSmiley;
        private TextBlock _expandedClockText;
        private System.Windows.Controls.Button[] _navTabButtons;
        private Border _pinOpenBtn;
        private Border _closeExpandedBtn;
        private Grid _tabViewportHost;
        private Border _navTray;
        private Border _expandedNotice;

        // Lazy-loaded Views
        private HomeView _homeView;
        private ClipboardView _clipboardView;
        private ShelfView _shelfView;
        private NotesClockView _notesClockView;
        private NotesClockView _clockView;
        private WeatherView _weatherView;
        private SettingsView _settingsView;

        public IslandWindow()
        {
            _settings = new SettingsManager();
            _eventQueue = new EventQueueManager(_settings);
            _clipboard = new ClipboardManager(_settings);
            _shelf = new FileShelfManager();
            _notes = new NotesManager(_eventQueue);
            _timers = new TimerService(_eventQueue);
            _weather = new WeatherService(_settings);
            _media = new MediaSessionService(_eventQueue);
            _sys = new SystemIntegrationService(_settings, _eventQueue);

            _animEngine = new AnimationEngine(BaseCollapsedWidth, BaseCollapsedHeight, _settings.Current.CollapsedCornerRadius);
            _animEngine.SetSpeedMultiplier(_settings.Current.AnimationSpeedMultiplier);
            _animEngine.FrameUpdated += OnAnimationFrameUpdated;
            _animEngine.AnimationSettled += OnAnimationSettled;

            Title = "Windows Dynamic Island";
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            try
            {
                string icoFile = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
                if (System.IO.File.Exists(icoFile))
                {
                    Icon = System.Windows.Media.Imaging.BitmapFrame.Create(new Uri(icoFile, UriKind.Absolute));
                }
            }
            catch
            {
            }
            Topmost = true;
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
            AllowDrop = true;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Ideal);
            TextOptions.SetTextRenderingMode(this, TextRenderingMode.ClearType);
            RenderOptions.SetClearTypeHint(this, ClearTypeHint.Enabled);
            RenderOptions.SetEdgeMode(this, EdgeMode.Unspecified);

            Width = 920;
            Height = 520;

            BuildWindowTree();
            InitializeTrayIcon();

            _hoverExpandTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(Math.Max(60, _settings.Current.ExpandDelayMs))
            };
            _hoverExpandTimer.Tick += (s, e) =>
            {
                _hoverExpandTimer.Stop();
                if (_collapsedNotesBtn != null && _collapsedNotesBtn.IsMouseOver)
                {
                    return;
                }
                if (!_isExpanded && !_isQuickNoteOpen && !_isIdleRetracted && !_isFullscreenRetracted &&
                    _settings.Current.ExpandOnHover && _capsuleBorder.IsMouseOver)
                {
                    SetExpandedState(true);
                }
            };

            _hoverCollapseTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(Math.Max(90, _settings.Current.CollapseDelayMs))
            };
            _hoverCollapseTimer.Tick += (s, e) =>
            {
                _hoverCollapseTimer.Stop();
                if (_capsuleBorder.IsMouseOver) return;

                _lastInteractionUtc = DateTime.UtcNow;

                if (_isQuickNoteOpen)
                {
                    if (_quickNoteEditorBox != null && _quickNoteEditorBox.IsKeyboardFocused)
                    {
                        return;
                    }
                    CloseQuickNoteMode();
                    return;
                }

                if (_isExpanded && !_isPinnedOpen)
                {
                    SetExpandedState(false);
                }
            };

            _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clockTimer.Tick += OnClockSecondTick;
            _clockTimer.Start();

            // 50ms timer for responsive top-edge liquid wake, idle auto-retraction, and smooth per-monitor fullscreen hiding
            _edgeAndIdleTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            _edgeAndIdleTimer.Tick += OnEdgeAndIdleTick;
            _edgeAndIdleTimer.Start();

            _eventQueue.ActiveEventChanged += OnActiveEventChanged;
            _eventQueue.SmileyMoodRequested += OnSmileyMoodRequested;
            _media.MediaStateChanged += OnMediaStateChanged;
            _clipboard.HistoryChanged += () => TriggerSmileyReaction(SmileyReactionType.ClipboardCopied);
            _shelf.ShelfChanged += () => TriggerSmileyReaction(SmileyReactionType.FileDropped);
            _notes.NotesChanged += () => TriggerSmileyReaction(SmileyReactionType.NoteTyped);
            _sys.VolumeStateChanged += (vol, muted) => TriggerSmileyReaction(muted ? SmileyReactionType.VolumeMuted : SmileyReactionType.VolumeChanged, vol);
            _sys.KeyboardStateChanged += () => TriggerSmileyReaction(SmileyReactionType.LayoutSwitched);
            _timers.TimerTick += UpdateCollapsedTimerPill;
            _weather.WeatherUpdated += UpdateCollapsedWeather;
            _sys.IpcCommandReceived += OnIpcCommandReceived;

            Loaded += OnWindowLoaded;
            Closing += OnWindowClosing;
        }

        private void BuildWindowTree()
        {
            _lastAppliedTheme = _settings.Current.Theme;
            _palette = ThemeTokens.GetPalette(_settings.Current.Theme, _settings.Current.Opacity);
            ModernStyles.UpdateThemeResources(_palette);

            var outerCanvasGrid = new Grid
            {
                Background = Brushes.Transparent,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Top
            };

            double effectiveScale = GetClampedScale(_settings.Current.IslandScale);
            _scaleTransform = new ScaleTransform(effectiveScale, effectiveScale);

            _islandHostGrid = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, _settings.Current.TopOffsetPx, 0, 0),
                LayoutTransform = _scaleTransform
            };

            _dropScaleTransform = new ScaleTransform(1.0, 1.0);
            _dropTranslateTransform = new TranslateTransform(0.0, 0.0);
            var dropTransformGroup = new TransformGroup();
            dropTransformGroup.Children.Add(_dropScaleTransform);
            dropTransformGroup.Children.Add(_dropTranslateTransform);

            _dropMorphContainer = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                RenderTransformOrigin = new Point(0.5, 0.0), // Top-edge anchor so it extrudes like a liquid drop from Y=0
                RenderTransform = dropTransformGroup
            };

            _shadowEffect = new DropShadowEffect
            {
                Color = _palette.ShadowColor,
                BlurRadius = _palette.IsLight ? 20 : 24,
                ShadowDepth = 4,
                Direction = 270,
                Opacity = _palette.ShadowOpacity
            };

            bool fusedTop = _settings.Current.TopOffsetPx <= 1.0;
            double initR = _settings.Current.CollapsedCornerRadius;
            _currentCornerRadius = initR;

            // 1. Shadow Path matching exact capsule bottom corner curvature without protruding corners
            _shadowPath = new System.Windows.Shapes.Path
            {
                Fill = Brushes.Black,
                Visibility = _palette.IsGlass ? Visibility.Collapsed : Visibility.Visible,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                IsHitTestVisible = false,
                Effect = _shadowEffect
            };

            // 3. _liquidFillPath renders the seamless droplet fill (including top concave meniscus ears)
            _liquidFillPath = new System.Windows.Shapes.Path
            {
                Fill = _palette.CapsuleBackground,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                IsHitTestVisible = false,
                SnapsToDevicePixels = false
            };

            _capsuleBorder = new Border
            {
                Width = BaseCollapsedWidth,
                Height = BaseCollapsedHeight,
                Background = Brushes.Transparent,
                BorderBrush = null,
                BorderThickness = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                AllowDrop = true,
                SnapsToDevicePixels = false,
                UseLayoutRounding = false,
                Effect = null
            };

            _capsuleBorder.MouseEnter += OnCapsuleMouseEnter;
            _capsuleBorder.MouseLeave += OnCapsuleMouseLeave;
            _capsuleBorder.DragEnter += OnCapsuleDragEnter;

            _innerClipGeometry = new RectangleGeometry(
                new Rect(0, 0, BaseCollapsedWidth - 2, BaseCollapsedHeight - 1),
                Math.Max(4, initR - 1),
                Math.Max(4, initR - 1));

            _innerRoot = new Grid
            {
                Clip = _innerClipGeometry
            };

            // Build the 4 morphological state layers
            _collapsedLayer = BuildCollapsedLayer();
            _quickNoteLayer = BuildQuickNoteLayer();
            _toastLayer = BuildToastLayer();
            _expandedLayer = BuildExpandedLayer();

            _innerRoot.Children.Add(_collapsedLayer);
            _innerRoot.Children.Add(_quickNoteLayer);
            _innerRoot.Children.Add(_toastLayer);
            _innerRoot.Children.Add(_expandedLayer);

            _capsuleBorder.Child = _innerRoot;

            var capsuleAndEarsGrid = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top
            };
            capsuleAndEarsGrid.Children.Add(_shadowPath);
            capsuleAndEarsGrid.Children.Add(_liquidFillPath);
            capsuleAndEarsGrid.Children.Add(_capsuleBorder);

            _dropMorphContainer.Children.Add(capsuleAndEarsGrid);
            _islandHostGrid.Children.Add(_dropMorphContainer);
            outerCanvasGrid.Children.Add(_islandHostGrid);
            Content = outerCanvasGrid;

            OnAnimationFrameUpdated(BaseCollapsedWidth, BaseCollapsedHeight, initR, 1.0);
            ApplyPaletteToChrome();
            UpdateClockLabels();
            UpdateCollapsedTimerPill();
            UpdateCollapsedWeather();
        }

        // ==================== LAYER 1: СПОКОЙНОЕ СОСТОЯНИЕ (COLLAPSED) ====================
        private Grid BuildCollapsedLayer()
        {
            var grid = new Grid
            {
                Margin = new Thickness(8, 0, 8, 0),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Left: Smiley + Weather
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Center: Time
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Right: Active Timer + Notes Button

            var leftStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            _collapsedSmiley = new SmileyBadgeControl(_settings);
            leftStack.Children.Add(_collapsedSmiley);

            var weatherRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            _collapsedWeatherIcon = VectorIcons.Create(VectorIcons.Weather, _palette.SecondaryText, 12.5);
            _collapsedWeatherTemp = new TextBlock
            {
                Text = "+12°",
                FontFamily = ThemeTokens.MonoFont,
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = _palette.PrimaryText,
                Margin = new Thickness(4, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            weatherRow.Children.Add(_collapsedWeatherIcon);
            weatherRow.Children.Add(_collapsedWeatherTemp);

            _collapsedWeatherPill = new Border
            {
                Background = Brushes.Transparent,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(5, 2, 5, 2),
                Margin = new Thickness(4, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = weatherRow
            };
            _collapsedWeatherPill.MouseEnter += (s, e) => _collapsedWeatherPill.Background = _palette.HoverBackground;
            _collapsedWeatherPill.MouseLeave += (s, e) => _collapsedWeatherPill.Background = Brushes.Transparent;
            _collapsedWeatherPill.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                SwitchTab(4);
                SetExpandedState(true);
            };
            leftStack.Children.Add(_collapsedWeatherPill);

            Grid.SetColumn(leftStack, 0);
            grid.Children.Add(leftStack);

            // Center: Current Time
            _collapsedClockText = new TextBlock
            {
                Text = DateTime.Now.ToString("HH:mm"),
                FontFamily = ThemeTokens.MonoFont,
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = _palette.PrimaryText,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 6, 0)
            };
            Grid.SetColumn(_collapsedClockText, 1);
            grid.Children.Add(_collapsedClockText);

            // Right group: Active Timer Pill + Dedicated Quick Notes Button
            var rightStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            _collapsedTimerPill = new Border
            {
                Background = _palette.AccentSubtle,
                BorderBrush = _palette.HairlineBorder,
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(6, 2, 6, 2),
                Margin = new Thickness(0, 0, 4, 0),
                Visibility = Visibility.Collapsed
            };
            _collapsedTimerText = new TextBlock
            {
                Text = "05:00",
                FontFamily = ThemeTokens.MonoFont,
                FontSize = 10.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = _palette.PrimaryText
            };
            _collapsedTimerPill.Child = _collapsedTimerText;
            _collapsedTimerPill.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                SwitchTab(6);
                SetExpandedState(true);
            };
            rightStack.Children.Add(_collapsedTimerPill);

            _collapsedNotesIcon = VectorIcons.Create(VectorIcons.Notes, _palette.SecondaryText, 12.5);
            _collapsedNotesGlassOverlay = ThemeTokens.BuildIos26GlassOverlay(9, false);
            _collapsedNotesGlassOverlay.Visibility = _palette.IsGlass ? Visibility.Visible : Visibility.Collapsed;
            var notesBtnHost = new Grid();
            notesBtnHost.Children.Add(_collapsedNotesGlassOverlay);
            notesBtnHost.Children.Add(new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Children = { _collapsedNotesIcon }
            });
            _collapsedNotesBtn = new Border
            {
                Width = 26,
                Height = 22,
                Background = _palette.RaisedBackground,
                BorderBrush = _palette.HairlineBorder,
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(9),
                ToolTip = "Быстрая заметка (открывает компактный блокнот без разворачивания всего острова)",
                Child = notesBtnHost
            };

            _collapsedNotesBtn.MouseEnter += (s, e) =>
            {
                _hoverExpandTimer.Stop();
                _collapsedNotesBtn.Background = _palette.HoverBackground;
                _collapsedNotesBtn.BorderBrush = _palette.SubtleBorder;
                _collapsedNotesIcon.Stroke = _palette.PrimaryText;
            };
            _collapsedNotesBtn.MouseLeave += (s, e) =>
            {
                _collapsedNotesBtn.Background = _palette.RaisedBackground;
                _collapsedNotesBtn.BorderBrush = _palette.HairlineBorder;
                _collapsedNotesIcon.Stroke = _palette.SecondaryText;
            };
            _collapsedNotesBtn.MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                _hoverExpandTimer.Stop();
            };
            _collapsedNotesBtn.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                _hoverExpandTimer.Stop();
                OpenQuickNoteMode();
            };
            rightStack.Children.Add(_collapsedNotesBtn);

            Grid.SetColumn(rightStack, 2);
            grid.Children.Add(rightStack);

            grid.MouseLeftButtonUp += (s, e) =>
            {
                if (!_isExpanded && !_isQuickNoteOpen)
                {
                    SetExpandedState(true);
                }
            };

            return grid;
        }

        // ==================== LAYER 2: КОМПАКТНЫЕ БЫСТРЫЕ ЗАМЕТКИ (QUICK NOTES POPOVER) ====================
        private Grid BuildQuickNoteLayer()
        {
            var grid = new Grid
            {
                Width = BaseQuickNoteWidth - 24,
                Height = BaseQuickNoteHeight - 20,
                Margin = new Thickness(12, 10, 12, 10),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Visibility = Visibility.Collapsed,
                Opacity = 0
            };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var header = new Grid();
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var titleRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            titleRow.Children.Add(VectorIcons.Create(VectorIcons.Notes, _palette.PrimaryText, 13));
            _quickNoteHeaderTitle = new TextBlock
            {
                Text = "Быстрая заметка",
                FontFamily = ThemeTokens.UiFont,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = _palette.PrimaryText,
                Margin = new Thickness(6, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            titleRow.Children.Add(_quickNoteHeaderTitle);
            Grid.SetColumn(titleRow, 0);
            header.Children.Add(titleRow);

            var newNoteBtn = ThemeTokens.CreateInteractiveButton(
                _palette,
                new TextBlock { Text = "+ Новая", FontFamily = ThemeTokens.UiFont, FontSize = 10.5, FontWeight = FontWeights.SemiBold, Foreground = _palette.PrimaryText },
                () =>
                {
                    _activeQuickNote = _notes.CreateNote("Быстрая заметка", "");
                    LoadActiveQuickNoteIntoEditor();
                    _quickNoteEditorBox?.Focus();
                },
                false, false, 6, new Thickness(7, 3, 7, 3), "Создать новую заметку");
            Grid.SetColumn(newNoteBtn, 1);
            header.Children.Add(newNoteBtn);

            var expandFullBtn = ThemeTokens.CreateInteractiveButton(
                _palette,
                new TextBlock { Text = "Все заметки ↗", FontFamily = ThemeTokens.UiFont, FontSize = 10.5, Foreground = _palette.SecondaryText },
                () =>
                {
                    _isQuickNoteOpen = false;
                    _quickNoteLayer.Visibility = Visibility.Collapsed;
                    SwitchTab(3);
                    SetExpandedState(true);
                },
                false, false, 6, new Thickness(7, 3, 7, 3), "Открыть полный раздел «Заметки / Часы»");
            Grid.SetColumn(expandFullBtn, 3);
            header.Children.Add(expandFullBtn);

            var closeQuickBtn = ThemeTokens.CreateInteractiveButton(
                _palette,
                VectorIcons.Create(VectorIcons.Close, _palette.MutedText, 11.5),
                CloseQuickNoteMode,
                false, false, 6, new Thickness(6, 4, 6, 4), "Свернуть быстрые заметки");
            Grid.SetColumn(closeQuickBtn, 5);
            header.Children.Add(closeQuickBtn);

            Grid.SetRow(header, 0);
            grid.Children.Add(header);

            _quickNoteEditorBox = ThemeTokens.CreateStyledTextBox(_palette, "Введите заметку или список задач...", 12, true);
            _quickNoteEditorBox.TextChanged += (s, e) =>
            {
                if (_suppressQuickNoteTextChanged) return;
                if (_activeQuickNote == null)
                {
                    _activeQuickNote = _notes.CreateNote("Быстрая заметка", _quickNoteEditorBox.Text);
                }
                else
                {
                    string firstLine = (_quickNoteEditorBox.Text ?? "").Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length > 0
                        ? (_quickNoteEditorBox.Text ?? "").Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)[0].Trim()
                        : "Быстрая заметка";
                    if (firstLine.Length > 34) firstLine = firstLine.Substring(0, 34) + "…";
                    _notes.UpdateNote(_activeQuickNote.Id, firstLine, _quickNoteEditorBox.Text);
                }
                if (_quickNoteStatusText != null)
                {
                    _quickNoteStatusText.Text = "Сохранено автоматически";
                }
            };

            var editorContainer = new Border
            {
                Background = _palette.CardBackground,
                BorderBrush = _palette.HairlineBorder,
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10, 8, 10, 8),
                Child = ThemeTokens.CreateInputWithPlaceholder(_palette, _quickNoteEditorBox, "Введите заметку или список задач...", 12, true)
            };
            Grid.SetRow(editorContainer, 2);
            grid.Children.Add(editorContainer);

            var footer = new Grid();
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _quickNoteStatusText = new TextBlock
            {
                Text = "Автосохранение включено",
                FontFamily = ThemeTokens.UiFont,
                FontSize = 10.5,
                Foreground = _palette.MutedText,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(_quickNoteStatusText, 0);
            footer.Children.Add(_quickNoteStatusText);

            var rem15Btn = ThemeTokens.CreateInteractiveButton(
                _palette,
                new TextBlock { Text = "Напомнить +15м", FontFamily = ThemeTokens.UiFont, FontSize = 10.5, Foreground = _palette.SecondaryText },
                () =>
                {
                    EnsureActiveQuickNote();
                    _notes.SetReminder(_activeQuickNote.Id, DateTime.Now.AddMinutes(15));
                    _quickNoteStatusText.Text = $"🔔 Напоминание в {DateTime.Now.AddMinutes(15):HH:mm}";
                    _quickNoteStatusText.Foreground = _palette.Positive;
                },
                false, false, 6, new Thickness(7, 3, 7, 3));
            Grid.SetColumn(rem15Btn, 1);
            footer.Children.Add(rem15Btn);

            var rem60Btn = ThemeTokens.CreateInteractiveButton(
                _palette,
                new TextBlock { Text = "+1ч", FontFamily = ThemeTokens.UiFont, FontSize = 10.5, Foreground = _palette.SecondaryText },
                () =>
                {
                    EnsureActiveQuickNote();
                    _notes.SetReminder(_activeQuickNote.Id, DateTime.Now.AddHours(1));
                    _quickNoteStatusText.Text = $"🔔 Напоминание в {DateTime.Now.AddHours(1):HH:mm}";
                    _quickNoteStatusText.Foreground = _palette.Positive;
                },
                false, false, 6, new Thickness(7, 3, 7, 3));
            Grid.SetColumn(rem60Btn, 3);
            footer.Children.Add(rem60Btn);

            var remOffBtn = ThemeTokens.CreateInteractiveButton(
                _palette,
                new TextBlock { Text = "Выкл ✕", FontFamily = ThemeTokens.UiFont, FontSize = 10.5, Foreground = _palette.MutedText },
                () =>
                {
                    EnsureActiveQuickNote();
                    _notes.SetReminder(_activeQuickNote.Id, null);
                    _quickNoteStatusText.Text = "Напоминание отключено";
                    _quickNoteStatusText.Foreground = _palette.MutedText;
                },
                false, false, 6, new Thickness(7, 3, 7, 3), "Отключить напоминание");
            Grid.SetColumn(remOffBtn, 5);
            footer.Children.Add(remOffBtn);

            Grid.SetRow(footer, 4);
            grid.Children.Add(footer);

            return grid;
        }

        private void EnsureActiveQuickNote()
        {
            if (_activeQuickNote != null)
            {
                for (int i = 0; i < _notes.Notes.Count; i++)
                {
                    if (_notes.Notes[i].Id == _activeQuickNote.Id)
                    {
                        _activeQuickNote = _notes.Notes[i];
                        return;
                    }
                }
            }
            if (_notes.Notes.Count > 0)
            {
                _activeQuickNote = _notes.Notes[0];
            }
            else
            {
                _activeQuickNote = _notes.CreateNote("Быстрая заметка", "");
            }
        }

        private void LoadActiveQuickNoteIntoEditor()
        {
            EnsureActiveQuickNote();
            if (_quickNoteEditorBox == null || _activeQuickNote == null) return;
            _suppressQuickNoteTextChanged = true;
            _quickNoteEditorBox.Text = _activeQuickNote.Content ?? "";
            _suppressQuickNoteTextChanged = false;
            if (_quickNoteStatusText != null)
            {
                if (_activeQuickNote.ReminderAt.HasValue && !_activeQuickNote.ReminderCompleted)
                {
                    _quickNoteStatusText.Text = $"🔔 Напоминание в {_activeQuickNote.ReminderAt.Value:HH:mm}";
                    _quickNoteStatusText.Foreground = _palette.Positive;
                }
                else
                {
                    _quickNoteStatusText.Text = "Автосохранение включено";
                    _quickNoteStatusText.Foreground = _palette.MutedText;
                }
            }
        }

        public void OpenQuickNoteMode()
        {
            _hoverExpandTimer.Stop();
            _hoverCollapseTimer.Stop();
            _lastInteractionUtc = DateTime.UtcNow;
            WakeFromIdleIfNeeded(false);

            _isExpanded = false;
            _isQuickNoteOpen = true;

            LoadActiveQuickNoteIntoEditor();

            _collapsedLayer.Visibility = Visibility.Collapsed;
            _toastLayer.Visibility = Visibility.Collapsed;
            _expandedLayer.Visibility = Visibility.Collapsed;
            _quickNoteLayer.Visibility = Visibility.Visible;

            var fade = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(130));
            _quickNoteLayer.BeginAnimation(OpacityProperty, fade);

            UpdateCapsuleSurfaceBrush(_palette.PanelBackground);
            _animEngine.AnimateTo(BaseQuickNoteWidth, BaseQuickNoteHeight, 16.0);
        }

        public void CloseQuickNoteMode()
        {
            if (!_isQuickNoteOpen) return;
            _isQuickNoteOpen = false;
            _lastInteractionUtc = DateTime.UtcNow;
            _quickNoteLayer.Visibility = Visibility.Collapsed;
            _quickNoteLayer.Opacity = 0;

            if (_eventQueue.CurrentEvent != null)
            {
                MorphToToast(_eventQueue.CurrentEvent);
            }
            else
            {
                MorphToCollapsed();
            }
        }

        // ==================== LAYER 3: СИСТЕМНОЕ СОБЫТИЕ / УВЕДОМЛЕНИЕ (TOAST) ====================
        private Grid BuildToastLayer()
        {
            var grid = new Grid
            {
                Margin = new Thickness(12, 6, 12, 6),
                Visibility = Visibility.Collapsed,
                Opacity = 0
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Smiley
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Badge
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Title + Subtitle + Progress
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Actions

            _toastSmiley = new SmileyBadgeControl(_settings);
            _toastSmiley.Margin = new Thickness(0, 0, 8, 0);
            Grid.SetColumn(_toastSmiley, 0);
            grid.Children.Add(_toastSmiley);

            _toastBadgeText = new TextBlock
            {
                Text = "INFO",
                FontFamily = ThemeTokens.MonoFont,
                FontSize = 10.5,
                FontWeight = FontWeights.Bold,
                Foreground = _palette.OnAccentText
            };
            _toastBadgeBorder = new Border
            {
                Background = _palette.Accent,
                BorderBrush = _palette.IsLight ? _palette.Accent : _palette.SubtleBorder,
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(8, 3, 8, 3),
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = _toastBadgeText
            };
            Grid.SetColumn(_toastBadgeBorder, 1);
            grid.Children.Add(_toastBadgeBorder);

            var centerStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            _toastTitleText = new TextBlock
            {
                Text = "Системное событие",
                FontFamily = ThemeTokens.UiFont,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = _palette.PrimaryText,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            centerStack.Children.Add(_toastTitleText);

            _toastSubtitleText = new TextBlock
            {
                Text = "",
                FontFamily = ThemeTokens.UiFont,
                FontSize = 10.5,
                Foreground = _palette.SecondaryText,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            centerStack.Children.Add(_toastSubtitleText);

            _toastProgressTrack = new Border
            {
                Height = 4,
                CornerRadius = new CornerRadius(2),
                Background = _palette.RaisedBackground,
                Margin = new Thickness(0, 4, 0, 0),
                Visibility = Visibility.Collapsed,
                ClipToBounds = true
            };
            _toastProgressFill = new Border
            {
                Height = 4,
                CornerRadius = new CornerRadius(2),
                Background = _palette.PrimaryText,
                HorizontalAlignment = HorizontalAlignment.Left,
                Width = 0
            };
            _toastProgressTrack.Child = _toastProgressFill;
            _toastProgressTrack.SizeChanged += (s, e) =>
            {
                if (_currentProgressRatio.HasValue && _toastProgressTrack.Visibility == Visibility.Visible && e.NewSize.Width > 0)
                {
                    double targetW = Math.Max(0.0, Math.Min(e.NewSize.Width, e.NewSize.Width * _currentProgressRatio.Value));
                    _toastProgressFill.BeginAnimation(WidthProperty, null);
                    _toastProgressFill.Width = targetW;
                }
            };
            centerStack.Children.Add(_toastProgressTrack);

            Grid.SetColumn(centerStack, 2);
            grid.Children.Add(centerStack);

            _toastActionsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0)
            };
            Grid.SetColumn(_toastActionsPanel, 3);
            grid.Children.Add(_toastActionsPanel);

            return grid;
        }

        // ==================== LAYER 4: РАСШИРЕННОЕ СОСТОЯНИЕ (EXPANDED) ====================
        private Grid BuildExpandedLayer()
        {
            _expandedScaleTransform = new ScaleTransform(0.88, 0.88);
            _expandedTranslateTransform = new TranslateTransform(0.0, -10.0);
            var expandedTransformGroup = new TransformGroup();
            expandedTransformGroup.Children.Add(_expandedScaleTransform);
            expandedTransformGroup.Children.Add(_expandedTranslateTransform);

            var grid = new Grid
            {
                Width = BaseExpandedWidth - 26,
                Height = BaseExpandedHeight - 22,
                Margin = new Thickness(13, 11, 13, 11),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                RenderTransformOrigin = new Point(0.5, 0.0),
                RenderTransform = expandedTransformGroup,
                Visibility = Visibility.Collapsed,
                Opacity = 0
            };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header Navigation Bar
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Active Section Content

            // Top Header Bar
            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Smiley + Time
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // 6 Navigation Tabs
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Pin + Close

            var brandStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            _expandedSmiley = new SmileyBadgeControl(_settings);
            brandStack.Children.Add(_expandedSmiley);

            _expandedClockText = new TextBlock
            {
                Text = DateTime.Now.ToString("HH:mm"),
                FontFamily = ThemeTokens.MonoFont,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = _palette.PrimaryText,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0)
            };
            brandStack.Children.Add(_expandedClockText);
            Grid.SetColumn(brandStack, 0);
            headerGrid.Children.Add(brandStack);

            // 6 Navigation Tabs: Главная | Буфер | Полка | Заметки | Погода | Настройки
            // Custom stateful buttons that dynamically read _palette and _activeTabIndex so hover/leave NEVER sticks!
            var navStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            string[] tabNames = { "Главная", "Буфер", "Полка", "Заметки", "Погода", "Настройки", "Таймер" };
            string[] tabIcons =
            {
                VectorIcons.Home,
                VectorIcons.Clipboard,
                VectorIcons.Shelf,
                VectorIcons.Notes,
                VectorIcons.Weather,
                VectorIcons.Settings,
                VectorIcons.Clock
            };

            _navTabButtons = new System.Windows.Controls.Button[tabNames.Length];
            foreach (int tabIdx in new[] { 0, 1, 2, 3, 6, 4, 5 })
            {
                var btn = CreateStatefulNavTabButton(tabIdx, tabNames[tabIdx], tabIcons[tabIdx]);
                _navTabButtons[tabIdx] = btn;
                navStack.Children.Add(btn);
            }

            _navTray = new Border
            {
                Child = navStack,
                Background = _palette.CardBackground,
                CornerRadius = new CornerRadius(11),
                Padding = new Thickness(3),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(_navTray, 1);
            headerGrid.Children.Add(_navTray);

            // Right Header Controls: Pin Open + Collapse (Dynamically bound to current _palette)
            var rightControls = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 0, 0)
            };

            _pinOpenBtn = new Border
            {
                CornerRadius = new CornerRadius(7),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                Margin = new Thickness(0, 0, 4, 0),
                Cursor = Cursors.Hand,
                SnapsToDevicePixels = true,
                UseLayoutRounding = true,
                ToolTip = "Закрепить панель открытой (удобно для перетаскивания файлов и текста)"
            };
            _pinOpenBtn.MouseEnter += (s, e) =>
            {
                if (!_isPinnedOpen)
                {
                    _pinOpenBtn.Background = _palette.HoverBackground;
                    _pinOpenBtn.BorderBrush = _palette.SubtleBorder;
                }
            };
            _pinOpenBtn.MouseLeave += (s, e) => UpdatePinButtonVisual();
            _pinOpenBtn.MouseLeftButtonDown += (s, e) => e.Handled = true;
            _pinOpenBtn.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                _isPinnedOpen = !_isPinnedOpen;
                UpdatePinButtonVisual();
            };
            rightControls.Children.Add(_pinOpenBtn);

            _closeExpandedBtn = new Border
            {
                CornerRadius = new CornerRadius(7),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                Cursor = Cursors.Hand,
                SnapsToDevicePixels = true,
                UseLayoutRounding = true,
                ToolTip = "Свернуть в капсулу"
            };
            _closeExpandedBtn.MouseEnter += (s, e) =>
            {
                _closeExpandedBtn.Background = _palette.HoverBackground;
                _closeExpandedBtn.BorderBrush = _palette.SubtleBorder;
            };
            _closeExpandedBtn.MouseLeave += (s, e) => UpdateCloseButtonVisual();
            _closeExpandedBtn.MouseLeftButtonDown += (s, e) => e.Handled = true;
            _closeExpandedBtn.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                _isPinnedOpen = false;
                UpdatePinButtonVisual();
                SetExpandedState(false);
            };
            rightControls.Children.Add(_closeExpandedBtn);

            if (_navTray != null) _navTray.Background = _palette.CardBackground;
            UpdatePinButtonVisual();
            UpdateCloseButtonVisual();

            Grid.SetColumn(rightControls, 2);
            headerGrid.Children.Add(rightControls);

            Grid.SetRow(headerGrid, 0);
            grid.Children.Add(headerGrid);

            _tabViewportHost = new Grid();
            Grid.SetRow(_tabViewportHost, 2);
            grid.Children.Add(_tabViewportHost);
            _expandedNotice = new Border
            {
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(11, 7, 7, 7),
                Margin = new Thickness(8, 0, 8, 8),
                VerticalAlignment = VerticalAlignment.Bottom,
                Visibility = Visibility.Collapsed
            };
            Grid.SetRow(_expandedNotice, 2);
            grid.Children.Add(_expandedNotice);

            SwitchTab(0);
            return grid;
        }

        private System.Windows.Controls.Button CreateStatefulNavTabButton(int tabIndex, string label, string geometry)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(VectorIcons.Create(geometry, _palette.SecondaryText, 10));
            row.Children.Add(new TextBlock
            {
                Text = label,
                FontFamily = ThemeTokens.UiFont,
                FontSize = 11,
                Margin = new Thickness(3, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            var button = new System.Windows.Controls.Button
            {
                Content = row,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(5, 5, 5, 5)
            };
            button.SetResourceReference(StyleProperty, "IslandPlayerButton");
            button.Click += (sender, args) => SwitchTab(tabIndex);
            return button;
        }

        private void ApplyNavTabButtonVisual(int index)
        {
            if (_navTabButtons == null || index < 0 || index >= _navTabButtons.Length) return;
            var button = _navTabButtons[index];
            if (button == null) return;
            bool active = index == _activeTabIndex;
            button.Background = active ? _palette.Accent : Brushes.Transparent;
            button.Foreground = active ? _palette.OnAccentText : _palette.SecondaryText;
            var row = (StackPanel)button.Content;
            ((System.Windows.Shapes.Path)row.Children[0]).Stroke = button.Foreground;
            ((TextBlock)row.Children[1]).FontWeight = active ? FontWeights.SemiBold : FontWeights.Medium;
        }

        private void UpdatePinButtonVisual()
        {
            if (_pinOpenBtn == null) return;
            _pinOpenBtn.Background = _isPinnedOpen ? _palette.Accent : _palette.RaisedBackground;
            _pinOpenBtn.BorderBrush = _isPinnedOpen
                ? (_palette.IsLight ? _palette.Accent : _palette.SubtleBorder)
                : _palette.HairlineBorder;
            var icon = VectorIcons.Create(VectorIcons.Pin, _isPinnedOpen ? _palette.OnAccentText : _palette.MutedText, 12);
            _pinOpenBtn.Child = ThemeTokens.WrapContentWithGlassLayers(_palette, icon, 7, _isPinnedOpen, new Thickness(6, 4, 6, 4));
        }

        private void UpdateCloseButtonVisual()
        {
            if (_closeExpandedBtn == null) return;
            _closeExpandedBtn.Background = _palette.RaisedBackground;
            _closeExpandedBtn.BorderBrush = _palette.HairlineBorder;
            var icon = VectorIcons.Create(VectorIcons.Close, _palette.MutedText, 12);
            _closeExpandedBtn.Child = ThemeTokens.WrapContentWithGlassLayers(_palette, icon, 7, false, new Thickness(6, 4, 6, 4));
        }

        public void SwitchTab(int index)
        {
            bool tabChanged = _activeTabIndex != index;
            _activeTabIndex = Math.Max(0, Math.Min(_navTabButtons.Length - 1, index));
            _lastInteractionUtc = DateTime.UtcNow;

            for (int i = 0; i < _navTabButtons.Length; i++)
            {
                ApplyNavTabButtonVisual(i);
            }

            if (tabChanged && _isExpanded)
            {
                TriggerSmileyReaction(SmileyReactionType.TabChanged, _activeTabIndex);
            }

            UIElement targetView = GetOrCreateTabView(_activeTabIndex);
            if (_tabViewportHost.Children.Count == 1 && _tabViewportHost.Children[0] == targetView) return;
            _tabViewportHost.Children.Clear();
            targetView.Opacity = 1.0;
            _tabViewportHost.Children.Add(targetView);

            ThemeTokens.Reveal((FrameworkElement)targetView);
        }

        private void SwitchSettingsCategory(int catIndex)
        {
            if (_settingsView == null)
            {
                _settingsView = new SettingsView(_settings, _sys, ApplyAllSettingsToWindow);
            }
            _settingsView.SwitchToCategory(catIndex);
        }

        private UIElement GetOrCreateTabView(int index)
        {
            switch (index)
            {
                case 0:
                    if (_homeView == null)
                    {
                        _homeView = new HomeView(
                            _settings,
                            _media,
                            _sys,
                            _clipboard,
                            _shelf,
                            _notes,
                            _timers,
                            _weather,
                            SwitchTab,
                            () => _hwnd,
                            SwitchSettingsCategory,
                            ApplyAllSettingsToWindow,
                            () =>
                            {
                                SwitchTab(3);
                                _notesClockView?.CreateNewNoteAndOpenEditor();
                            });
                    }
                    return _homeView;
                case 1:
                    if (_clipboardView == null)
                    {
                        _clipboardView = new ClipboardView(_settings, _clipboard);
                    }
                    return _clipboardView;
                case 2:
                    if (_shelfView == null)
                    {
                        _shelfView = new ShelfView(_settings, _shelf);
                    }
                    return _shelfView;
                case 3:
                    if (_notesClockView == null)
                    {
                        _notesClockView = new NotesClockView(_settings, _notes, _timers);
                    }
                    return _notesClockView;
                case 4:
                    if (_weatherView == null)
                    {
                        _weatherView = new WeatherView(_settings, _weather);
                    }
                    return _weatherView;
                case 6:
                    if (_clockView == null)
                        _clockView = new NotesClockView(_settings, _notes, _timers, clockOnly: true);
                    return _clockView;
                default:
                    if (_settingsView == null)
                    {
                        _settingsView = new SettingsView(_settings, _sys, ApplyAllSettingsToWindow);
                    }
                    return _settingsView;
            }
        }

        // ==================== LIQUID DROP MORPHING, HOVER & IDLE AUTO-RETRACT STATE MACHINE ====================
        private int GetEffectiveExpandDelayMs()
        {
            int wakeDelay = Math.Max(200, _settings.Current.WakeExpandDelayMs);
            if ((DateTime.UtcNow - _lastWakeFromIdleUtc).TotalMilliseconds < wakeDelay + 350)
            {
                return wakeDelay;
            }
            return Math.Max(60, _settings.Current.ExpandDelayMs);
        }

        private void OnCapsuleMouseEnter(object sender, MouseEventArgs e)
        {
            _lastInteractionUtc = DateTime.UtcNow;
            _hoverCollapseTimer.Stop();

            if (_isIdleRetracted)
            {
                WakeFromIdleIfNeeded(true);
            }

            if (_collapsedNotesBtn != null && _collapsedNotesBtn.IsMouseOver)
            {
                _hoverExpandTimer.Stop();
                return;
            }

            if (!_isExpanded && !_isQuickNoteOpen && _settings.Current.ExpandOnHover)
            {
                _hoverExpandTimer.Stop();
                _hoverExpandTimer.Interval = TimeSpan.FromMilliseconds(GetEffectiveExpandDelayMs());
                _hoverExpandTimer.Start();
            }
        }

        private void OnCapsuleMouseLeave(object sender, MouseEventArgs e)
        {
            _lastInteractionUtc = DateTime.UtcNow;
            _hoverExpandTimer.Stop();
            if ((_isExpanded && !_isPinnedOpen) || _isQuickNoteOpen)
            {
                _hoverCollapseTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(90, _settings.Current.CollapseDelayMs));
                _hoverCollapseTimer.Start();
            }
        }

        private void OnCapsuleDragEnter(object sender, DragEventArgs e)
        {
            _lastInteractionUtc = DateTime.UtcNow;
            WakeFromIdleIfNeeded(false);

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                _isQuickNoteOpen = false;
                SwitchTab(2);
                if (!_isExpanded)
                {
                    SetExpandedState(true);
                }
            }
        }

        private void UpdateCapsuleSurfaceBrush(System.Windows.Media.Brush surfaceBrush)
        {
            if (_liquidFillPath != null) _liquidFillPath.Fill = surfaceBrush;
        }

        public void SetExpandedState(bool expanded)
        {
            _hoverExpandTimer.Stop();
            _hoverCollapseTimer.Stop();
            _lastInteractionUtc = DateTime.UtcNow;
            WakeFromIdleIfNeeded(false);

            if (expanded && _isFullscreenRetracted)
            {
                _fullscreenOverride = _fullscreenWindow;
                SetFullscreenRetractedState(false);
            }
            else if (!expanded)
            {
                _fullscreenOverride = IntPtr.Zero;
            }

            _isExpanded = expanded;
            UpdateExpandedNotice(_eventQueue.CurrentEvent);
            _isQuickNoteOpen = false;
            TriggerSmileyReaction(expanded ? SmileyReactionType.Expand : SmileyReactionType.Collapse);

            if (_isExpanded)
            {
                _quickNoteLayer.Visibility = Visibility.Collapsed;
                _toastLayer.Visibility = Visibility.Collapsed;
                _collapsedLayer.BeginAnimation(OpacityProperty, null);
                _expandedLayer.BeginAnimation(OpacityProperty, null);
                _collapsedLayer.Visibility = Visibility.Visible;
                _expandedLayer.Visibility = Visibility.Visible;

                UpdateCapsuleSurfaceBrush(_palette.PanelBackground);
                _animEngine.AnimateTo(BaseExpandedWidth, BaseExpandedHeight, _settings.Current.ExpandedCornerRadius);
            }
            else
            {
                _quickNoteLayer.Visibility = Visibility.Collapsed;
                _quickNoteLayer.Opacity = 0;

                if (_eventQueue.CurrentEvent != null)
                {
                    _expandedLayer.Visibility = Visibility.Collapsed;
                    _expandedLayer.Opacity = 0;
                    MorphToToast(_eventQueue.CurrentEvent);
                }
                else
                {
                    MorphToCollapsed();
                }
            }
        }

        private void MorphToCollapsed()
        {
            _isQuickNoteOpen = false;
            _lastInteractionUtc = DateTime.UtcNow;
            WakeFromIdleIfNeeded(false);

            _toastLayer.Visibility = Visibility.Collapsed;
            _quickNoteLayer.Visibility = Visibility.Collapsed;
            _collapsedLayer.BeginAnimation(OpacityProperty, null);
            _expandedLayer.BeginAnimation(OpacityProperty, null);
            _collapsedLayer.Visibility = Visibility.Visible;

            UpdateCapsuleSurfaceBrush(_palette.CapsuleBackground);
            _animEngine.AnimateTo(BaseCollapsedWidth, BaseCollapsedHeight, _settings.Current.CollapsedCornerRadius);
        }

        private void MorphToToast(IslandEvent ev)
        {
            if (_isExpanded || _isQuickNoteOpen || ev == null) return;

            _lastInteractionUtc = DateTime.UtcNow;
            WakeFromIdleIfNeeded(false);

            bool wasAlreadyToast = _toastLayer.Visibility == Visibility.Visible && _toastLayer.Opacity > 0.5;

            _collapsedLayer.Visibility = Visibility.Collapsed;
            _quickNoteLayer.Visibility = Visibility.Collapsed;
            _expandedLayer.Visibility = Visibility.Collapsed;
            _toastLayer.Visibility = Visibility.Visible;

            UpdateCapsuleSurfaceBrush(_palette.CapsuleBackground);

            if (!wasAlreadyToast)
            {
                var fade = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(120));
                _toastLayer.BeginAnimation(OpacityProperty, fade);
            }
            else
            {
                _toastLayer.BeginAnimation(OpacityProperty, null);
                _toastLayer.Opacity = 1.0;
            }

            _toastSmiley.SetMood(ev.Mood);
            _toastBadgeText.Text = string.IsNullOrEmpty(ev.BadgeText) ? "INFO" : ev.BadgeText;
            _toastTitleText.Text = ev.Title;
            _toastSubtitleText.Text = ev.Subtitle;
            _toastSubtitleText.Visibility = string.IsNullOrWhiteSpace(ev.Subtitle) ? Visibility.Collapsed : Visibility.Visible;

            if (ev.ProgressRatio.HasValue)
            {
                UpdateToastProgress(ev.ProgressRatio.Value, wasAlreadyToast);
            }
            else
            {
                _currentProgressRatio = null;
                _toastProgressFill.BeginAnimation(WidthProperty, null);
                _toastProgressTrack.Visibility = Visibility.Collapsed;
            }

            bool hasActions = !string.IsNullOrEmpty(ev.PrimaryActionLabel) || !string.IsNullOrEmpty(ev.SecondaryActionLabel);
            if (hasActions || _toastActionsPanel.Children.Count > 0)
            {
                _toastActionsPanel.Children.Clear();
                if (!string.IsNullOrEmpty(ev.PrimaryActionLabel) && ev.PrimaryAction != null)
                {
                    var btn1 = ThemeTokens.CreateInteractiveButton(
                        _palette,
                        new TextBlock { Text = ev.PrimaryActionLabel, FontFamily = ThemeTokens.UiFont, FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = _palette.OnAccentText },
                        ev.PrimaryAction,
                        true, false, 6, new Thickness(8, 4, 8, 4));
                    _toastActionsPanel.Children.Add(btn1);
                }
                if (!string.IsNullOrEmpty(ev.SecondaryActionLabel) && ev.SecondaryAction != null)
                {
                    var btn2 = ThemeTokens.CreateInteractiveButton(
                        _palette,
                        new TextBlock { Text = ev.SecondaryActionLabel, FontFamily = ThemeTokens.UiFont, FontSize = 11, Foreground = _palette.PrimaryText },
                        ev.SecondaryAction,
                        false, false, 6, new Thickness(8, 4, 8, 4));
                    btn2.Margin = new Thickness(5, 0, 0, 0);
                    _toastActionsPanel.Children.Add(btn2);
                }
            }

            double toastW = (_toastActionsPanel.Children.Count > 0) ? 450.0 : BaseToastWidth;
            _animEngine.AnimateTo(toastW, BaseToastHeight, 18.0);
        }

        private void UpdateToastProgress(double ratio, bool animate)
        {
            ratio = Math.Max(0.0, Math.Min(1.0, ratio));
            _currentProgressRatio = ratio;
            _toastProgressTrack.Visibility = Visibility.Visible;

            double trackWidth = _toastProgressTrack.ActualWidth;
            if (trackWidth <= 10.0)
            {
                _toastLayer.Measure(new System.Windows.Size(BaseToastWidth, BaseToastHeight));
                _toastLayer.Arrange(new System.Windows.Rect(0, 0, BaseToastWidth, BaseToastHeight));
                trackWidth = _toastProgressTrack.ActualWidth;
            }
            if (trackWidth <= 10.0)
            {
                trackWidth = 250.0;
            }

            double targetW = Math.Max(0.0, Math.Min(trackWidth, trackWidth * ratio));

            if (animate)
            {
                var barAnim = new DoubleAnimation(targetW, TimeSpan.FromMilliseconds(65))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                _toastProgressFill.BeginAnimation(WidthProperty, barAnim);
            }
            else
            {
                _toastProgressFill.BeginAnimation(WidthProperty, null);
                _toastProgressFill.Width = targetW;
            }
        }

        private void EnterIdleRetractedState()
        {
            if (_isIdleRetracted || _isExpanded || _isQuickNoteOpen || _eventQueue.CurrentEvent != null || _isFullscreenRetracted)
                return;

            _isIdleRetracted = true;
            _hoverExpandTimer.Stop();

            var fadeOut = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(150))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            _collapsedLayer.BeginAnimation(OpacityProperty, fadeOut);

            // Narrow into a sleek liquid droplet and glide upward into the top bezel leaving a subtle 4px lip
            _animEngine.AnimateTo(BaseRetractedWidth, BaseRetractedHeight, 10.0);
            AnimateDropExtrusion(targetY: -(BaseRetractedHeight - 4.0), targetScaleX: 0.92, targetScaleY: 0.85, targetOpacity: 0.88, durationMs: 320);
        }

        private void WakeFromIdleIfNeeded(bool animateToCollapsed)
        {
            if (!_isIdleRetracted) return;
            _isIdleRetracted = false;
            _lastInteractionUtc = DateTime.UtcNow;
            _lastWakeFromIdleUtc = DateTime.UtcNow;
            _hoverExpandTimer.Stop();

            AnimateDropExtrusion(targetY: 0.0, targetScaleX: 1.0, targetScaleY: 1.0, targetOpacity: 1.0, durationMs: 260);

            var fadeIn = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(160))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            _collapsedLayer.BeginAnimation(OpacityProperty, fadeIn);

            if (animateToCollapsed && !_isExpanded && !_isQuickNoteOpen && _eventQueue.CurrentEvent == null)
            {
                _animEngine.AnimateTo(BaseCollapsedWidth, BaseCollapsedHeight, _settings.Current.CollapsedCornerRadius);
            }
            TriggerSmileyReaction(SmileyReactionType.WakeUp);
        }

        private void SetFullscreenRetractedState(bool retracted)
        {
            if (_isFullscreenRetracted == retracted) return;
            _isFullscreenRetracted = retracted;
            _hoverExpandTimer.Stop();
            _hoverCollapseTimer.Stop();

            if (retracted)
            {
                // Keep the current silhouette intact throughout the dismissal.
                AnimateDropExtrusion(-22, 1, 1, 0, 190);
                return;
            }

            _lastInteractionUtc = _lastWakeFromIdleUtc = DateTime.UtcNow;
            _isIdleRetracted = false;
            if (!_isExpanded && !_isQuickNoteOpen && _eventQueue.CurrentEvent == null)
            {
                _collapsedLayer.BeginAnimation(OpacityProperty, null);
                _collapsedLayer.Opacity = 1;
                _collapsedLayer.Visibility = Visibility.Visible;
                _animEngine.SnapTo(BaseCollapsedWidth, BaseCollapsedHeight, _settings.Current.CollapsedCornerRadius);
            }
            if (!_isExpanded && !_isQuickNoteOpen)
            {
                if (_eventQueue.CurrentEvent != null) MorphToToast(_eventQueue.CurrentEvent);
                else _toastLayer.Visibility = Visibility.Collapsed;
            }
            AnimateDropExtrusion(0, 1, 1, 1, 300);
        }

        private void AnimateDropExtrusion(double targetY, double targetScaleX, double targetScaleY, double targetOpacity, int durationMs)
        {
            void Retarget(DependencyObject visual, DependencyProperty property, double target,
                Action<DependencyProperty, AnimationTimeline> animate)
            {
                double current = (double)visual.GetValue(property);
                animate(property, null);
                visual.SetValue(property, target);
                if (!SystemParameters.ClientAreaAnimation || Math.Abs(current - target) < 0.001) return;
                animate(property, new DoubleAnimation(current, target, TimeSpan.FromMilliseconds(durationMs))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                    FillBehavior = FillBehavior.Stop
                });
            }

            Retarget(_dropTranslateTransform, TranslateTransform.YProperty, targetY, _dropTranslateTransform.BeginAnimation);
            Retarget(_dropScaleTransform, ScaleTransform.ScaleXProperty, targetScaleX, _dropScaleTransform.BeginAnimation);
            Retarget(_dropScaleTransform, ScaleTransform.ScaleYProperty, targetScaleY, _dropScaleTransform.BeginAnimation);
            Retarget(_dropMorphContainer, OpacityProperty, targetOpacity, _dropMorphContainer.BeginAnimation);
        }

        private void OnAnimationSettled()
        {
            if (_expandedLayer != null)
            {
                if (!_isExpanded)
                {
                    _expandedLayer.Visibility = Visibility.Collapsed;
                    _expandedLayer.Opacity = 0.0;
                }
                else
                {
                    _expandedLayer.Opacity = 1.0;
                    _expandedScaleTransform.ScaleX = 1.0;
                    _expandedScaleTransform.ScaleY = 1.0;
                    _expandedTranslateTransform.Y = 0.0;
                }
            }
            if (_collapsedLayer != null && _isExpanded)
            {
                _collapsedLayer.Visibility = Visibility.Collapsed;
            }
        }

        private void OnAnimationFrameUpdated(double w, double h, double radius, double opacity)
        {
            double clampedW = Math.Max(90, w);
            double clampedH = Math.Max(14, h);
            bool fusedTop = _settings.Current.TopOffsetPx <= 1.0;

            double ear = fusedTop ? Math.Min(12.0, Math.Max(4.0, clampedH * 0.36)) : 0.0;
            double maxCr = fusedTop
                ? Math.Max(4.0, Math.Min(clampedW * 0.5, clampedH - ear))
                : Math.Max(4.0, Math.Min(clampedW, clampedH) * 0.5);
            double clampedR = Math.Max(4.0, Math.Min(radius, maxCr));
            _currentCornerRadius = clampedR;

            _capsuleBorder.Width = clampedW;
            _capsuleBorder.Height = clampedH;
            _capsuleBorder.Margin = new Thickness(ear, 0, ear, 0);

            if (_shadowPath != null)
            {
                // Shadow path perfectly matches the capsule bottom corner radius and is inset by 1px so black fill never peaks past the contour
                double shW = Math.Max(10, clampedW - 2);
                double shH = Math.Max(10, clampedH - 1);
                double shR = Math.Max(3, clampedR - 1);
                double shOffX = (fusedTop && ear > 0.5) ? ear + 1 : 1;
                _shadowPath.Data = BuildLiquidDropGeometry(shW, shH, shR, 0.0, fusedTop, true, shOffX, 0.0);
            }

            Geometry fillGeom = BuildLiquidDropGeometry(clampedW, clampedH, clampedR, ear, fusedTop, true);

            if (_liquidFillPath != null)
            {
                _liquidFillPath.Data = fillGeom;
            }

            // Frame-locked Apple Dynamic Island inner content morph (Scale + TranslateY + Cross-fade)
            if (!_isQuickNoteOpen && _toastLayer?.Visibility != Visibility.Visible && !_isIdleRetracted && _expandedLayer != null && _collapsedLayer != null)
            {
                double expandProgress = Math.Max(0.0, Math.Min(1.0, (clampedW - BaseCollapsedWidth) / (BaseExpandedWidth - BaseCollapsedWidth)));
                if (expandProgress > 0.005 || _isExpanded)
                {
                    _expandedLayer.Visibility = Visibility.Visible;
                    double reveal = Math.Max(0, Math.Min(1, (expandProgress - 0.18) / 0.70));
                    _expandedLayer.Opacity = reveal * reveal * (3 - 2 * reveal);
                    if (_expandedScaleTransform != null)
                    {
                        double s = 0.98 + 0.02 * expandProgress;
                        _expandedScaleTransform.ScaleX = s;
                        _expandedScaleTransform.ScaleY = s;
                    }
                    if (_expandedTranslateTransform != null)
                    {
                        _expandedTranslateTransform.Y = -8.0 * (1.0 - reveal);
                    }
                }
                else
                {
                    _expandedLayer.Visibility = Visibility.Collapsed;
                    _expandedLayer.Opacity = 0.0;
                }

                if (expandProgress < 0.98)
                {
                    _collapsedLayer.Visibility = Visibility.Visible;
                    _collapsedLayer.Opacity = Math.Max(0.0, Math.Min(1.0, 1.0 - expandProgress * 3.1));
                }
                else
                {
                    _collapsedLayer.Visibility = Visibility.Collapsed;
                }
            }

            if (_innerClipGeometry != null)
            {
                double innerW = Math.Max(10, clampedW - 2);
                double innerH = Math.Max(10, clampedH - (fusedTop ? 1 : 2));
                double innerR = Math.Max(3, clampedR - 1);
                // When fused at top (Y=0), extend clip rect upward by innerR so top corners are not rounded while bottom corners are clipped cleanly
                _innerClipGeometry.Rect = fusedTop
                    ? new Rect(0, -innerR, innerW, innerH + innerR)
                    : new Rect(0, 0, innerW, innerH);
                _innerClipGeometry.RadiusX = innerR;
                _innerClipGeometry.RadiusY = innerR;
            }
        }

        private static Geometry BuildLiquidDropGeometry(
            double w,
            double h,
            double cr,
            double ear,
            bool fusedTop,
            bool closedForFill,
            double offsetX = 0.0,
            double offsetY = 0.0)
        {
            const double k = 0.55228475; // Exact cubic Bezier circle kappa: 4*(sqrt(2)-1)/3
            var geom = new StreamGeometry();
            using (StreamGeometryContext ctx = geom.Open())
            {
                if (!fusedTop)
                {
                    double r = Math.Max(4.0, Math.Min(cr, Math.Min(w, h) * 0.5));
                    ctx.BeginFigure(new Point(offsetX + r, offsetY), closedForFill, true);
                    ctx.LineTo(new Point(offsetX + w - r, offsetY), true, true);
                    ctx.BezierTo(new Point(offsetX + w - r + r * k, offsetY), new Point(offsetX + w, offsetY + r - r * k), new Point(offsetX + w, offsetY + r), true, true);
                    ctx.LineTo(new Point(offsetX + w, offsetY + h - r), true, true);
                    ctx.BezierTo(new Point(offsetX + w, offsetY + h - r + r * k), new Point(offsetX + w - r + r * k, offsetY + h), new Point(offsetX + w - r, offsetY + h), true, true);
                    ctx.LineTo(new Point(offsetX + r, offsetY + h), true, true);
                    ctx.BezierTo(new Point(offsetX + r - r * k, offsetY + h), new Point(offsetX, offsetY + h - r + r * k), new Point(offsetX, offsetY + h - r), true, true);
                    ctx.LineTo(new Point(offsetX, offsetY + r), true, true);
                    ctx.BezierTo(new Point(offsetX, offsetY + r - r * k), new Point(offsetX + r - r * k, offsetY), new Point(offsetX + r, offsetY), true, true);
                }
                else if (ear <= 0.5)
                {
                    // Flat top at Y=0, rounded bottom corners (used for shadow behind main body so shadow never bleeds under concave ears)
                    double r = Math.Max(4.0, Math.Min(cr, Math.Min(w * 0.5, h)));
                    ctx.BeginFigure(new Point(offsetX, offsetY), closedForFill, closedForFill);
                    ctx.LineTo(new Point(offsetX, offsetY + h - r), true, true);
                    ctx.BezierTo(new Point(offsetX, offsetY + h - r + r * k), new Point(offsetX + r - r * k, offsetY + h), new Point(offsetX + r, offsetY + h), true, true);
                    ctx.LineTo(new Point(offsetX + w - r, offsetY + h), true, true);
                    ctx.BezierTo(new Point(offsetX + w - r + r * k, offsetY + h), new Point(offsetX + w, offsetY + h - r + r * k), new Point(offsetX + w, offsetY + h - r), true, true);
                    ctx.LineTo(new Point(offsetX + w, offsetY), true, true);
                }
                else
                {
                    double leftX = offsetX + ear;
                    double rightX = offsetX + ear + w;
                    double totalW = offsetX + w + ear * 2.0;

                    // Start at top-left outer meniscus tip at the top screen edge
                    // For fill: closed figure (closes along Y=0). For 1px contour: open path (traces only the curved droplet silhouette!)
                    ctx.BeginFigure(new Point(offsetX, offsetY), closedForFill, closedForFill);

                    // 1. Top-left concave meniscus curve: (0, 0) -> (leftX, ear)
                    ctx.BezierTo(
                        new Point(leftX - ear * 0.42, offsetY),
                        new Point(leftX, offsetY + ear * 0.38),
                        new Point(leftX, offsetY + ear),
                        true, true);

                    // 2. Left vertical wall: (leftX, ear) -> (leftX, h - cr)
                    if (h - cr > ear)
                    {
                        ctx.LineTo(new Point(leftX, offsetY + h - cr), true, true);
                    }

                    // 3. Bottom-left convex corner: (leftX, h - cr) -> (leftX + cr, h)
                    ctx.BezierTo(
                        new Point(leftX, offsetY + h - cr + cr * k),
                        new Point(leftX + cr - cr * k, offsetY + h),
                        new Point(leftX + cr, offsetY + h),
                        true, true);

                    // 4. Bottom horizontal edge: (leftX + cr, h) -> (rightX - cr, h)
                    ctx.LineTo(new Point(rightX - cr, offsetY + h), true, true);

                    // 5. Bottom-right convex corner: (rightX - cr, h) -> (rightX, h - cr)
                    ctx.BezierTo(
                        new Point(rightX - cr + cr * k, offsetY + h),
                        new Point(rightX, offsetY + h - cr + cr * k),
                        new Point(rightX, offsetY + h - cr),
                        true, true);

                    // 6. Right vertical wall: (rightX, h - cr) -> (rightX, ear)
                    if (h - cr > ear)
                    {
                        ctx.LineTo(new Point(rightX, offsetY + ear), true, true);
                    }

                    // 7. Top-right concave meniscus curve: (rightX, ear) -> (totalW, 0)
                    ctx.BezierTo(
                        new Point(rightX, offsetY + ear * 0.38),
                        new Point(rightX + ear * 0.42, offsetY),
                        new Point(totalW, offsetY),
                        true, true);
                }
            }
            geom.Freeze();
            return geom;
        }

        private void OnActiveEventChanged(IslandEvent ev)
        {
            UpdateExpandedNotice(ev);
            if (_isExpanded || _isQuickNoteOpen || _isFullscreenRetracted) return;
            if (ev != null)
            {
                MorphToToast(ev);
            }
            else
            {
                MorphToCollapsed();
            }
        }

        private void UpdateExpandedNotice(IslandEvent notification)
        {
            if (_expandedNotice == null) return;
            if (!_isExpanded || notification?.EventType != IslandEventType.AppNotification || notification.PrimaryAction == null)
            {
                _expandedNotice.Visibility = Visibility.Collapsed;
                _expandedNotice.Child = null;
                return;
            }
            var action = new System.Windows.Controls.Button
            {
                Content = notification.PrimaryActionLabel,
                Foreground = _palette.OnAccentText,
                Background = _palette.Accent,
                BorderBrush = _palette.Accent,
                Padding = new Thickness(10, 5, 10, 5),
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            action.SetResourceReference(StyleProperty, "IslandPlayerButton");
            action.Click += (sender, args) => notification.PrimaryAction();
            var labels = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            labels.Children.Add(new TextBlock
            {
                Text = notification.Title,
                FontWeight = FontWeights.SemiBold,
                Foreground = _palette.PrimaryText,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            labels.Children.Add(new TextBlock
            {
                Text = notification.BadgeText + "  ·  " + notification.Subtitle,
                Foreground = _palette.SecondaryText,
                FontSize = 11,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            var row = new DockPanel();
            DockPanel.SetDock(action, Dock.Right);
            row.Children.Add(action);
            row.Children.Add(labels);
            _expandedNotice.Background = _palette.IsLight ? Brushes.White : ThemeTokens.MakeBrush(255, 39, 43, 52);
            _expandedNotice.BorderThickness = new Thickness(1);
            _expandedNotice.BorderBrush = _palette.HairlineBorder;
            _expandedNotice.Child = row;
            _expandedNotice.Visibility = Visibility.Visible;
            if (SystemParameters.ClientAreaAnimation)
                _expandedNotice.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180))
                {
                    FillBehavior = FillBehavior.Stop
                });
        }

        private void OnMediaStateChanged()
        {
            SmileyMood target = _media.IsPlaying
                ? SmileyMood.MusicPlaying
                : (_timers.IsTimerRunning ? SmileyMood.TimerActive : SmileyMood.Normal);
            if (_eventQueue.CurrentEvent == null || _media.IsPlaying)
            {
                _collapsedSmiley?.SetMood(target);
                _expandedSmiley?.SetMood(target);
                _toastSmiley?.SetMood(target);
            }
        }

        private void TriggerSmileyReaction(SmileyReactionType reaction, int contextIndex = 0)
        {
            _collapsedSmiley?.React(reaction, contextIndex);
            _expandedSmiley?.React(reaction, contextIndex);
            _toastSmiley?.React(reaction, contextIndex);
        }

        private void OnSmileyMoodRequested(SmileyMood mood)
        {
            if (_media.IsPlaying && mood == SmileyMood.Normal)
            {
                mood = SmileyMood.MusicPlaying;
            }
            else if (_timers.IsTimerRunning && mood == SmileyMood.Normal)
            {
                mood = SmileyMood.TimerActive;
            }
            _collapsedSmiley?.SetMood(mood);
            _expandedSmiley?.SetMood(mood);
            _toastSmiley?.SetMood(mood);
        }

        private void OnIpcCommandReceived(string cmd)
        {
            if (string.IsNullOrWhiteSpace(cmd)) return;
            _lastInteractionUtc = DateTime.UtcNow;
            WakeFromIdleIfNeeded(true);

            if (string.Equals(cmd, "expand", StringComparison.OrdinalIgnoreCase))
            {
                _isPinnedOpen = true;
                UpdatePinButtonVisual();
                SetExpandedState(true);
            }
            else if (string.Equals(cmd, "collapse", StringComparison.OrdinalIgnoreCase))
            {
                _isPinnedOpen = false;
                _isQuickNoteOpen = false;
                UpdatePinButtonVisual();
                SetExpandedState(false);
            }
            else if (string.Equals(cmd, "quicknote", StringComparison.OrdinalIgnoreCase))
            {
                OpenQuickNoteMode();
            }
            else if (cmd.StartsWith("tab:", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(cmd.Substring(4).Trim(), out int tabIdx))
                {
                    _isPinnedOpen = true;
                    UpdatePinButtonVisual();
                    SwitchTab(tabIdx);
                    SetExpandedState(true);
                }
            }
            else if (cmd.StartsWith("theme:", StringComparison.OrdinalIgnoreCase))
            {
                string modeStr = cmd.Substring(6).Trim();
                if (string.Equals(modeStr, "dark", StringComparison.OrdinalIgnoreCase))
                    _settings.Current.Theme = AppThemeMode.Dark;
                else if (string.Equals(modeStr, "light", StringComparison.OrdinalIgnoreCase))
                    _settings.Current.Theme = AppThemeMode.Light;
                else if (string.Equals(modeStr, "ios26", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(modeStr, "glass", StringComparison.OrdinalIgnoreCase))
                    _settings.Current.Theme = AppThemeMode.Ios26Glass;
                _settings.Save();
                ApplyAllSettingsToWindow();
            }
            else if (cmd.StartsWith("setcat:", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(cmd.Substring(7).Trim(), out int catIdx))
                {
                    _isPinnedOpen = true;
                    UpdatePinButtonVisual();
                    SwitchTab(5);
                    SetExpandedState(true);
                    _settingsView?.SwitchToCategory(catIdx);
                }
            }
            else if (string.Equals(cmd, "noteedit", StringComparison.OrdinalIgnoreCase))
            {
                _isPinnedOpen = true;
                UpdatePinButtonVisual();
                SwitchTab(3);
                SetExpandedState(true);
                _notesClockView?.OpenFirstNoteEditor();
            }
            else if (string.Equals(cmd, "notelist", StringComparison.OrdinalIgnoreCase))
            {
                _isPinnedOpen = true;
                UpdatePinButtonVisual();
                SwitchTab(3);
                SetExpandedState(true);
                _notesClockView?.ShowNotesList();
            }
            else if (cmd.StartsWith("shot:", StringComparison.OrdinalIgnoreCase))
            {
                string outPath = cmd.Substring(5).Trim();
                SaveVisualSnapshot(outPath);
            }
        }

        private void SaveVisualSnapshot(string outPath)
        {
            try
            {
                WakeFromIdleIfNeeded(true);
                _animEngine.SnapTo(_animEngine.TargetWidth, _animEngine.TargetHeight, _currentCornerRadius);
                _expandedLayer.BeginAnimation(OpacityProperty, null);
                _expandedLayer.Opacity = _isExpanded ? 1.0 : 0.0;
                _expandedLayer.Visibility = _isExpanded ? Visibility.Visible : Visibility.Collapsed;
                if (_expandedScaleTransform != null)
                {
                    _expandedScaleTransform.ScaleX = 1.0;
                    _expandedScaleTransform.ScaleY = 1.0;
                }
                if (_expandedTranslateTransform != null)
                {
                    _expandedTranslateTransform.Y = 0.0;
                }
                _quickNoteLayer.BeginAnimation(OpacityProperty, null);
                _quickNoteLayer.Opacity = 1.0;
                _toastLayer.BeginAnimation(OpacityProperty, null);
                _toastLayer.Opacity = 1.0;
                _collapsedLayer.BeginAnimation(OpacityProperty, null);
                _collapsedLayer.Opacity = _isExpanded ? 0.0 : 1.0;
                _collapsedLayer.Visibility = _isExpanded ? Visibility.Collapsed : Visibility.Visible;
                _dropMorphContainer.BeginAnimation(OpacityProperty, null);
                _dropMorphContainer.Opacity = 1.0;
                _dropTranslateTransform.BeginAnimation(TranslateTransform.YProperty, null);
                _dropTranslateTransform.Y = 0;
                _dropScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                _dropScaleTransform.ScaleX = 1.0;
                _dropScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                _dropScaleTransform.ScaleY = 1.0;

                if (_tabViewportHost?.Children.Count > 0)
                {
                    _tabViewportHost.Children[0].BeginAnimation(OpacityProperty, null);
                    _tabViewportHost.Children[0].Opacity = 1.0;
                }

                UpdateLayout();

                double scale = _scaleTransform != null ? _scaleTransform.ScaleX : 1.0;
                int capW = (int)Math.Ceiling(_capsuleBorder.ActualWidth * scale);
                int capH = (int)Math.Ceiling(_capsuleBorder.ActualHeight * scale);
                bool fusedTop = _settings.Current.TopOffsetPx <= 1.0;
                int padX = 28;
                int padTop = fusedTop ? 0 : 20;
                int padBottom = 24;
                int bmpW = capW + padX * 2;
                int bmpH = capH + padTop + padBottom;

                var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(
                    bmpW,
                    bmpH,
                    96,
                    96,
                    PixelFormats.Pbgra32);

                var dv = new DrawingVisual();
                using (DrawingContext dc = dv.RenderOpen())
                {
                    if (_palette.IsGlass)
                    {
                        // Sample wallpaper behind the translucent surface in the exported preview.
                        var glassWallpaper = ThemeTokens.MakeLinearGradient(
                            new Point(0, 0), new Point(1, 1),
                            (0.0, Color.FromRgb(32, 54, 108)),
                            (0.32, Color.FromRgb(64, 42, 112)),
                            (0.68, Color.FromRgb(22, 74, 110)),
                            (1.0, Color.FromRgb(18, 28, 56)));
                        dc.DrawRoundedRectangle(glassWallpaper, null, new Rect(0, 0, bmpW, bmpH), 10, 10);

                        // Subtle ambient wallpaper orbs behind the glass to showcase optical blur & transparency
                        var orb1 = new RadialGradientBrush(Color.FromArgb(145, 92, 168, 255), Color.FromArgb(0, 92, 168, 255))
                        {
                            Center = new Point(0.28, 0.35),
                            GradientOrigin = new Point(0.28, 0.35),
                            RadiusX = 0.38,
                            RadiusY = 0.45
                        };
                        dc.DrawRectangle(orb1, null, new Rect(0, 0, bmpW, bmpH));

                        var orb2 = new RadialGradientBrush(Color.FromArgb(130, 185, 105, 255), Color.FromArgb(0, 185, 105, 255))
                        {
                            Center = new Point(0.74, 0.62),
                            GradientOrigin = new Point(0.74, 0.62),
                            RadiusX = 0.40,
                            RadiusY = 0.48
                        };
                        dc.DrawRectangle(orb2, null, new Rect(0, 0, bmpW, bmpH));
                    }
                    else
                    {
                        Color backdropColor = _palette.IsLight
                            ? Color.FromRgb(226, 228, 235)
                            : Color.FromRgb(20, 22, 28);

                        dc.DrawRoundedRectangle(
                            new SolidColorBrush(backdropColor),
                            null,
                            new Rect(0, 0, bmpW, bmpH),
                            10, 10);
                    }

                    double cornerR = _currentCornerRadius * scale;
                    double ear = fusedTop ? Math.Min(12.0, Math.Max(4.0, _capsuleBorder.ActualHeight * 0.36)) * scale : 0.0;
                    double originX = padX - ear;

                    Geometry fillGeom = BuildLiquidDropGeometry(capW, capH, cornerR, ear, fusedTop, true, originX, padTop);

                    // 1. Shadow behind main capsule body (excluded from droplet interior so translucent glass never shows an inner shadow seam!)
                    var shadowBrush = new SolidColorBrush(_palette.IsLight
                        ? Color.FromArgb(26, 20, 20, 28)
                        : Color.FromArgb(120, 0, 0, 0));
                    Geometry shadowGeom = BuildLiquidDropGeometry(capW, capH, cornerR, 0.0, fusedTop, true, padX, padTop + 2);
                    var outerShadowGeom = new CombinedGeometry(GeometryCombineMode.Exclude, shadowGeom, fillGeom);
                    if (!_palette.IsGlass) dc.DrawGeometry(shadowBrush, null, outerShadowGeom);

                    // 2. Unified liquid-drop fill (surface + concave top ears as a single seamless vector shape)
                    var surfaceBrush = _liquidFillPath != null ? _liquidFillPath.Fill : _palette.CapsuleBackground;
                    dc.DrawGeometry(surfaceBrush, null, fillGeom);

                    // 3. Inner content clipped to droplet
                    dc.PushClip(fillGeom);
                    var vb = new VisualBrush(_capsuleBorder)
                    {
                        Stretch = Stretch.Fill
                    };
                    dc.DrawRectangle(vb, null, new Rect(padX, padTop, capW, capH));
                    dc.Pop();

                    // 4. Continuous 1px contour line following the exact curved outer silhouette of the liquid droplet

                }
                rtb.Render(dv);

                var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
                string dir = Path.GetDirectoryName(outPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                using (var fs = File.Create(outPath))
                {
                    encoder.Save(fs);
                }
            }
            catch (Exception ex)
            {
                StorageService.LogError("SaveVisualSnapshot", ex);
            }
        }

        // ==================== FAST TOP-EDGE WAKE, IDLE RETRACTION & PER-MONITOR FULLSCREEN ====================
        private void OnEdgeAndIdleTick(object sender, EventArgs e)
        {
            if (_hwnd == IntPtr.Zero || _settings.Current.IsIslandHidden) return;

            // Giving the island keyboard focus must not look like leaving the fullscreen app.
            var foreground = NativeMethods.GetForegroundWindow();
            if (foreground != IntPtr.Zero && foreground != _hwnd)
            {
                _fullscreenWindow = _settings.Current.HideInFullscreenApps && NativeMethods.IsForegroundWindowFullscreen(_hwnd)
                    ? foreground : IntPtr.Zero;
                if (_fullscreenWindow != _fullscreenOverride) _fullscreenOverride = IntPtr.Zero;
            }
            if (!_settings.Current.HideInFullscreenApps) _fullscreenWindow = IntPtr.Zero;
            SetFullscreenRetractedState(_fullscreenWindow != IntPtr.Zero && _fullscreenWindow != _fullscreenOverride);
            if (_isFullscreenRetracted) return;

            // 2. Check cursor proximity to top edge / capsule
            System.Drawing.Point mousePt = System.Windows.Forms.Control.MousePosition;
            var screens = Screen.AllScreens;
            int idx = _settings.Current.SelectedMonitorIndex;
            if (screens != null && screens.Length > 0)
            {
                if (idx < 0 || idx >= screens.Length) idx = 0;
                Screen targetScreen = screens[idx];

                double centerX = targetScreen.Bounds.Left + targetScreen.Bounds.Width * 0.5 + _settings.Current.HorizontalOffsetPx;
                double topY = targetScreen.Bounds.Top;

                bool inWakeStrip = mousePt.Y >= topY &&
                                   mousePt.Y <= topY + 10 &&
                                   Math.Abs(mousePt.X - centerX) <= 150;

                if (_isIdleRetracted)
                {
                    if (inWakeStrip)
                    {
                        WakeFromIdleIfNeeded(true);
                        if (_settings.Current.ExpandOnHover)
                        {
                            // Use the dedicated WakeExpandDelayMs so waking from the top edge first reveals the calm collapsed capsule!
                            _hoverExpandTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(200, _settings.Current.WakeExpandDelayMs));
                            _hoverExpandTimer.Start();
                        }
                    }
                    return;
                }
            }

            // 3. Keep awake while mouse is over capsule, expanded, quick-note open, or toast active
            if (_capsuleBorder.IsMouseOver || _isExpanded || _isQuickNoteOpen || _eventQueue.CurrentEvent != null)
            {
                _lastInteractionUtc = DateTime.UtcNow;
                return;
            }

            // 4. Smoothly retract upward into the top edge after IdleRetractSeconds of inactivity
            if (_settings.Current.AutoRetractOnIdle)
            {
                double idleSec = (DateTime.UtcNow - _lastInteractionUtc).TotalSeconds;
                if (idleSec >= Math.Max(2, _settings.Current.IdleRetractSeconds))
                {
                    EnterIdleRetractedState();
                }
            }
        }

        private void OnClockSecondTick(object sender, EventArgs e)
        {
            UpdateClockLabels();

            if (_hwnd != IntPtr.Zero && !_settings.Current.IsIslandHidden && !_isFullscreenRetracted && _settings.Current.AllowOverFullscreenApps)
            {
                NativeMethods.SetWindowPos(
                    _hwnd,
                    NativeMethods.HWND_TOPMOST,
                    0, 0, 0, 0,
                    NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
            }
        }

        private void UpdateClockLabels()
        {
            var s = _settings.Current;
            string fmt = s.ClockUse24HourFormat
                ? (s.ClockShowSeconds ? "HH:mm:ss" : "HH:mm")
                : (s.ClockShowSeconds ? "hh:mm:ss tt" : "hh:mm tt");

            string nowStr = DateTime.Now.ToString(fmt);
            if (_collapsedClockText != null)
            {
                _collapsedClockText.Text = nowStr;
                _collapsedClockText.Visibility = s.ShowClockInCollapsed ? Visibility.Visible : Visibility.Collapsed;
            }
            if (_expandedClockText != null)
            {
                _expandedClockText.Text = nowStr;
            }
        }

        private void UpdateCollapsedTimerPill()
        {
            if (_collapsedTimerPill == null) return;
            bool show = _settings.Current.ShowTimerInCollapsed && _timers.IsTimerActive;
            _collapsedTimerPill.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            if (show)
            {
                _collapsedTimerText.Text = _timers.FormattedTimerRemaining;
            }
        }

        private void UpdateCollapsedWeather()
        {
            if (_collapsedWeatherPill == null) return;
            _collapsedWeatherPill.Visibility = _settings.Current.ShowWeatherInCollapsed ? Visibility.Visible : Visibility.Collapsed;

            var snap = _weather.GetPrimaryWeather();
            if (snap != null)
            {
                _collapsedWeatherTemp.Text = _weather.FormatTemperature(snap.CurrentTemp);
                if (_collapsedWeatherIcon != null)
                {
                    _collapsedWeatherIcon.Data = Geometry.Parse(VectorIcons.GetWeatherIconPath(snap.WeatherCode));
                }
                _collapsedWeatherPill.ToolTip = $"{snap.CityName}: {snap.ConditionDescription}, {_weather.FormatTemperature(snap.CurrentTemp)}";
            }
        }

        private void ApplyPaletteToChrome()
        {
            if (_capsuleBorder == null) return;

            bool fusedTop = _settings.Current.TopOffsetPx <= 1.0;
            bool usePanelBg = _isExpanded || _isQuickNoteOpen;
            var surfaceBrush = usePanelBg ? _palette.PanelBackground : _palette.CapsuleBackground;

            UpdateCapsuleSurfaceBrush(surfaceBrush);
            if (_shadowPath != null)
                _shadowPath.Visibility = _palette.IsGlass ? Visibility.Collapsed : Visibility.Visible;
            if (_shadowEffect != null)
            {
                _shadowEffect.Color = _palette.ShadowColor;
                _shadowEffect.Opacity = _palette.ShadowOpacity;
                _shadowEffect.BlurRadius = _palette.IsLight ? 20 : 24;
            }

            // Collapsed layer colors
            if (_collapsedClockText != null) _collapsedClockText.Foreground = _palette.PrimaryText;
            if (_collapsedWeatherTemp != null) _collapsedWeatherTemp.Foreground = _palette.PrimaryText;
            if (_collapsedWeatherIcon != null) _collapsedWeatherIcon.Stroke = _palette.SecondaryText;
            if (_collapsedTimerPill != null)
            {
                _collapsedTimerPill.Background = _palette.AccentSubtle;
                _collapsedTimerPill.BorderBrush = _palette.HairlineBorder;
            }
            if (_collapsedTimerText != null) _collapsedTimerText.Foreground = _palette.PrimaryText;
            if (_collapsedNotesBtn != null)
            {
                _collapsedNotesBtn.Background = _palette.RaisedBackground;
                _collapsedNotesBtn.BorderBrush = _palette.HairlineBorder;
            }
            if (_collapsedNotesGlassOverlay != null)
            {
                _collapsedNotesGlassOverlay.Visibility = _palette.IsGlass ? Visibility.Visible : Visibility.Collapsed;
            }
            if (_collapsedNotesIcon != null) _collapsedNotesIcon.Stroke = _palette.SecondaryText;

            // Toast layer colors
            if (_toastBadgeBorder != null)
            {
                _toastBadgeBorder.Background = _palette.Accent;
                _toastBadgeBorder.BorderBrush = _palette.IsLight ? _palette.Accent : _palette.SubtleBorder;
            }
            if (_toastBadgeText != null) _toastBadgeText.Foreground = _palette.OnAccentText;
            if (_toastTitleText != null) _toastTitleText.Foreground = _palette.PrimaryText;
            if (_toastSubtitleText != null) _toastSubtitleText.Foreground = _palette.SecondaryText;
            if (_toastProgressTrack != null) _toastProgressTrack.Background = _palette.RaisedBackground;
            if (_toastProgressFill != null) _toastProgressFill.Background = _palette.PrimaryText;

            // Expanded header colors
            if (_expandedClockText != null) _expandedClockText.Foreground = _palette.PrimaryText;
            if (_navTabButtons != null)
            {
                for (int i = 0; i < _navTabButtons.Length; i++)
                {
                    ApplyNavTabButtonVisual(i);
                }
            }
            if (_navTray != null) _navTray.Background = _palette.CardBackground;
            UpdatePinButtonVisual();
            UpdateCloseButtonVisual();
        }

        private double GetClampedScale(double requestedScale)
        {
            double s = Math.Max(0.85, Math.Min(1.30, requestedScale));
            try
            {
                var screens = Screen.AllScreens;
                if (screens != null && screens.Length > 0)
                {
                    int idx = _settings.Current.SelectedMonitorIndex;
                    if (idx < 0 || idx >= screens.Length) idx = 0;
                    var dpi = VisualTreeHelper.GetDpi(this);
                    double dpiX = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1.0;
                    double dpiY = dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1.0;
                    double maxW = (screens[idx].WorkingArea.Width / dpiX) - 48.0;
                    double maxH = (screens[idx].WorkingArea.Height / dpiY) - 64.0;
                    if (BaseExpandedWidth * s > maxW) s = Math.Max(0.85, maxW / BaseExpandedWidth);
                    if (BaseExpandedHeight * s > maxH) s = Math.Max(0.85, maxH / BaseExpandedHeight);
                }
            }
            catch
            {
            }
            return s;
        }

        private void ApplyAllSettingsToWindow()
        {
            var s = _settings.Current;
            _palette = ThemeTokens.GetPalette(s.Theme, s.Opacity);
            ModernStyles.UpdateThemeResources(_palette);
            _animEngine.SetSpeedMultiplier(s.AnimationSpeedMultiplier);

            double effectiveScale = GetClampedScale(s.IslandScale);
            _scaleTransform.ScaleX = effectiveScale;
            _scaleTransform.ScaleY = effectiveScale;
            _islandHostGrid.Margin = new Thickness(0, s.TopOffsetPx, 0, 0);

            if (!s.AutoRetractOnIdle && _isIdleRetracted)
            {
                WakeFromIdleIfNeeded(true);
            }

            ApplyPaletteToChrome();
            OnAnimationFrameUpdated(_capsuleBorder.Width, _capsuleBorder.Height,
                _isExpanded ? s.ExpandedCornerRadius : s.CollapsedCornerRadius, 1.0);

            _collapsedSmiley?.RefreshFromSettings(_palette);
            _expandedSmiley?.RefreshFromSettings(_palette);
            _toastSmiley?.RefreshFromSettings(_palette);

            if (_collapsedNotesBtn != null)
            {
                _collapsedNotesBtn.Visibility = s.ShowNotesIconInCollapsed ? Visibility.Visible : Visibility.Collapsed;
            }

            if (_innerRoot != null && _quickNoteLayer != null)
            {
                int qIdx = _innerRoot.Children.IndexOf(_quickNoteLayer);
                if (qIdx >= 0)
                {
                    _innerRoot.Children.RemoveAt(qIdx);
                    _quickNoteLayer = BuildQuickNoteLayer();
                    _innerRoot.Children.Insert(qIdx, _quickNoteLayer);
                }
            }

            UpdateClockLabels();
            UpdateCollapsedWeather();
            UpdateCollapsedTimerPill();
            PositionWindowOnSelectedMonitor();

            if (_lastAppliedTheme != s.Theme)
            {
                _lastAppliedTheme = s.Theme;
                _homeView?.RebuildTheme();
                _clipboardView?.RebuildTheme();
                _shelfView?.RebuildTheme();
                _notesClockView?.RebuildTheme();
                _clockView?.RebuildTheme();
                _weatherView?.RebuildTheme();
                _settingsView?.RebuildTheme();
                SwitchTab(_activeTabIndex);
            }
            else
            {
                _homeView?.RebuildTheme();
            }
        }

        // ==================== WIN32 INTEROP, CLICK-THROUGH HIT-TESTING & TRAY ====================
        private async void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            _hwnd = new WindowInteropHelper(this).Handle;

            int exStyle = NativeMethods.GetWindowLong(_hwnd, NativeMethods.GWL_EXSTYLE);
            exStyle |= NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_TOPMOST;
            NativeMethods.SetWindowLong(_hwnd, NativeMethods.GWL_EXSTYLE, exStyle);

            HwndSource source = HwndSource.FromHwnd(_hwnd);
            source?.AddHook(WndProc);

            try
            {
                string icoFile = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
                if (System.IO.File.Exists(icoFile))
                {
                    using (var icoBig = new System.Drawing.Icon(icoFile, 32, 32))
                    {
                        NativeMethods.SendMessage(_hwnd, (uint)NativeMethods.WM_SETICON, (IntPtr)NativeMethods.ICON_BIG, icoBig.Handle);
                    }
                    using (var icoSmall = new System.Drawing.Icon(icoFile, 16, 16))
                    {
                        NativeMethods.SendMessage(_hwnd, (uint)NativeMethods.WM_SETICON, (IntPtr)NativeMethods.ICON_SMALL, icoSmall.Handle);
                    }
                }
            }
            catch
            {
            }

            NativeMethods.AddClipboardFormatListener(_hwnd);
            RegisterGlobalHotkeys();
            PositionWindowOnSelectedMonitor();

            if (_settings.Current.WindowsNotificationListenerEnabled)
                _ = _sys.InitializeNotificationListenerAsync();
            await _media.InitializeAsync();
            await _weather.RefreshAllAsync();
        }

        private void RegisterGlobalHotkeys()
        {
            if (_hwnd == IntPtr.Zero) return;
            NativeMethods.RegisterHotKey(_hwnd, HOTKEY_ID_TOGGLE, NativeMethods.MOD_ALT | NativeMethods.MOD_NOREPEAT, 0x20);
            NativeMethods.RegisterHotKey(_hwnd, HOTKEY_ID_NOTES, NativeMethods.MOD_ALT | NativeMethods.MOD_NOREPEAT, 0x4E);
            NativeMethods.RegisterHotKey(_hwnd, HOTKEY_ID_CLIPBOARD, NativeMethods.MOD_ALT | NativeMethods.MOD_NOREPEAT, 0x42);
        }

        private void PositionWindowOnSelectedMonitor()
        {
            var screens = Screen.AllScreens;
            if (screens == null || screens.Length == 0) return;

            int idx = _settings.Current.SelectedMonitorIndex;
            if (idx < 0 || idx >= screens.Length) idx = 0;
            Screen targetScreen = screens[idx];

            var dpi = VisualTreeHelper.GetDpi(this);
            double dpiX = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1.0;
            double dpiY = dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1.0;

            double screenLeftDip = targetScreen.Bounds.Left / dpiX;
            double screenTopDip = targetScreen.Bounds.Top / dpiY;
            double screenWidthDip = targetScreen.Bounds.Width / dpiX;

            double effectiveScale = GetClampedScale(_settings.Current.IslandScale);
            if (_scaleTransform != null)
            {
                _scaleTransform.ScaleX = effectiveScale;
                _scaleTransform.ScaleY = effectiveScale;
            }

            Width = Math.Max(780, Math.Ceiling(BaseExpandedWidth * effectiveScale + 96));
            Height = Math.Max(480, Math.Ceiling(BaseExpandedHeight * effectiveScale + _settings.Current.TopOffsetPx + 84));

            Left = screenLeftDip + (screenWidthDip - Width) / 2.0 + _settings.Current.HorizontalOffsetPx;
            Top = screenTopDip;
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == NativeMethods.WM_NCHITTEST)
            {
                // Make everything outside the active capsule (and the entire window when retracted/hidden) 100% click-through!
                if (_settings.Current.IsIslandHidden || _isFullscreenRetracted || _isIdleRetracted)
                {
                    handled = true;
                    return new IntPtr(NativeMethods.HTTRANSPARENT);
                }

                try
                {
                    int x = (short)(lParam.ToInt64() & 0xFFFF);
                    int y = (short)((lParam.ToInt64() >> 16) & 0xFFFF);
                    Point ptInCapsule = _capsuleBorder.PointFromScreen(new Point(x, y));
                    if (ptInCapsule.X < 0 || ptInCapsule.Y < 0 ||
                        ptInCapsule.X > _capsuleBorder.ActualWidth ||
                        ptInCapsule.Y > _capsuleBorder.ActualHeight)
                    {
                        handled = true;
                        return new IntPtr(NativeMethods.HTTRANSPARENT);
                    }
                }
                catch
                {
                }
            }
            else if (msg == NativeMethods.WM_CLIPBOARDUPDATE)
            {
                _clipboard.OnWindowsClipboardUpdated(_hwnd);
            }
            else if (msg == NativeMethods.WM_HOTKEY)
            {
                _lastInteractionUtc = DateTime.UtcNow;
                WakeFromIdleIfNeeded(true);

                int id = wParam.ToInt32();
                if (id == HOTKEY_ID_TOGGLE)
                {
                    if (_settings.Current.IsIslandHidden)
                    {
                        ToggleIslandVisibility();
                    }
                    else
                    {
                        SetExpandedState(_isFullscreenRetracted || !_isExpanded);
                    }
                    handled = true;
                }
                else if (id == HOTKEY_ID_NOTES)
                {
                    if (_settings.Current.IsIslandHidden) ToggleIslandVisibility();
                    if (!_isExpanded && !_isQuickNoteOpen)
                    {
                        OpenQuickNoteMode();
                    }
                    else
                    {
                        SwitchTab(3);
                        SetExpandedState(true);
                    }
                    handled = true;
                }
                else if (id == HOTKEY_ID_CLIPBOARD)
                {
                    if (_settings.Current.IsIslandHidden) ToggleIslandVisibility();
                    SwitchTab(1);
                    SetExpandedState(true);
                    handled = true;
                }
            }
            else if (msg == NativeMethods.WM_DEVICECHANGE)
            {
                _sys.OnDeviceChanged(wParam.ToInt32());
            }
            else if (msg == NativeMethods.WM_DISPLAYCHANGE)
            {
                PositionWindowOnSelectedMonitor();
            }

            return IntPtr.Zero;
        }

        private void InitializeTrayIcon()
        {
            _trayIcon = new NotifyIcon
            {
                Text = "Windows Dynamic Island",
                Visible = true
            };

            try
            {
                string exePath = Assembly.GetExecutingAssembly().Location;
                string icoFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
                if (File.Exists(icoFile))
                {
                    _trayIcon.Icon = new System.Drawing.Icon(icoFile);
                }
                else if (File.Exists(exePath))
                {
                    _trayIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
                }
                else
                {
                    _trayIcon.Icon = SystemIcons.Application;
                }
            }
            catch
            {
                _trayIcon.Icon = SystemIcons.Application;
            }

            var menu = new ContextMenuStrip();
            menu.Items.Add("Развернуть Dynamic Island", null, (s, e) =>
            {
                if (_settings.Current.IsIslandHidden) ToggleIslandVisibility();
                WakeFromIdleIfNeeded(true);
                SetExpandedState(true);
            });
            menu.Items.Add("Быстрая заметка (компактно)", null, (s, e) =>
            {
                if (_settings.Current.IsIslandHidden) ToggleIslandVisibility();
                OpenQuickNoteMode();
            });
            menu.Items.Add("Скрыть / Показать остров", null, (s, e) => ToggleIslandVisibility());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Буфер обмена", null, (s, e) => { SwitchTab(1); SetExpandedState(true); });
            menu.Items.Add("Файловая полка", null, (s, e) => { SwitchTab(2); SetExpandedState(true); });
            menu.Items.Add("Таймер", null, (s, e) => { SwitchTab(6); SetExpandedState(true); });
            menu.Items.Add("Заметки", null, (s, e) => { SwitchTab(3); SetExpandedState(true); });
            menu.Items.Add("Погода", null, (s, e) => { SwitchTab(4); SetExpandedState(true); });
            menu.Items.Add("Настройки", null, (s, e) => { SwitchTab(5); SetExpandedState(true); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Выход", null, (s, e) => Application.Current.Shutdown());

            _trayIcon.ContextMenuStrip = menu;
            _trayIcon.DoubleClick += (s, e) =>
            {
                if (_settings.Current.IsIslandHidden) ToggleIslandVisibility();
                WakeFromIdleIfNeeded(true);
                SetExpandedState(!_isExpanded);
            };
        }

        public void ToggleIslandVisibility()
        {
            _settings.Current.IsIslandHidden = !_settings.Current.IsIslandHidden;
            _settings.Save();
            _islandHostGrid.Visibility = _settings.Current.IsIslandHidden ? Visibility.Hidden : Visibility.Visible;
        }

        private void OnWindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                _clockTimer?.Stop();
                _hoverExpandTimer?.Stop();
                _hoverCollapseTimer?.Stop();
                _edgeAndIdleTimer?.Stop();
                _media.Dispose();
                if (_hwnd != IntPtr.Zero)
                {
                    NativeMethods.RemoveClipboardFormatListener(_hwnd);
                    NativeMethods.UnregisterHotKey(_hwnd, HOTKEY_ID_TOGGLE);
                    NativeMethods.UnregisterHotKey(_hwnd, HOTKEY_ID_NOTES);
                    NativeMethods.UnregisterHotKey(_hwnd, HOTKEY_ID_CLIPBOARD);
                }
                if (_trayIcon != null)
                {
                    _trayIcon.Visible = false;
                    _trayIcon.Dispose();
                }
                _sys?.Dispose();
            }
            catch
            {
            }
        }
    }
}
