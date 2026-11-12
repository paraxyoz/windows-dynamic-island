using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Xml;
using WindowsDynamicIsland.Services;

namespace WindowsDynamicIsland.UI.Views
{
    public sealed class DashboardView : UserControl
    {
        private static readonly HttpClient Client = new HttpClient { Timeout = TimeSpan.FromSeconds(9) };
        private readonly SettingsManager _settings;
        private readonly SystemIntegrationService _system;
        private readonly WorkstationMonitor _monitor;
        private ThemePalette _palette;
        private Border _body;
        private int _section;
        private TextBlock _cpu, _gpu, _devices, _game, _tasks;
        private ProgressBar _cpuBar, _gpuBar;
        private Dictionary<string, decimal> _rates;
        private string _rateDate;

        public DashboardView(SettingsManager settings, SystemIntegrationService system)
        {
            _settings = settings;
            _system = system;
            _monitor = system.Workstation;
            _monitor.Changed += Refresh;
            IsVisibleChanged += (s, e) =>
            {
                _monitor.DashboardVisible = IsVisible && _section == 0;
                if (IsVisible) Refresh();
            };
            RebuildTheme();
        }

        public void RebuildTheme()
        {
            _palette = ThemeTokens.GetPalette(_settings.Current.Theme);
            var layout = new DockPanel();
            var nav = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 9) };
            string[] names = { "Компьютер", "Задачи", "Валюты", "Новости", "Перевод" };
            for (int i = 0; i < names.Length; i++)
            {
                int index = i;
                nav.Children.Add(ActionButton(names[i], () => Select(index)));
            }
            DockPanel.SetDock(nav, Dock.Top);
            layout.Children.Add(nav);
            _body = ThemeTokens.CreateCard(_palette, 16, new Thickness(16));
            layout.Children.Add(_body);
            Content = layout;
            Select(_section);
        }

        private void Select(int section)
        {
            _section = section;
            _monitor.DashboardVisible = IsVisible && section == 0;
            var stack = new StackPanel();
            switch (section)
            {
                case 1:
                    stack.Children.Add(Label("Активные задачи", 21));
                    _tasks = Detail(stack, 15);
                    Detail(stack).Text = "Подключённые инструменты передают прогресс. Завершение появляется в острове.";
                    break;
                case 2: BuildRates(stack); break;
                case 3: BuildNews(stack); break;
                case 4: BuildTranslation(stack); break;
                default:
                    var metrics = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2 };
                    metrics.Children.Add(Metric("Процессор", out _cpu, out _cpuBar));
                    metrics.Children.Add(Metric("Видеокарта", out _gpu, out _gpuBar));
                    stack.Children.Add(metrics);
                    _game = Detail(stack, 17);
                    _devices = Detail(stack, 14);
                    break;
            }
            _body.Child = new ScrollViewer
            {
                Content = stack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            Refresh();
            ThemeTokens.Reveal(stack, section == 1 ? 3 : 7, section == 3 ? 300 : 205);
        }

        private Border Metric(string title, out TextBlock value, out ProgressBar meter)
        {
            var stack = new StackPanel();
            stack.Children.Add(Label(title));
            stack.Children.Add(value = Label("—", 26));
            stack.Children.Add(meter = new ProgressBar
            {
                Maximum = 100, Height = 4, Margin = new Thickness(0, 8, 0, 0),
                Foreground = _palette.Accent, Background = _palette.RaisedBackground
            });
            meter.SetResourceReference(StyleProperty, "IslandMeter");
            return new Border { Child = stack, Background = _palette.RaisedBackground, CornerRadius = new CornerRadius(12),
                Padding = new Thickness(12), Margin = new Thickness(0, 0, 8, 6) };
        }

        private void Refresh()
        {
            if (!IsVisible) return;
            if (_section == 1 && _tasks != null)
            {
                string tasks = string.Join("\n", _system.ActiveTasks.Select(item => item.Title + " · " + item.Subtitle)
                    .Concat(_system.ActiveDownloads.Select(name => "Загрузка · " + name)));
                _tasks.Text = tasks.Length == 0 ? "Сейчас нет активных задач." : tasks;
            }
            if (_section != 0 || _cpu == null) return;
            _cpu.Text = Percent(_monitor.CpuPercent) + (_monitor.CpuTemperature.HasValue ? $" · {_monitor.CpuTemperature:0} °C" : "");
            _gpu.Text = Percent(_monitor.GpuPercent) + (_monitor.GpuTemperature.HasValue ? $" · {_monitor.GpuTemperature:0} °C" : "");
            Meter(_cpuBar, _monitor.CpuPercent ?? 0);
            Meter(_gpuBar, _monitor.GpuPercent ?? 0);
            _game.Text = _monitor.Game == null ? "Игровой режим · ожидание игры" :
                $"{_monitor.Game} · {(DateTime.Now - _monitor.GameStarted):hh\\:mm\\:ss}";
            string battery = _monitor.BatteryPercent.HasValue
                ? $"{_monitor.BatteryPercent}%{(_monitor.OnAc ? " · от сети" : "")}" : "данные недоступны";
            string mic = !_monitor.MicrophoneMuted.HasValue ? "не найден" : _monitor.MicrophoneMuted.Value ? "выключен" : "включён";
            string bluetooth = _monitor.BluetoothDevices.Count == 0 ? "нет данных об устройствах" : string.Join(", ", _monitor.BluetoothDevices);
            _devices.Text = $"Батарея · {battery}\nСистемный микрофон · {mic}\nАудиовыход · {_monitor.AudioOutput ?? "не найден"}\nBluetooth · {bluetooth}";
        }

        private static string Percent(double? value) => value.HasValue ? $"{value:0}%" : "—";

        private static void Meter(ProgressBar bar, double target)
        {
            double from = bar.Value;
            if (Math.Abs(from - target) < 0.4) return;
            bar.BeginAnimation(ProgressBar.ValueProperty, null);
            bar.Value = target;
            if (SystemParameters.ClientAreaAnimation)
                bar.BeginAnimation(ProgressBar.ValueProperty, new DoubleAnimation(from, target, TimeSpan.FromMilliseconds(550))
                { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop });
        }

        private void BuildRates(StackPanel stack)
        {
            var row = Row(stack);
            var from = Combo(row, null, 120);
            var to = Combo(row, null, 120);
            var result = Detail(stack, 26);
            var status = Detail(stack);
            bool loading = false;
            void Render()
            {
                if (_rates == null || !(from.SelectedItem is string a) || !(to.SelectedItem is string b)) return;
                result.Text = $"1 {a} = {_rates[b] / _rates[a]:0.####} {b}";
                status.Text = "Справочный курс ЕЦБ · " + _rateDate;
            }
            void Fill()
            {
                string[] codes = _rates.Keys.OrderBy(code => code).ToArray();
                from.ItemsSource = to.ItemsSource = codes;
                from.SelectedItem = "EUR";
                to.SelectedItem = _rates.ContainsKey("USD") ? "USD" : codes[0];
                Render();
            }
            from.SelectionChanged += (s, e) => Render();
            to.SelectionChanged += (s, e) => Render();
            row.Children.Add(ActionButton("Загрузить курсы", async () =>
            {
                if (loading) return;
                loading = true;
                status.Text = "Загрузка…";
                try
                {
                    var xml = Xml(await Client.GetStringAsync("https://www.ecb.europa.eu/stats/eurofxref/eurofxref-daily.xml"));
                    var rates = new Dictionary<string, decimal> { ["EUR"] = 1 };
                    foreach (XmlElement element in xml.SelectNodes("//*[@currency and @rate]"))
                        if (decimal.TryParse(element.GetAttribute("rate"), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal rate) && rate > 0)
                            rates[element.GetAttribute("currency")] = rate;
                    if (rates.Count == 1) throw new FormatException();
                    _rates = rates;
                    _rateDate = xml.SelectSingleNode("//*[@time]")?.Attributes?["time"]?.Value;
                    Fill();
                    ThemeTokens.Reveal(result, 2, 230);
                }
                catch { status.Text = "Курсы ЕЦБ сейчас недоступны."; }
                finally { loading = false; }
            }));
            if (_rates != null) Fill();
            else status.Text = "Загрузите курсы, чтобы выбрать валютную пару.";
        }

        private void BuildNews(StackPanel stack)
        {
            var row = Row(stack);
            var country = Combo(row, new[] { "Великобритания · BBC", "США · NPR", "Германия · Tagesschau" }, 220);
            var feed = new StackPanel();
            stack.Children.Add(feed);
            bool loading = false;
            async System.Threading.Tasks.Task Load()
            {
                if (loading) return;
                loading = true;
                country.IsEnabled = false;
                feed.Children.Clear();
                Detail(feed).Text = "Загрузка…";
                try
                {
                    var feeds = new[] { "https://feeds.bbci.co.uk/news/rss.xml", "https://feeds.npr.org/1001/rss.xml",
                        "https://www.tagesschau.de/index~rss2.xml" };
                    var xml = Xml(await Client.GetStringAsync(feeds[country.SelectedIndex]));
                    feed.Children.Clear();
                    foreach (XmlElement item in xml.SelectNodes("//item").Cast<XmlElement>().Take(7))
                    {
                        string title = item["title"]?.InnerText;
                        if (string.IsNullOrWhiteSpace(title) || !Uri.TryCreate(item["link"]?.InnerText, UriKind.Absolute, out var link) ||
                            link.Scheme != "https") continue;
                        var button = ActionButton(title, () =>
                        {
                            try { Process.Start(new ProcessStartInfo(link.AbsoluteUri) { UseShellExecute = true })?.Dispose(); }
                            catch (Exception ex) { StorageService.LogError("OpenHeadline", ex); }
                        });
                        button.Content = Label(title);
                        button.HorizontalContentAlignment = HorizontalAlignment.Left;
                        button.Margin = new Thickness(0, 6, 0, 0);
                        feed.Children.Add(button);
                    }
                    if (feed.Children.Count == 0) Detail(feed).Text = "В ленте пока нет заголовков.";
                    ThemeTokens.Reveal(feed, 4, 275);
                }
                catch { feed.Children.Clear(); Detail(feed).Text = "Лента недоступна. Попробуйте другой источник."; }
                finally { loading = false; country.IsEnabled = true; }
            }
            row.Children.Add(ActionButton("Обновить", async () => await Load()));
            country.SelectionChanged += async (s, e) => await Load();
            _ = Load();
        }

        private void BuildTranslation(StackPanel stack)
        {
            var row = Row(stack);
            string[] names = { "Русский", "English", "Deutsch", "Français", "Español" };
            string[] codes = { "ru", "en", "de", "fr", "es" };
            var from = Combo(row, names, 115);
            var to = Combo(row, names, 115);
            to.SelectedIndex = 1;
            var box = ThemeTokens.CreateInputWithPlaceholder(_palette, "Введите текст", out var input, 14, true);
            stack.Children.Add(new Border { Child = box, Background = _palette.RaisedBackground, MinHeight = 76,
                Margin = new Thickness(0, 10, 0, 0), Padding = new Thickness(9), CornerRadius = new CornerRadius(9) });
            var output = Detail(stack, 16);
            bool loading = false;
            row.Children.Add(ActionButton("Перевести онлайн", async () =>
            {
                string source = input.Text.Trim();
                if (loading || source.Length == 0) return;
                if (Encoding.UTF8.GetByteCount(source) > 500) { output.Text = "Сократите текст для одного запроса."; return; }
                if (from.SelectedIndex == to.SelectedIndex) { output.Text = source; return; }
                loading = true;
                output.Text = "Перевожу…";
                try
                {
                    string pair = codes[from.SelectedIndex] + "|" + codes[to.SelectedIndex];
                    string json = await Client.GetStringAsync("https://api.mymemory.translated.net/get?q=" +
                        Uri.EscapeDataString(source) + "&langpair=" + Uri.EscapeDataString(pair));
                    var response = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
                    if (!response.TryGetValue("responseStatus", out var status) || Convert.ToInt32(status) != 200 ||
                        !response.TryGetValue("responseData", out var data) || !(data is Dictionary<string, object> result) ||
                        !result.TryGetValue("translatedText", out var translated)) throw new FormatException();
                    output.Text = WebUtility.HtmlDecode(translated as string);
                    ThemeTokens.Reveal(output, 6, 245);
                }
                catch { output.Text = "Сервис перевода сейчас недоступен."; }
                finally { loading = false; }
            }));
            Detail(stack).Text = "Текст отправляется в MyMemory только по кнопке перевода.";
        }

        private static XmlDocument Xml(string text)
        {
            var xml = new XmlDocument { XmlResolver = null };
            using (var reader = XmlReader.Create(new System.IO.StringReader(text), new XmlReaderSettings
                { DtdProcessing = DtdProcessing.Prohibit, MaxCharactersInDocument = 1000000 }))
                xml.Load(reader);
            return xml;
        }

        private static StackPanel Row(StackPanel stack)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            stack.Children.Add(row);
            return row;
        }

        private static ComboBox Combo(StackPanel row, string[] values, int width)
        {
            var combo = new ComboBox { ItemsSource = values, SelectedIndex = values == null ? -1 : 0,
                Width = width, Margin = new Thickness(0, 0, 8, 0) };
            row.Children.Add(combo);
            return combo;
        }

        private TextBlock Detail(StackPanel stack, double size = 12)
        {
            var text = Label("", size);
            text.Margin = new Thickness(0, 10, 0, 0);
            stack.Children.Add(text);
            return text;
        }

        private TextBlock Label(string text, double size = 12) => new TextBlock
        {
            Text = text, FontSize = size, FontFamily = ThemeTokens.UiFont, Foreground = _palette.PrimaryText,
            TextWrapping = TextWrapping.Wrap
        };

        private Button ActionButton(string title, Action action)
        {
            var button = new Button { Content = title, Foreground = _palette.PrimaryText, Background = _palette.RaisedBackground,
                Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(0, 0, 5, 0) };
            button.SetResourceReference(StyleProperty, "IslandPlayerButton");
            button.Click += (s, e) => action();
            return button;
        }
    }
}
