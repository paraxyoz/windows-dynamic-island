using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace WindowsDynamicIsland.UI
{
    /// <summary>
    /// Uniform 1.5px stroke vector line icons (Lucide / Fluent geometry).
    /// Zero OS color emojis used as structural UI icons.
    /// </summary>
    public static class VectorIcons
    {
        public const string Home = "M 3 10 L 12 3 L 21 10 V 20 A 1 1 0 0 1 20 21 H 4 A 1 1 0 0 1 3 20 Z M 9 21 V 14 H 15 V 21";
        public const string Clipboard = "M 9 4 H 15 A 1 1 0 0 1 16 5 V 6 H 8 V 5 A 1 1 0 0 1 9 4 Z M 8 5 H 6 A 2 2 0 0 0 4 7 V 19 A 2 2 0 0 0 6 21 H 18 A 2 2 0 0 0 20 19 V 7 A 2 2 0 0 0 18 5 H 16";
        public const string Shelf = "M 3 7 A 2 2 0 0 1 5 5 H 9 L 11 7 H 19 A 2 2 0 0 1 21 9 V 18 A 2 2 0 0 1 19 20 H 5 A 2 2 0 0 1 3 18 Z";
        public const string Notes = "M 14 3 V 7 A 1 1 0 0 0 15 8 H 19 M 17 21 H 7 A 2 2 0 0 1 5 19 V 5 A 2 2 0 0 1 7 3 H 14 L 19 8 V 19 A 2 2 0 0 1 17 21 Z M 9 13 H 15 M 9 17 H 13";
        public const string Clock = "M 12 21 A 9 9 0 1 1 12 3 A 9 9 0 1 1 12 21 Z M 12 7 V 12 L 15 14";
        public const string Weather = "M 17.5 19 H 9 A 5 5 0 1 1 9.9 9.1 A 6 6 0 0 1 20.8 12.2 A 3.5 3.5 0 0 1 17.5 19 Z";
        public const string Settings = "M 12 15 A 3 3 0 1 0 12 9 A 3 3 0 1 0 12 15 Z M 19.4 15 A 1.65 1.65 0 0 0 19.7 16.8 L 19.8 16.9 A 2 2 0 0 1 16.9 19.8 L 16.8 19.7 A 1.65 1.65 0 0 0 15 19.4 A 1.65 1.65 0 0 0 14 20.9 V 21 A 2 2 0 0 1 10 21 V 20.9 A 1.65 1.65 0 0 0 9 19.4 A 1.65 1.65 0 0 0 7.2 19.7 L 7.1 19.8 A 2 2 0 0 1 4.2 16.9 L 4.3 16.8 A 1.65 1.65 0 0 0 4.6 15 A 1.65 1.65 0 0 0 3.1 14 H 3 A 2 2 0 0 1 3 10 H 3.1 A 1.65 1.65 0 0 0 4.6 9 A 1.65 1.65 0 0 0 4.3 7.2 L 4.2 7.1 A 2 2 0 0 1 7.1 4.2 L 7.2 4.3 A 1.65 1.65 0 0 0 9 4.6 A 1.65 1.65 0 0 0 10 3.1 V 3 A 2 2 0 0 1 14 3 V 3.1 A 1.65 1.65 0 0 0 15 4.6 A 1.65 1.65 0 0 0 16.8 4.3 L 16.9 4.2 A 2 2 0 0 1 19.8 7.1 L 19.7 7.2 A 1.65 1.65 0 0 0 19.4 9 A 1.65 1.65 0 0 0 20.9 10 H 21 A 2 2 0 0 1 21 14 H 20.9 A 1.65 1.65 0 0 0 19.4 15 Z";

        public const string Play = "M 6 4 L 19 12 L 6 20 Z";
        public const string Pause = "M 7 4 H 10 V 20 H 7 Z M 14 4 H 17 V 20 H 14 Z";
        public const string SkipPrev = "M 19 20 L 9 12 L 19 4 Z M 5 19 V 5";
        public const string SkipNext = "M 5 4 L 15 12 L 5 20 Z M 19 5 V 19";
        public const string Music = "M 9 18 V 5 L 21 3 V 16 M 6 21 A 3 3 0 1 0 6 15 A 3 3 0 1 0 6 21 Z M 18 19 A 3 3 0 1 0 18 13 A 3 3 0 1 0 18 19 Z";

        public const string VolumeHigh = "M 11 5 L 6 9 H 2 V 15 H 6 L 11 19 Z M 15.5 8.5 A 5 5 0 0 1 15.5 15.5 M 19 5 A 10 10 0 0 1 19 19";
        public const string VolumeMute = "M 11 5 L 6 9 H 2 V 15 H 6 L 11 19 Z M 22 9 L 16 15 M 16 9 L 22 15";

        public const string Power = "M 12 2 V 12 M 18.4 6.6 A 9 9 0 1 1 5.6 6.6";
        public const string Restart = "M 3 12 A 9 9 0 1 0 12 3 C 9.5 3 7.2 4 5.5 5.5 L 3 8 M 3 3 V 8 H 8";
        public const string Search = "M 11 19 A 8 8 0 1 0 11 3 A 8 8 0 1 0 11 19 Z M 21 21 L 16.65 16.65";
        public const string Keyboard = "M 4 6 H 20 A 2 2 0 0 1 22 8 V 16 A 2 2 0 0 1 20 18 H 4 A 2 2 0 0 1 2 16 V 8 A 2 2 0 0 1 4 6 Z M 6 10 H 6.01 M 10 10 H 10.01 M 14 10 H 14.01 M 18 10 H 18.01 M 8 14 H 16";

        public const string Pin = "M 12 17 V 22 M 5 17 H 19 V 15 L 17 13 V 8 L 18 6 V 4 H 6 V 6 L 7 8 V 13 L 5 15 Z";
        public const string Trash = "M 3 6 H 21 M 19 6 V 20 A 2 2 0 0 1 17 22 H 7 A 2 2 0 0 1 5 20 V 6 M 8 6 V 4 A 2 2 0 0 1 10 2 H 14 A 2 2 0 0 1 16 4 V 6";
        public const string Copy = "M 9 9 H 19 A 2 2 0 0 1 21 11 V 19 A 2 2 0 0 1 19 21 H 9 A 2 2 0 0 1 7 19 V 11 A 2 2 0 0 1 9 9 Z M 5 15 H 4 A 2 2 0 0 1 2 13 V 5 A 2 2 0 0 1 4 3 H 12 A 2 2 0 0 1 14 5 V 6";
        public const string Plus = "M 12 5 V 19 M 5 12 H 19";
        public const string Check = "M 20 6 L 9 17 L 4 12";
        public const string Close = "M 18 6 L 6 18 M 6 6 L 18 18";
        public const string MoreDots = "M 5 12 A 1 1 0 1 0 5 12.1 M 12 12 A 1 1 0 1 0 12 12.1 M 19 12 A 1 1 0 1 0 19 12.1";
        public const string Bell = "M 18 8 A 6 6 0 0 0 6 8 C 6 15 3 17 3 17 H 21 C 21 17 18 15 18 8 Z M 13.73 21 A 2 2 0 0 1 10.27 21";
        public const string Terminal = "M 4 17 L 10 11 L 4 5 M 12 19 H 20";
        public const string Sun = "M 12 17 A 5 5 0 1 0 12 7 A 5 5 0 1 0 12 17 Z M 12 1 V 3 M 12 21 V 23 M 4.22 4.22 L 5.64 5.64 M 18.36 18.36 L 19.78 19.78 M 1 12 H 3 M 21 12 H 23 M 4.22 19.78 L 5.64 18.36 M 18.36 5.64 L 19.78 4.22";
        public const string Rain = "M 16 13 V 21 M 8 13 V 21 M 12 15 V 23 M 20 16.58 A 5 5 0 0 0 18 7 H 16.74 A 8 8 0 1 0 4 15.25";
        public const string Snow = "M 20 17.58 A 5 5 0 0 0 18 8 H 16.74 A 8 8 0 1 0 4 16.25 M 8 16 H 8.01 M 8 20 H 8.01 M 12 18 H 12.01 M 12 22 H 12.01 M 16 16 H 16.01 M 16 20 H 16.01";
        public const string Wind = "M 9.59 4.59 A 2 2 0 1 1 11 8 H 2 M 12.59 19.41 A 2 2 0 1 0 14 16 H 2 M 17.73 7.73 A 2.5 2.5 0 1 1 19.5 12 H 2";

        public static string GetWeatherIconPath(int weatherCode)
        {
            if (weatherCode == 0 || weatherCode == 1) return Sun;
            if (weatherCode >= 51 && weatherCode <= 67) return Rain;
            if (weatherCode >= 80 && weatherCode <= 82) return Rain;
            if (weatherCode >= 71 && weatherCode <= 77) return Snow;
            if (weatherCode >= 85 && weatherCode <= 86) return Snow;
            return Weather;
        }

        public static Path Create(string pathData, Brush strokeBrush, double size = 16, double strokeThickness = 1.6)
        {
            return new Path
            {
                Data = Geometry.Parse(pathData),
                Stroke = strokeBrush,
                StrokeThickness = strokeThickness,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round,
                Stretch = Stretch.Uniform,
                Width = size,
                Height = size,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                SnapsToDevicePixels = true
            };
        }
    }
}
