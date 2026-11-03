using System;
using System.Collections.Generic;

namespace WindowsDynamicIsland.Models
{
    [Serializable]
    public class CityLocation
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "Москва";
        public string Country { get; set; } = "Россия";
        public double Latitude { get; set; } = 55.7558;
        public double Longitude { get; set; } = 37.6173;
        public bool IsPrimary { get; set; }
    }

    [Serializable]
    public class HourlyWeatherPoint
    {
        public DateTime Time { get; set; }
        public double Temperature { get; set; }
        public int WeatherCode { get; set; }
        public int PrecipProbability { get; set; }
    }

    [Serializable]
    public class DailyWeatherPoint
    {
        public DateTime Date { get; set; }
        public int WeatherCode { get; set; }
        public double TempMin { get; set; }
        public double TempMax { get; set; }
        public int PrecipProbabilityMax { get; set; }
    }

    [Serializable]
    public class WeatherSnapshot
    {
        public string CityId { get; set; } = string.Empty;
        public string CityName { get; set; } = string.Empty;
        public double CurrentTemp { get; set; }
        public double FeelsLike { get; set; }
        public int WeatherCode { get; set; }
        public string ConditionDescription { get; set; } = "Ясно";
        public int Humidity { get; set; }
        public double WindSpeedKmh { get; set; }
        public int PrecipProbability { get; set; }
        public double TempMin { get; set; }
        public double TempMax { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        public List<HourlyWeatherPoint> Hourly { get; set; } = new List<HourlyWeatherPoint>();
        public List<DailyWeatherPoint> Daily { get; set; } = new List<DailyWeatherPoint>();
    }
}
