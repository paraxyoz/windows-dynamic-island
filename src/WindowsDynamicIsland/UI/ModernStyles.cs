using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Markup;

namespace WindowsDynamicIsland.UI
{
    /// <summary>
    /// Provides clean, custom WPF ControlTemplates for TextBox, ScrollBar, Slider, CheckBox (Toggle Switch), and ToolTip
    /// using DynamicResource brushes so Light/Dark theme switches update every control with zero legacy chrome.
    /// </summary>
    public static class ModernStyles
    {
        private const string StylesXaml = @"
<ResourceDictionary xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
                    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">

    <SolidColorBrush x:Key=""IslandToolTipBg"" Color=""#FFFFFF""/>
    <SolidColorBrush x:Key=""IslandToolTipFg"" Color=""#202124""/>
    <SolidColorBrush x:Key=""IslandToolTipBorder"" Color=""#DCDCE2""/>

    <SolidColorBrush x:Key=""IslandControlTrackBg"" Color=""#E2E2E7""/>
    <SolidColorBrush x:Key=""IslandControlTrackBorder"" Color=""#D4D4DC""/>
    <SolidColorBrush x:Key=""IslandControlThumbIdle"" Color=""#FFFFFF""/>
    <SolidColorBrush x:Key=""IslandControlActiveBg"" Color=""#2B2B30""/>
    <SolidColorBrush x:Key=""IslandControlThumbActive"" Color=""#FFFFFF""/>
    <SolidColorBrush x:Key=""IslandScrollThumb"" Color=""#C8C8D0""/>

