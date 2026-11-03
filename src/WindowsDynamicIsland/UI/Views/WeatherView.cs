using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using WindowsDynamicIsland.Models;
using WindowsDynamicIsland.Services;

namespace WindowsDynamicIsland.UI.Views
{
    public sealed class WeatherView : UserControl
    {
        private readonly SettingsManager _settings;
        private readonly WeatherService _weather;
        private ThemePalette _palette;

        private string _selectedCityId;
        private int _hourlyPageOffset; // 0, 6, 12, 18
        private StackPanel _citiesStrip;
        private TextBox _addCityBox;
        private StackPanel _searchResultsStrip;
        private Grid _weatherDetailsHost;

        public WeatherView(SettingsManager settings, WeatherService weather)
        {
            _settings = settings;
            _weather = weather;
            _selectedCityId = _weather.GetPrimaryCity()?.Id;

            _weather.WeatherUpdated += () =>
            {
                Application.Current?.Dispatcher?.BeginInvoke(new Action(RefreshWeatherUi));
            };

            RebuildTheme();
        }

        public void SelectCityById(string cityId)
        {
            if (string.IsNullOrWhiteSpace(cityId)) return;
            _selectedCityId = cityId;
            _hourlyPageOffset = 0;
            RefreshWeatherUi();
        }

