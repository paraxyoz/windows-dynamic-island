using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace WindowsDynamicIsland.UI.Views
{
    internal sealed class WeatherMascot : Canvas
    {
        public WeatherMascot(int code)
        {
            Width = 100;
            Height = 76;
            IsHitTestVisible = false;
            var movement = new TranslateTransform();
            RenderTransform = movement;
            bool sunny = code <= 1;
            bool partlyCloudy = code == 2;
            bool snow = (code >= 71 && code <= 77) || code == 85 || code == 86;
            bool rain = code >= 51 && !snow;

            if (sunny || partlyCloudy)
            {
                var rays = new Canvas { Width = 56, Height = 56 };
                SetLeft(rays, partlyCloudy ? 10 : 22);
                SetTop(rays, 6);
                for (int i = 0; i < 8; i++)
                {
                    double angle = i * Math.PI / 4;
                    rays.Children.Add(new Line
                    {
                        X1 = 28 + 24 * Math.Cos(angle), Y1 = 28 + 24 * Math.Sin(angle),
                        X2 = 28 + 29 * Math.Cos(angle), Y2 = 28 + 29 * Math.Sin(angle),
                        Stroke = Brush("#FFBC67"), StrokeThickness = 3,
                        StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round
                    });
                }
                Children.Add(rays);
                Circle(partlyCloudy ? 16 : 28, 12, 44, Gradient("#FFF4B1", "#FFAB56"));
                if (sunny) Face(50, 32, "#633C30");
            }
            if (!sunny)
            {
                var cloud = new Path
                {
                    Data = Geometry.Parse("M22,55 C12,55 8,49 8,42 C8,34 15,28 23,29 C27,18 35,13 45,13 C56,13 64,19 67,27 C79,25 90,32 90,43 C90,51 85,55 76,55 Z"),
                    Fill = Gradient("#F5F9FF", "#A7C6EA")
                };
                if (partlyCloudy) cloud.RenderTransform = new TranslateTransform(5, 6);
                Children.Add(cloud);
                Face(partlyCloudy ? 56 : 51, partlyCloudy ? 46 : 40, "#334563");
                if (rain || snow)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        double x = 32 + 17 * i;
                        if (snow) Circle(x, 61 + (i % 2) * 4, 4, Brush("#E6F5FF"));
                        else Children.Add(new Line
                        {
                            X1 = x, Y1 = 61 + (i % 2) * 3, X2 = x - 3, Y2 = 69 + (i % 2) * 3,
                            Stroke = Brush("#79BBF9"), StrokeThickness = 3,
                            StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round
                        });
                    }
                }
            }

            IsVisibleChanged += (s, e) =>
            {
                movement.BeginAnimation(TranslateTransform.YProperty, null);
                if (IsVisible && SystemParameters.ClientAreaAnimation)
                    movement.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, -2.2, TimeSpan.FromSeconds(3.5))
                    {
                        AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever,
                        EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                    });
            };
        }

        private void Face(double x, double y, string ink)
        {
            var dark = Brush(ink);
            Circle(x - 10, y - 4, 4, dark);
            Circle(x + 6, y - 4, 4, dark);
            Circle(x - 16, y + 1, 5, Brush("#50F57984"));
            Circle(x + 11, y + 1, 5, Brush("#50F57984"));
            var smile = new Path
            {
                Data = Geometry.Parse("M0,0 Q4,5 8,0"), Stroke = dark, StrokeThickness = 1.8,
                StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round
            };
            SetLeft(smile, x - 4);
            SetTop(smile, y + 1);
            Children.Add(smile);
        }

        private void Circle(double x, double y, double diameter, Brush brush)
        {
            var circle = new Ellipse { Width = diameter, Height = diameter, Fill = brush };
            SetLeft(circle, x);
            SetTop(circle, y);
            Children.Add(circle);
        }

        private static SolidColorBrush Brush(string hex) =>
            new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));

        private static LinearGradientBrush Gradient(string top, string bottom) =>
            new LinearGradientBrush(Brush(top).Color, Brush(bottom).Color, 90);
    }
}
