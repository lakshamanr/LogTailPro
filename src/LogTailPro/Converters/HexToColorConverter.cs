using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace LogTailPro.Converters;

/// <summary>Converts an HTML hex color string (e.g. "#CC2222") to a <see cref="SolidColorBrush"/>.</summary>
[ValueConversion(typeof(string), typeof(SolidColorBrush))]
public sealed class HexToColorConverter : IValueConverter
{
    public static readonly HexToColorConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string hex)
        {
            try
            {
                Color color = (Color)ColorConverter.ConvertFromString(hex);
                return new SolidColorBrush(color);
            }
            catch { }
        }
        return new SolidColorBrush(Colors.Transparent);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is SolidColorBrush brush)
            return brush.Color.ToString();
        return "#000000";
    }
}
