using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Threading;
using WindowsDynamicIsland.Models;

namespace WindowsDynamicIsland.Services
{
    public sealed class WeatherService
    {
        private const string CitiesFileName = "weather_cities.json";
        private const string CacheFileName = "weather_cache.json";

        private readonly SettingsManager _settings;
        private readonly List<CityLocation> _cities;
        private readonly Dictionary<string, WeatherSnapshot> _cache;
        private readonly HttpClient _httpClient;
        private readonly DispatcherTimer _refreshTimer;

        public event Action WeatherUpdated;
        public event Action<string> WeatherError;

        public IReadOnlyList<CityLocation> Cities => _cities;
        public bool IsLoading { get; private set; }
        public string LastErrorMessage { get; private set; } = string.Empty;

        public WeatherService(SettingsManager settings)
        {
            _settings = settings;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "WindowsDynamicIsland/1.0");

            _cities = StorageService.LoadJson(CitiesFileName, CreateDefaultCities);
            if (_cities.Count == 0)
            {
                _cities.AddRange(CreateDefaultCities());
            }
            EnsurePrimaryCity();

            var cachedList = StorageService.LoadJson(CacheFileName, () => new List<WeatherSnapshot>());
            _cache = new Dictionary<string, WeatherSnapshot>();
            foreach (var snap in cachedList)
            {
                if (!string.IsNullOrEmpty(snap?.CityId))
                {
                    _cache[snap.CityId] = snap;
                }
            }

            _refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(Math.Max(5, _settings.Current.WeatherRefreshMinutes))
            };
            _refreshTimer.Tick += async (s, e) => await RefreshAllAsync();
            _refreshTimer.Start();