        public void RebuildTheme()
        {
            _palette = ThemeTokens.GetPalette(_settings.Current.Theme, 1.0);
            if (string.IsNullOrEmpty(_selectedCityId) || !_weather.Cities.Any(c => c.Id == _selectedCityId))
            {
                _selectedCityId = _weather.GetPrimaryCity()?.Id;
            }

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // City strip + Add city search
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Search results row
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Forecast content

            // 1. Top Bar: Cities Pills + Search to add city + Refresh button
            var topGrid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(164) });
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _citiesStrip = new StackPanel { Orientation = Orientation.Horizontal };
            var citiesScroll = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = _citiesStrip
            };
            citiesScroll.PreviewMouseWheel += (s, e) =>
            {
                citiesScroll.ScrollToHorizontalOffset(citiesScroll.HorizontalOffset - e.Delta * 0.4);
                e.Handled = true;
            };
            Grid.SetColumn(citiesScroll, 0);
            topGrid.Children.Add(citiesScroll);

            var searchBorder = new Border
            {
                Background = _palette.CardBackground,
                BorderBrush = _palette.HairlineBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(8, 4, 8, 4)
            };
            var searchHost = ThemeTokens.CreateInputWithPlaceholder(_palette, "Добавить город...", out _addCityBox, 11.5, false);
            _addCityBox.KeyDown += async (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Enter)
                {
                    await PerformCitySearchAsync();
                }
            };
            searchBorder.Child = searchHost;
            searchBorder.ToolTip = "Введите название города и нажмите + или Enter";
            Grid.SetColumn(searchBorder, 2);
            topGrid.Children.Add(searchBorder);

            var topActions = new StackPanel { Orientation = Orientation.Horizontal };
            var searchBtn = ThemeTokens.CreateInteractiveButton(
                _palette,
                VectorIcons.Create(VectorIcons.Plus, _palette.OnAccentText, 12),
                async () => await PerformCitySearchAsync(),
                true, false, 7, new Thickness(8, 5, 8, 5), "Найти и добавить город");
            searchBtn.Margin = new Thickness(0, 0, 5, 0);
            topActions.Children.Add(searchBtn);

            var refreshBtn = ThemeTokens.CreateInteractiveButton(
                _palette,
                VectorIcons.Create(VectorIcons.Restart, _palette.PrimaryText, 12),
                async () => await _weather.RefreshAllAsync(),
                false, false, 7, new Thickness(8, 5, 8, 5), "Обновить прогноз погоды");
            topActions.Children.Add(refreshBtn);

            Grid.SetColumn(topActions, 4);
            topGrid.Children.Add(topActions);

            Grid.SetRow(topGrid, 0);
            root.Children.Add(topGrid);

            // 2. City search suggestions strip
            _searchResultsStrip = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 6),
                Visibility = Visibility.Collapsed
            };
            Grid.SetRow(_searchResultsStrip, 1);
            root.Children.Add(_searchResultsStrip);

            // 3. Main Weather Details Host
            _weatherDetailsHost = new Grid();
            Grid.SetRow(_weatherDetailsHost, 2);
            root.Children.Add(_weatherDetailsHost);

            Content = root;
            RefreshWeatherUi();
        }

        private async System.Threading.Tasks.Task PerformCitySearchAsync()
        {
            string q = _addCityBox?.Text?.Trim();
            if (string.IsNullOrEmpty(q)) return;

            _searchResultsStrip.Children.Clear();
            _searchResultsStrip.Visibility = Visibility.Visible;
            _searchResultsStrip.Children.Add(new TextBlock
            {
                Text = "Поиск города...",
                FontFamily = ThemeTokens.UiFont,
                FontSize = 11,
                Foreground = _palette.MutedText,
                VerticalAlignment = VerticalAlignment.Center
            });

            var found = await _weather.SearchCitiesAsync(q);
            _searchResultsStrip.Children.Clear();

            if (found.Count == 0)
            {
                _searchResultsStrip.Children.Add(new TextBlock
                {
                    Text = "Город не найден",
                    FontFamily = ThemeTokens.UiFont,
                    FontSize = 11,
                    Foreground = _palette.Warning
                });
                return;
            }

            foreach (var candidate in found.Take(4))
            {
                var c = candidate;
                var btn = ThemeTokens.CreateInteractiveButton(
                    _palette,
                    new TextBlock
                    {
                        Text = $"+ {c.Name} ({c.Country})",
                        FontFamily = ThemeTokens.UiFont,
                        FontSize = 11,
                        Foreground = _palette.PrimaryText
                    },
                    async () =>
                    {
                        _searchResultsStrip.Visibility = Visibility.Collapsed;
                        _addCityBox.Text = string.Empty;
                        var added = await _weather.AddCityAsync(c);
                        if (added != null)
                        {
                            _selectedCityId = added.Id;
                            _hourlyPageOffset = 0;
                            RefreshWeatherUi();
                        }
                    },
                    false, false, 6, new Thickness(8, 3, 8, 3));
                btn.Margin = new Thickness(0, 0, 5, 0);
                _searchResultsStrip.Children.Add(btn);
            }
        }

        private void RefreshWeatherUi()
        {
            if (_citiesStrip == null || _weatherDetailsHost == null) return;

            var currentCity = _weather.Cities.FirstOrDefault(c => c.Id == _selectedCityId) ?? _weather.GetPrimaryCity();
            if (currentCity != null)
            {
                _selectedCityId = currentCity.Id;
            }

            // Rebuild cities strip
            _citiesStrip.Children.Clear();
            foreach (var city in _weather.Cities)
            {
                bool selected = city.Id == _selectedCityId;
                var snap = _weather.GetWeatherForCity(city.Id);
                string tempLabel = snap != null ? "  " + _weather.FormatTemperature(snap.CurrentTemp) : "";
                string star = city.IsPrimary ? "• " : "";

                var pillContent = new StackPanel { Orientation = Orientation.Horizontal };
                pillContent.Children.Add(new TextBlock
                {
                    Text = $"{star}{city.Name}{tempLabel}",
                    FontFamily = ThemeTokens.UiFont,
                    FontSize = 11.5,
                    FontWeight = selected ? FontWeights.SemiBold : FontWeights.Medium,
                    Foreground = selected ? _palette.OnAccentText : _palette.PrimaryText
                });

                string cid = city.Id;
                var pill = ThemeTokens.CreateInteractiveButton(
                    _palette,
                    pillContent,
                    () =>
                    {
                        _selectedCityId = cid;
                        _hourlyPageOffset = 0;
                        RefreshWeatherUi();
                    },
                    selected, false, 7, new Thickness(10, 4, 10, 4));
                pill.Margin = new Thickness(0, 0, 5, 0);
                _citiesStrip.Children.Add(pill);
            }

            // Rebuild main weather body
            _weatherDetailsHost.Children.Clear();
            if (currentCity == null) return;

            var weatherData = _weather.GetWeatherForCity(currentCity.Id);
            if (weatherData == null)
            {
                var loadingCard = ThemeTokens.CreateCard(_palette, 12, new Thickness(20));
                loadingCard.Child = new TextBlock
                {
                    Text = _weather.IsLoading
                        ? $"Загрузка метеоданных для города {currentCity.Name}..."
                        : (!string.IsNullOrEmpty(_weather.LastErrorMessage)
                            ? $"{_weather.LastErrorMessage}. Нажмите кнопку обновления."
                            : $"Нажмите кнопку обновления для загрузки прогноза ({currentCity.Name})."),
                    FontFamily = ThemeTokens.UiFont,
                    FontSize = 12,
                    Foreground = _palette.SecondaryText,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                _weatherDetailsHost.Children.Add(loadingCard);
                return;
            }

            var bodyGrid = new Grid();
            bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.18, GridUnitType.Star) });
            bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });

            // Left Column: Current Weather Block + 24-Hour Paged Grid (Zero Clipping!)
            var leftGrid = new Grid();
            leftGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            leftGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) });
            leftGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var mainCard = BuildCurrentWeatherCard(currentCity, weatherData);
            Grid.SetRow(mainCard, 0);
            leftGrid.Children.Add(mainCard);

            var hourlyCard = BuildHourlyForecastCard(weatherData);
            Grid.SetRow(hourlyCard, 2);
            leftGrid.Children.Add(hourlyCard);

            Grid.SetColumn(leftGrid, 0);
            bodyGrid.Children.Add(leftGrid);

            // Right Column: 7-Day Weekly Forecast
            var weeklyCard = BuildWeeklyForecastCard(weatherData);
            Grid.SetColumn(weeklyCard, 2);
            bodyGrid.Children.Add(weeklyCard);

            _weatherDetailsHost.Children.Add(bodyGrid);
        }

        private UIElement BuildCurrentWeatherCard(CityLocation city, WeatherSnapshot snap)
        {
            var card = ThemeTokens.CreateCard(_palette, 12, new Thickness(12, 10, 12, 10));
            var stack = new StackPanel();

            // Header row: City Name + Set Primary / Delete buttons
            var topRow = new Grid();
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var titleStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            titleStack.Children.Add(VectorIcons.Create(VectorIcons.GetWeatherIconPath(snap.WeatherCode), _palette.PrimaryText, 16));
            titleStack.Children.Add(new TextBlock
            {
                Text = city.Name,
                FontFamily = ThemeTokens.UiFont,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = _palette.PrimaryText,
                Margin = new Thickness(7, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            Grid.SetColumn(titleStack, 0);
            topRow.Children.Add(titleStack);

            var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            if (!city.IsPrimary)
            {
                var makePrimaryBtn = ThemeTokens.CreateInteractiveButton(
                    _palette,
                    new TextBlock { Text = "Основной", FontFamily = ThemeTokens.UiFont, FontSize = 10, Foreground = _palette.SecondaryText },
                    () =>
                    {
                        _weather.SetPrimaryCity(city.Id);
                        RefreshWeatherUi();
                    },
                    false, false, 5, new Thickness(6, 2, 6, 2), "Отображать этот город в спокойном состоянии");
                makePrimaryBtn.Margin = new Thickness(0, 0, 4, 0);
                actions.Children.Add(makePrimaryBtn);
            }
            if (_weather.Cities.Count > 1)
            {
                var delCityBtn = ThemeTokens.CreateInteractiveButton(
                    _palette,
                    VectorIcons.Create(VectorIcons.Trash, _palette.MutedText, 11.5),
                    () =>
                    {
                        _weather.RemoveCity(city.Id);
                        _selectedCityId = _weather.GetPrimaryCity()?.Id;
                        RefreshWeatherUi();
                    },
                    false, false, 5, new Thickness(5, 3, 5, 3), "Удалить город");
                actions.Children.Add(delCityBtn);
            }
            Grid.SetColumn(actions, 1);
            topRow.Children.Add(actions);
            stack.Children.Add(topRow);

            // Big Temperature + Condition
            var heroRow = new Grid { Margin = new Thickness(0, 4, 0, 8) };
            heroRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            heroRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            heroRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var tempText = new TextBlock
            {
                Text = _weather.FormatTemperature(snap.CurrentTemp),
                FontFamily = ThemeTokens.MonoFont,
                FontSize = 28,
                FontWeight = FontWeights.SemiBold,
                Foreground = _palette.PrimaryText,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(tempText, 0);
            heroRow.Children.Add(tempText);

            var condStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            condStack.Children.Add(new TextBlock
            {
                Text = snap.ConditionDescription,
                FontFamily = ThemeTokens.UiFont,
                FontSize = 12,
                FontWeight = FontWeights.Medium,
                Foreground = _palette.PrimaryText,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            condStack.Children.Add(new TextBlock
            {
                Text = $"Ощущается {_weather.FormatTemperature(snap.FeelsLike)} • ↑{_weather.FormatTemperature(snap.TempMax)} ↓{_weather.FormatTemperature(snap.TempMin)}",
                FontFamily = ThemeTokens.UiFont,
                FontSize = 10.5,
                Foreground = _palette.MutedText,
                Margin = new Thickness(0, 1, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            Grid.SetColumn(condStack, 2);
            heroRow.Children.Add(condStack);
            stack.Children.Add(heroRow);

            // 3 Telemetry Pills: Humidity, Wind, Precipitation
            var metricsGrid = new Grid();
            metricsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            metricsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
            metricsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            metricsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
            metricsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var m1 = BuildMetricBox("Влажность", $"{snap.Humidity}%");
            var m2 = BuildMetricBox("Ветер", $"{snap.WindSpeedKmh:0.#} км/ч");
            var m3 = BuildMetricBox("Осадки", $"{snap.PrecipProbability}%");

            Grid.SetColumn(m1, 0);
            Grid.SetColumn(m2, 2);
            Grid.SetColumn(m3, 4);
            metricsGrid.Children.Add(m1);
            metricsGrid.Children.Add(m2);
            metricsGrid.Children.Add(m3);

            stack.Children.Add(metricsGrid);
            card.Child = stack;
            return card;
        }

        private UIElement BuildMetricBox(string label, string val)
        {
            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = label,
                FontFamily = ThemeTokens.UiFont,
                FontSize = 10,
                Foreground = _palette.MutedText
            });
            stack.Children.Add(new TextBlock
            {
                Text = val,
                FontFamily = ThemeTokens.MonoFont,
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = _palette.PrimaryText,
                Margin = new Thickness(0, 1, 0, 0)
            });

            return new Border
            {
                Background = _palette.RaisedBackground,
                BorderBrush = _palette.HairlineBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(7, 4, 7, 4),
                Child = stack
            };
        }

        private UIElement BuildHourlyForecastCard(WeatherSnapshot snap)
        {
            var card = ThemeTokens.CreateCard(_palette, 12, new Thickness(12, 9, 12, 9));
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(6) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            int totalHours = Math.Min(24, snap.Hourly.Count);
            const int pageSize = 5;
            if (_hourlyPageOffset >= totalHours) _hourlyPageOffset = 0;

            var headerRow = new Grid();
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            headerRow.Children.Add(new TextBlock
            {
                Text = "Почасовой прогноз (24 ч)",
                FontFamily = ThemeTokens.UiFont,
                FontSize = 11,
                FontWeight = FontWeights.Medium,
                Foreground = _palette.SecondaryText,
                VerticalAlignment = VerticalAlignment.Center
            });

            var pagerRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var prevPageBtn = ThemeTokens.CreateInteractiveButton(
                _palette,
                new TextBlock { Text = "‹", FontFamily = ThemeTokens.MonoFont, FontSize = 11, Foreground = _palette.SecondaryText, HorizontalAlignment = HorizontalAlignment.Center },
                () =>
                {
                    _hourlyPageOffset = Math.Max(0, _hourlyPageOffset - pageSize);
                    RefreshWeatherUi();
                },
                false, false, 5, new Thickness(6, 1, 6, 1), "Предыдущие часы");
            prevPageBtn.Margin = new Thickness(0, 0, 3, 0);

            var nextPageBtn = ThemeTokens.CreateInteractiveButton(
                _palette,
                new TextBlock { Text = "›", FontFamily = ThemeTokens.MonoFont, FontSize = 11, Foreground = _palette.SecondaryText, HorizontalAlignment = HorizontalAlignment.Center },
                () =>
                {
                    if (_hourlyPageOffset + pageSize < totalHours)
                    {
                        _hourlyPageOffset += pageSize;
                        RefreshWeatherUi();
                    }
                },
                false, false, 5, new Thickness(6, 1, 6, 1), "Следующие часы");

            pagerRow.Children.Add(prevPageBtn);
            pagerRow.Children.Add(nextPageBtn);
            Grid.SetColumn(pagerRow, 1);
            headerRow.Children.Add(pagerRow);

            Grid.SetRow(headerRow, 0);
            grid.Children.Add(headerRow);

            // Exact 5-column UniformGrid: zero horizontal overflow and zero half-clipped cards!
            var uniformRow = new UniformGrid
            {
                Columns = pageSize,
                Rows = 1,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            var pageItems = snap.Hourly.Skip(_hourlyPageOffset).Take(pageSize).ToList();
            for (int i = 0; i < pageItems.Count; i++)
            {
                var hr = pageItems[i];
                var itemStack = new StackPanel
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                itemStack.Children.Add(new TextBlock
                {
                    Text = hr.Time.ToString("HH:00"),
                    FontFamily = ThemeTokens.MonoFont,
                    FontSize = 10,
                    Foreground = _palette.MutedText,
                    HorizontalAlignment = HorizontalAlignment.Center
                });

                var ico = VectorIcons.Create(VectorIcons.GetWeatherIconPath(hr.WeatherCode), _palette.SecondaryText, 13.5);
                ico.Margin = new Thickness(0, 5, 0, 5);
                itemStack.Children.Add(ico);

                itemStack.Children.Add(new TextBlock
                {
                    Text = _weather.FormatTemperature(hr.Temperature),
                    FontFamily = ThemeTokens.MonoFont,
                    FontSize = 11.5,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = _palette.PrimaryText,
                    HorizontalAlignment = HorizontalAlignment.Center
                });

                itemStack.Children.Add(new TextBlock
                {
                    Text = $"{hr.PrecipProbability}%",
                    FontFamily = ThemeTokens.MonoFont,
                    FontSize = 9.5,
                    Foreground = _palette.MutedText,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 2, 0, 0)
                });

                uniformRow.Children.Add(new Border
                {
                    Background = _palette.RaisedBackground,
                    BorderBrush = _palette.HairlineBorder,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(3, 6, 3, 6),
                    Margin = new Thickness(i == 0 ? 0 : 2, 0, i == pageItems.Count - 1 ? 0 : 2, 0),
                    Child = itemStack
                });
            }

            card.PreviewMouseWheel += (s, e) =>
            {
                if (e.Delta < 0 && _hourlyPageOffset + pageSize < totalHours)
                {
                    _hourlyPageOffset += pageSize;
                    RefreshWeatherUi();
                }
                else if (e.Delta > 0 && _hourlyPageOffset > 0)
                {
                    _hourlyPageOffset = Math.Max(0, _hourlyPageOffset - pageSize);
                    RefreshWeatherUi();
                }
                e.Handled = true;
            };

            Grid.SetRow(uniformRow, 2);
            grid.Children.Add(uniformRow);

            card.Child = grid;
            return card;
        }

        private UIElement BuildWeeklyForecastCard(WeatherSnapshot snap)
        {
            var card = ThemeTokens.CreateCard(_palette, 12, new Thickness(12, 9, 12, 9));
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(6) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            grid.Children.Add(new TextBlock
            {
                Text = "Недельный прогноз (7 дней)",
                FontFamily = ThemeTokens.UiFont,
                FontSize = 11,
                FontWeight = FontWeights.Medium,
                Foreground = _palette.SecondaryText
            });

            var daysStack = new StackPanel();
            var ruCulture = new CultureInfo("ru-RU");

            for (int i = 0; i < snap.Daily.Count; i++)
            {
                var d = snap.Daily[i];
                var row = new Grid();
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                string dayName = i == 0 ? "Сегодня" : ruCulture.DateTimeFormat.GetAbbreviatedDayName(d.Date.DayOfWeek);
                if (!string.IsNullOrEmpty(dayName))
                {
                    dayName = char.ToUpper(dayName[0]) + dayName.Substring(1);
                }

                row.Children.Add(new TextBlock
                {
                    Text = dayName,
                    FontFamily = ThemeTokens.UiFont,
                    FontSize = 11,
                    FontWeight = i == 0 ? FontWeights.SemiBold : FontWeights.Normal,
                    Foreground = _palette.PrimaryText,
                    VerticalAlignment = VerticalAlignment.Center
                });

                var ico = VectorIcons.Create(VectorIcons.GetWeatherIconPath(d.WeatherCode), _palette.SecondaryText, 12.5);
                Grid.SetColumn(ico, 1);
                row.Children.Add(ico);

                var condTb = new TextBlock
                {
                    Text = WeatherService.DescribeWeatherCode(d.WeatherCode),
                    FontFamily = ThemeTokens.UiFont,
                    FontSize = 10.5,
                    Foreground = _palette.SecondaryText,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(4, 0, 4, 0)
                };
                Grid.SetColumn(condTb, 2);
                row.Children.Add(condTb);

                var precipTb = new TextBlock
                {
                    Text = $"{d.PrecipProbabilityMax}%",
                    FontFamily = ThemeTokens.MonoFont,
                    FontSize = 10,
                    Foreground = _palette.MutedText,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 0, 6, 0)
                };
                Grid.SetColumn(precipTb, 3);
                row.Children.Add(precipTb);

                var minMaxTb = new TextBlock
                {
                    Text = $"{_weather.FormatTemperature(d.TempMin)} / {_weather.FormatTemperature(d.TempMax)}",
                    FontFamily = ThemeTokens.MonoFont,
                    FontSize = 11,
                    FontWeight = FontWeights.Medium,
                    Foreground = _palette.PrimaryText,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Right
                };
                Grid.SetColumn(minMaxTb, 4);
                row.Children.Add(minMaxTb);

                daysStack.Children.Add(new Border
                {
                    Background = _palette.RaisedBackground,
                    CornerRadius = new CornerRadius(5),
                    Padding = new Thickness(7, 4, 7, 4),
                    Margin = new Thickness(0, 0, 0, 3),
                    Child = row
                });
            }

            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = daysStack
            };
            Grid.SetRow(scroll, 2);
            grid.Children.Add(scroll);

            card.Child = grid;
            return card;
        }
    }
}