    <!-- Clean Minimal TextBox Template (Prevents Caret offset / glyph overlap) -->
    <Style TargetType=""{x:Type TextBox}"">
        <Setter Property=""SnapsToDevicePixels"" Value=""True""/>
        <Setter Property=""UseLayoutRounding"" Value=""True""/>
        <Setter Property=""FocusVisualStyle"" Value=""{x:Null}""/>
        <Setter Property=""KeyboardNavigation.TabNavigation"" Value=""None""/>
        <Setter Property=""AllowDrop"" Value=""True""/>
        <Setter Property=""ScrollViewer.PanningMode"" Value=""VerticalFirst""/>
        <Setter Property=""Stylus.IsFlicksEnabled"" Value=""False""/>
        <Setter Property=""Template"">
            <Setter.Value>
                <ControlTemplate TargetType=""{x:Type TextBox}"">
                    <Border Background=""{TemplateBinding Background}""
                            BorderBrush=""{TemplateBinding BorderBrush}""
                            BorderThickness=""{TemplateBinding BorderThickness}""
                            SnapsToDevicePixels=""True"">
                        <ScrollViewer x:Name=""PART_ContentHost""
                                      Focusable=""False""
                                      Margin=""0""
                                      Padding=""{TemplateBinding Padding}""
                                      VerticalAlignment=""{TemplateBinding VerticalContentAlignment}""
                                      HorizontalScrollBarVisibility=""Hidden""
                                      VerticalScrollBarVisibility=""Hidden""
                                      Background=""Transparent""/>
                    </Border>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- Premium ToolTip -->
    <Style TargetType=""{x:Type ToolTip}"">
        <Setter Property=""Background"" Value=""{DynamicResource IslandToolTipBg}""/>
        <Setter Property=""Foreground"" Value=""{DynamicResource IslandToolTipFg}""/>
        <Setter Property=""BorderBrush"" Value=""{DynamicResource IslandToolTipBorder}""/>
        <Setter Property=""BorderThickness"" Value=""1""/>
        <Setter Property=""FontFamily"" Value=""Segoe UI Variable Text, Segoe UI""/>
        <Setter Property=""FontSize"" Value=""11.5""/>
        <Setter Property=""Padding"" Value=""8,5,8,5""/>
        <Setter Property=""HasDropShadow"" Value=""True""/>
        <Setter Property=""Template"">
            <Setter.Value>
                <ControlTemplate TargetType=""{x:Type ToolTip}"">
                    <Border Background=""{TemplateBinding Background}""
                            BorderBrush=""{TemplateBinding BorderBrush}""
                            BorderThickness=""{TemplateBinding BorderThickness}""
                            CornerRadius=""6""
                            Padding=""{TemplateBinding Padding}"">
                        <ContentPresenter />
                    </Border>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- Minimal Pill Toggle Switch for CheckBox -->
    <Style TargetType=""{x:Type CheckBox}"">
        <Setter Property=""Width"" Value=""34""/>
        <Setter Property=""Height"" Value=""18""/>
        <Setter Property=""Template"">
            <Setter.Value>
                <ControlTemplate TargetType=""{x:Type CheckBox}"">
                    <Grid Width=""34"" Height=""18"" Background=""Transparent"">
                        <Border x:Name=""Track""
                                CornerRadius=""9""
                                Background=""{DynamicResource IslandControlTrackBg}""
                                BorderBrush=""{DynamicResource IslandControlTrackBorder}""
                                BorderThickness=""1""/>
                        <Ellipse x:Name=""Thumb""
                                 Width=""12"" Height=""12""
                                 Fill=""{DynamicResource IslandControlThumbIdle}""
                                 HorizontalAlignment=""Left""
                                 VerticalAlignment=""Center""
                                 Margin=""3,0,0,0""/>
                    </Grid>
                    <ControlTemplate.Triggers>
                        <Trigger Property=""IsChecked"" Value=""True"">
                            <Setter TargetName=""Track"" Property=""Background"" Value=""{DynamicResource IslandControlActiveBg}""/>
                            <Setter TargetName=""Track"" Property=""BorderBrush"" Value=""{DynamicResource IslandControlActiveBg}""/>
                            <Setter TargetName=""Thumb"" Property=""Fill"" Value=""{DynamicResource IslandControlThumbActive}""/>
                            <Setter TargetName=""Thumb"" Property=""HorizontalAlignment"" Value=""Right""/>
                            <Setter TargetName=""Thumb"" Property=""Margin"" Value=""0,0,3,0""/>
                        </Trigger>
                        <Trigger Property=""IsMouseOver"" Value=""True"">
                            <Setter TargetName=""Track"" Property=""Opacity"" Value=""0.88""/>
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <Style x:Key=""IslandPlayerButton"" TargetType=""{x:Type Button}"">
        <Setter Property=""Cursor"" Value=""Hand""/>
        <Setter Property=""BorderThickness"" Value=""0""/>
        <Setter Property=""HorizontalContentAlignment"" Value=""Center""/>
        <Setter Property=""Template"">
            <Setter.Value>
                <ControlTemplate TargetType=""{x:Type Button}"">
                    <Grid x:Name=""Surface"" Background=""Transparent"">
                        <Border CornerRadius=""9"" Background=""{TemplateBinding Background}""/>
                        <Border x:Name=""Glow"" CornerRadius=""9"" Background=""White"" Opacity=""0""/>
                        <Border Padding=""{TemplateBinding Padding}"">
                            <ContentPresenter HorizontalAlignment=""{TemplateBinding HorizontalContentAlignment}"" VerticalAlignment=""Center""/>
                        </Border>
                    </Grid>
                    <ControlTemplate.Triggers>
                        <Trigger Property=""IsMouseOver"" Value=""True"">
                            <Trigger.EnterActions>
                                <BeginStoryboard><Storyboard>
                                    <DoubleAnimation Storyboard.TargetName=""Glow"" Storyboard.TargetProperty=""Opacity""
                                                     To=""0.085"" Duration=""0:0:0.15""/>
                                </Storyboard></BeginStoryboard>
                            </Trigger.EnterActions>
                            <Trigger.ExitActions>
                                <BeginStoryboard><Storyboard>
                                    <DoubleAnimation Storyboard.TargetName=""Glow"" Storyboard.TargetProperty=""Opacity""
                                                     To=""0"" Duration=""0:0:0.18""/>
                                </Storyboard></BeginStoryboard>
                            </Trigger.ExitActions>
                        </Trigger>
                        <Trigger Property=""IsPressed"" Value=""True"">
                            <Setter TargetName=""Surface"" Property=""Opacity"" Value=""0.77""/>
                        </Trigger>
                        <Trigger Property=""IsEnabled"" Value=""False"">
                            <Setter TargetName=""Surface"" Property=""Opacity"" Value=""0.48""/>
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- Minimal Slim Slider -->
    <Style TargetType=""{x:Type Slider}"">
        <Setter Property=""Height"" Value=""18""/>
        <Setter Property=""FocusVisualStyle"" Value=""{x:Null}""/>
        <Setter Property=""Template"">
            <Setter.Value>
                <ControlTemplate TargetType=""{x:Type Slider}"">
                    <Grid Background=""Transparent"" VerticalAlignment=""Center"">
                        <Border Height=""4""
                                CornerRadius=""2""
                                Background=""{DynamicResource IslandControlTrackBg}""
                                VerticalAlignment=""Center""/>
                        <Track x:Name=""PART_Track"">
                            <Track.DecreaseRepeatButton>
                                <RepeatButton Command=""Slider.DecreaseLarge"">
                                    <RepeatButton.Template>
                                        <ControlTemplate TargetType=""RepeatButton"">
                                            <Border Height=""4"" CornerRadius=""2"" Background=""{DynamicResource IslandControlActiveBg}"" VerticalAlignment=""Center""/>
                                        </ControlTemplate>
                                    </RepeatButton.Template>
                                </RepeatButton>
                            </Track.DecreaseRepeatButton>
                            <Track.IncreaseRepeatButton>
                                <RepeatButton Command=""Slider.IncreaseLarge"">
                                    <RepeatButton.Template>
                                        <ControlTemplate TargetType=""RepeatButton"">
                                            <Border Height=""4"" Background=""Transparent"" VerticalAlignment=""Center""/>
                                        </ControlTemplate>
                                    </RepeatButton.Template>
                                </RepeatButton>
                            </Track.IncreaseRepeatButton>
                            <Track.Thumb>
                                <Thumb>
                                    <Thumb.Template>
                                        <ControlTemplate TargetType=""Thumb"">
                                            <Ellipse Width=""13"" Height=""13""
                                                     Fill=""{DynamicResource IslandControlThumbActive}""
                                                     Stroke=""{DynamicResource IslandControlActiveBg}""
                                                     StrokeThickness=""2""/>
                                        </ControlTemplate>
                                    </Thumb.Template>
                                </Thumb>
                            </Track.Thumb>
                        </Track>
                    </Grid>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- Ultra-thin Minimal ScrollBar -->
    <Style TargetType=""{x:Type ScrollBar}"">
        <Setter Property=""Background"" Value=""Transparent""/>
        <Setter Property=""Width"" Value=""5""/>
        <Setter Property=""Template"">
            <Setter.Value>
                <ControlTemplate TargetType=""{x:Type ScrollBar}"">
                    <Grid Background=""Transparent"" Width=""5"">
                        <Track x:Name=""PART_Track"" IsDirectionReversed=""true"">
                            <Track.Thumb>
                                <Thumb>
                                    <Thumb.Template>
                                        <ControlTemplate TargetType=""Thumb"">
                                            <Border Background=""{DynamicResource IslandScrollThumb}"" CornerRadius=""2.5"" Margin=""0.5""/>
                                        </ControlTemplate>
                                    </Thumb.Template>
                                </Thumb>
                            </Track.Thumb>
                        </Track>
                    </Grid>
                    <ControlTemplate.Triggers>
                        <Trigger Property=""Orientation"" Value=""Horizontal"">
                            <Setter Property=""Width"" Value=""Auto""/>
                            <Setter Property=""Height"" Value=""5""/>
                            <Setter Property=""Template"">
                                <Setter.Value>
                                    <ControlTemplate TargetType=""{x:Type ScrollBar}"">
                                        <Grid Background=""Transparent"" Height=""5"">
                                            <Track x:Name=""PART_Track"" IsDirectionReversed=""False"">
                                                <Track.Thumb>
                                                    <Thumb>
                                                        <Thumb.Template>
                                                            <ControlTemplate TargetType=""Thumb"">
                                                                <Border Background=""{DynamicResource IslandScrollThumb}"" CornerRadius=""2.5"" Margin=""0.5""/>
                                                            </ControlTemplate>
                                                        </Thumb.Template>
                                                    </Thumb>
                                                </Track.Thumb>
                                            </Track>
                                        </Grid>
                                    </ControlTemplate>
                                </Setter.Value>
                            </Setter>
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>
</ResourceDictionary>";