            _settings.SettingsChanged += s =>
            {
                _refreshTimer.Interval = TimeSpan.FromMinutes(Math.Max(5, s.WeatherRefreshMinutes));
                WeatherUpdated?.Invoke();
            };
        }

        private static List<CityLocation> CreateDefaultCities()
        {
            return new List<CityLocation>
            {
                new CityLocation
                {
                    Id = "moscow",
                    Name = "Москва",
                    Country = "Россия",
                    Latitude = 55.7558,
                    Longitude = 37.6173,
                    IsPrimary = true
                },
                new CityLocation
                {
                    Id = "spb",
                    Name = "Санкт-Петербург",
                    Country = "Россия",
                    Latitude = 59.9343,
                    Longitude = 30.3351,
                    IsPrimary = false
                }
            };
        }

        public CityLocation GetPrimaryCity()
        {
            EnsurePrimaryCity();
            return _cities.FirstOrDefault(c => c.IsPrimary) ?? _cities.FirstOrDefault();
        }

        public WeatherSnapshot GetPrimaryWeather()
        {
            var primary = GetPrimaryCity();
            if (primary == null) return null;
            return GetWeatherForCity(primary.Id);
        }

        public WeatherSnapshot GetWeatherForCity(string cityId)
        {
            if (string.IsNullOrEmpty(cityId)) return null;
            return _cache.TryGetValue(cityId, out var snap) ? snap : null;
        }

        public void SetPrimaryCity(string cityId)
        {
            foreach (var c in _cities)
            {
                c.IsPrimary = c.Id == cityId;
            }
            EnsurePrimaryCity();
            SaveCities();
            WeatherUpdated?.Invoke();
        }

        public void RemoveCity(string cityId)
        {
            if (_cities.Count <= 1) return; // Keep at least 1 city
            if (_cities.RemoveAll(c => c.Id == cityId) > 0)
            {
                _cache.Remove(cityId);
                EnsurePrimaryCity();
                SaveCities();
                SaveCache();
                WeatherUpdated?.Invoke();
            }
        }

        public async Task<List<CityLocation>> SearchCitiesAsync(string query)
        {
            var results = new List<CityLocation>();
            if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2) return results;

            try
            {
                string encoded = Uri.EscapeDataString(query.Trim());
                string url = $"https://geocoding-api.open-meteo.com/v1/search?name={encoded}&count=6&language=ru&format=json";
                string json = await _httpClient.GetStringAsync(url);

                var serializer = new JavaScriptSerializer();
                var root = serializer.DeserializeObject(json) as IDictionary<string, object>;
                if (root != null && root.TryGetValue("results", out object resObj) && resObj is IEnumerable list)
                {
                    foreach (object itemObj in list)
                    {
                        if (itemObj is IDictionary<string, object> item)
                        {
                            string name = GetString(item, "name");
                            string country = GetString(item, "country");
                            string admin1 = GetString(item, "admin1");
                            double lat = GetDouble(item, "latitude");
                            double lon = GetDouble(item, "longitude");

                            if (!string.IsNullOrEmpty(name))
                            {
                                string subtitle = !string.IsNullOrEmpty(country)
                                    ? (!string.IsNullOrEmpty(admin1) && admin1 != name ? $"{country}, {admin1}" : country)
                                    : admin1;
                                results.Add(new CityLocation
                                {
                                    Id = $"{lat:F2}_{lon:F2}".Replace(',', '.'),
                                    Name = name,
                                    Country = subtitle,
                                    Latitude = lat,
                                    Longitude = lon
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                StorageService.LogError("SearchCitiesAsync", ex);
            }

            return results;
        }

        public async Task<CityLocation> AddCityAsync(CityLocation city)
        {
            if (city == null) return null;
            var existing = _cities.FirstOrDefault(c =>
                Math.Abs(c.Latitude - city.Latitude) < 0.05 &&
                Math.Abs(c.Longitude - city.Longitude) < 0.05);

            if (existing != null)
            {
                await RefreshCityAsync(existing);
                return existing;
            }

            if (_cities.Count == 0) city.IsPrimary = true;
            _cities.Add(city);
            SaveCities();
            await RefreshCityAsync(city);
            return city;
        }

        public async Task RefreshAllAsync()
        {
            if (IsLoading) return;
            IsLoading = true;
            LastErrorMessage = string.Empty;

            try
            {
                foreach (var city in _cities.ToList())
                {
                    await RefreshCityInternalAsync(city);
                }
                SaveCache();
            }
            catch (Exception ex)
            {
                LastErrorMessage = "Нет подключения к метеосерверу";
                StorageService.LogError("RefreshAllAsync", ex);
                WeatherError?.Invoke(LastErrorMessage);
            }
            finally
            {
                IsLoading = false;
                WeatherUpdated?.Invoke();
            }
        }

        public async Task RefreshCityAsync(CityLocation city)
        {
            if (city == null) return;
            try
            {
                await RefreshCityInternalAsync(city);
                SaveCache();
                LastErrorMessage = string.Empty;
            }
            catch (Exception ex)
            {
                LastErrorMessage = "Не удалось обновить погоду";
                StorageService.LogError("RefreshCityAsync", ex);
                WeatherError?.Invoke(LastErrorMessage);
            }
            WeatherUpdated?.Invoke();
        }

        private async Task RefreshCityInternalAsync(CityLocation city)
        {
            string lat = city.Latitude.ToString("0.####", CultureInfo.InvariantCulture);
            string lon = city.Longitude.ToString("0.####", CultureInfo.InvariantCulture);

            string url = $"https://api.open-meteo.com/v1/forecast?latitude={lat}&longitude={lon}" +
                         "&current=temperature_2m,relative_humidity_2m,apparent_temperature,weather_code,wind_speed_10m" +
                         "&hourly=temperature_2m,precipitation_probability,weather_code" +
                         "&daily=weather_code,temperature_2m_max,temperature_2m_min,precipitation_probability_max" +
                         "&timezone=auto&forecast_days=7";

            string json = await _httpClient.GetStringAsync(url);
            var serializer = new JavaScriptSerializer();
            var root = serializer.DeserializeObject(json) as IDictionary<string, object>;
            if (root == null) throw new InvalidOperationException("Empty weather payload");

            var snap = new WeatherSnapshot
            {
                CityId = city.Id,
                CityName = city.Name,
                UpdatedAt = DateTime.Now
            };

            if (root.TryGetValue("current", out object curObj) && curObj is IDictionary<string, object> current)
            {
                snap.CurrentTemp = GetDouble(current, "temperature_2m");
                snap.FeelsLike = GetDouble(current, "apparent_temperature");
                snap.Humidity = (int)Math.Round(GetDouble(current, "relative_humidity_2m"));
                snap.WindSpeedKmh = GetDouble(current, "wind_speed_10m");
                snap.WeatherCode = (int)Math.Round(GetDouble(current, "weather_code"));
                snap.ConditionDescription = DescribeWeatherCode(snap.WeatherCode);
            }

            if (root.TryGetValue("hourly", out object hrObj) && hrObj is IDictionary<string, object> hourly)
            {
                var times = GetStringList(hourly, "time");
                var temps = GetDoubleList(hourly, "temperature_2m");
                var probs = GetDoubleList(hourly, "precipitation_probability");
                var codes = GetDoubleList(hourly, "weather_code");

                DateTime nowHour = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, DateTime.Now.Hour, 0, 0);
                int startIndex = 0;
                for (int i = 0; i < times.Count; i++)
                {
                    if (DateTime.TryParse(times[i], CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed) &&
                        parsed >= nowHour)
                    {
                        startIndex = i;
                        break;
                    }
                }

                int count = Math.Min(24, times.Count - startIndex);
                for (int i = 0; i < count; i++)
                {
                    int idx = startIndex + i;
                    DateTime.TryParse(times[idx], CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt);
                    snap.Hourly.Add(new HourlyWeatherPoint
                    {
                        Time = dt,
                        Temperature = idx < temps.Count ? temps[idx] : snap.CurrentTemp,
                        PrecipProbability = idx < probs.Count ? (int)Math.Round(probs[idx]) : 0,
                        WeatherCode = idx < codes.Count ? (int)Math.Round(codes[idx]) : snap.WeatherCode
                    });
                }

                if (snap.Hourly.Count > 0)
                {
                    snap.PrecipProbability = snap.Hourly[0].PrecipProbability;
                }
            }

            if (root.TryGetValue("daily", out object dlObj) && dlObj is IDictionary<string, object> daily)
            {
                var dates = GetStringList(daily, "time");
                var codes = GetDoubleList(daily, "weather_code");
                var maxs = GetDoubleList(daily, "temperature_2m_max");
                var mins = GetDoubleList(daily, "temperature_2m_min");
                var probs = GetDoubleList(daily, "precipitation_probability_max");

                int count = Math.Min(7, dates.Count);
                for (int i = 0; i < count; i++)
                {
                    DateTime.TryParse(dates[i], CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt);
                    snap.Daily.Add(new DailyWeatherPoint
                    {
                        Date = dt,
                        WeatherCode = i < codes.Count ? (int)Math.Round(codes[i]) : 0,
                        TempMax = i < maxs.Count ? maxs[i] : snap.CurrentTemp,
                        TempMin = i < mins.Count ? mins[i] : snap.CurrentTemp,
                        PrecipProbabilityMax = i < probs.Count ? (int)Math.Round(probs[i]) : 0
                    });
                }

                if (snap.Daily.Count > 0)
                {
                    snap.TempMin = snap.Daily[0].TempMin;
                    snap.TempMax = snap.Daily[0].TempMax;
                }
            }

            _cache[city.Id] = snap;
        }

        public string FormatTemperature(double tempCelsius)
        {
            double val = _settings.Current.WeatherUseCelsius
                ? tempCelsius
                : (tempCelsius * 9.0 / 5.0 + 32.0);
            int rounded = (int)Math.Round(val);
            string sign = rounded > 0 ? "+" : "";
            return $"{sign}{rounded}°";
        }

        public static string DescribeWeatherCode(int code)
        {
            switch (code)
            {
                case 0: return "Ясно";
                case 1: return "Преимущественно ясно";
                case 2: return "Переменная облачность";
                case 3: return "Пасмурно";
                case 45:
                case 48: return "Туман";
                case 51:
                case 53:
                case 55: return "Морось";
                case 56:
                case 57: return "Ледяная морось";
                case 61: return "Небольшой дождь";
                case 63: return "Умеренный дождь";
                case 65: return "Сильный дождь";
                case 66:
                case 67: return "Ледяной дождь";
                case 71: return "Небольшой снег";
                case 73: return "Снегопад";
                case 75: return "Сильный снег";
                case 77: return "Снежные зёрна";
                case 80:
                case 81:
                case 82: return "Ливень";
                case 85:
                case 86: return "Снежный заряд";
                case 95: return "Гроза";
                case 96:
                case 99: return "Гроза с градом";
                default: return "Облачно";
            }
        }

        private void EnsurePrimaryCity()
        {
            if (_cities.Count == 0) return;
            if (!_cities.Any(c => c.IsPrimary))
            {
                _cities[0].IsPrimary = true;
            }
        }

        private void SaveCities()
        {
            StorageService.SaveJson(CitiesFileName, _cities);
        }

        private void SaveCache()
        {
            StorageService.SaveJson(CacheFileName, _cache.Values.ToList());
        }

        private static string GetString(IDictionary<string, object> dict, string key)
        {
            if (dict != null && dict.TryGetValue(key, out object val) && val != null)
                return val.ToString();
            return string.Empty;
        }

        private static double GetDouble(IDictionary<string, object> dict, string key)
        {
            if (dict != null && dict.TryGetValue(key, out object val) && val != null)
            {
                if (val is double d) return d;
                if (val is int i) return i;
                if (val is decimal m) return (double)m;
                if (double.TryParse(val.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double parsed))
                    return parsed;
            }
            return 0.0;
        }

        private static List<string> GetStringList(IDictionary<string, object> dict, string key)
        {
            var res = new List<string>();
            if (dict != null && dict.TryGetValue(key, out object val) && val is IEnumerable list)
            {
                foreach (object o in list)
                {
                    res.Add(o?.ToString() ?? string.Empty);
                }
            }
            return res;
        }

        private static List<double> GetDoubleList(IDictionary<string, object> dict, string key)
        {
            var res = new List<double>();
            if (dict != null && dict.TryGetValue(key, out object val) && val is IEnumerable list)
            {
                foreach (object o in list)
                {
                    if (o == null) { res.Add(0); continue; }
                    if (o is double d) res.Add(d);
                    else if (o is int i) res.Add(i);
                    else if (o is decimal m) res.Add((double)m);
                    else if (double.TryParse(o.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double p)) res.Add(p);
                    else res.Add(0);
                }
            }
            return res;
        }
    }
}