        public static void ApplyToApplication(Application app)
        {
            if (app == null) return;
            try
            {
                using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(StylesXaml)))
                {
                    if (XamlReader.Load(ms) is ResourceDictionary dict)
                    {
                        app.Resources.MergedDictionaries.Add(dict);
                    }
                }
            }
            catch
            {
            }
        }

        public static void UpdateThemeResources(ThemePalette palette)
        {
            var app = Application.Current;
            if (app == null || palette == null) return;

            if (palette.IsGlass)
            {
                app.Resources["IslandToolTipBg"] = ThemeTokens.MakeBrush(225, 22, 28, 42);
                app.Resources["IslandToolTipFg"] = ThemeTokens.MakeBrush(255, 248, 250, 255);
                app.Resources["IslandToolTipBorder"] = ThemeTokens.MakeBrush(140, 255, 255, 255);

                app.Resources["IslandControlTrackBg"] = ThemeTokens.MakeBrush(52, 255, 255, 255);
                app.Resources["IslandControlTrackBorder"] = ThemeTokens.MakeBrush(110, 255, 255, 255);
                app.Resources["IslandControlThumbIdle"] = ThemeTokens.MakeBrush(215, 255, 255, 255);
                app.Resources["IslandControlActiveBg"] = palette.Accent;
                app.Resources["IslandControlThumbActive"] = ThemeTokens.MakeBrush(255, 24, 30, 44);
                app.Resources["IslandScrollThumb"] = ThemeTokens.MakeBrush(95, 255, 255, 255);
            }
            else if (palette.IsLight)
            {
                app.Resources["IslandToolTipBg"] = ThemeTokens.MakeBrush(255, 255, 255, 255);
                app.Resources["IslandToolTipFg"] = ThemeTokens.MakeBrush(255, 32, 33, 36);
                app.Resources["IslandToolTipBorder"] = ThemeTokens.MakeBrush(255, 220, 220, 226);

                app.Resources["IslandControlTrackBg"] = ThemeTokens.MakeBrush(255, 224, 224, 230);
                app.Resources["IslandControlTrackBorder"] = ThemeTokens.MakeBrush(255, 210, 210, 218);
                app.Resources["IslandControlThumbIdle"] = ThemeTokens.MakeBrush(255, 255, 255, 255);
                app.Resources["IslandControlActiveBg"] = ThemeTokens.MakeBrush(255, 43, 43, 48);
                app.Resources["IslandControlThumbActive"] = ThemeTokens.MakeBrush(255, 255, 255, 255);
                app.Resources["IslandScrollThumb"] = ThemeTokens.MakeBrush(255, 198, 198, 206);
            }
            else
            {
                app.Resources["IslandToolTipBg"] = ThemeTokens.MakeBrush(255, 10, 10, 13);
                app.Resources["IslandToolTipFg"] = ThemeTokens.MakeBrush(255, 232, 232, 237);
                app.Resources["IslandToolTipBorder"] = ThemeTokens.MakeBrush(255, 36, 36, 44);

                app.Resources["IslandControlTrackBg"] = ThemeTokens.MakeBrush(255, 24, 24, 30);
                app.Resources["IslandControlTrackBorder"] = ThemeTokens.MakeBrush(255, 42, 42, 50);
                app.Resources["IslandControlThumbIdle"] = ThemeTokens.MakeBrush(255, 130, 130, 140);
                app.Resources["IslandControlActiveBg"] = ThemeTokens.MakeBrush(255, 198, 198, 208);
                app.Resources["IslandControlThumbActive"] = ThemeTokens.MakeBrush(255, 12, 12, 15);
                app.Resources["IslandScrollThumb"] = ThemeTokens.MakeBrush(255, 56, 56, 66);
            }
        }
    }
}
