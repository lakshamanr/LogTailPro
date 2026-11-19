using System.Collections.Concurrent;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace LogTailPro.Converters;

/// <summary>Converts an HTML hex color string (e.g. "#CC2222") to a <see cref="SolidColorBrush"/>.</summary>
[ValueConversion(typeof(string), typeof(SolidColorBrush))]
public sealed class HexToColorConverter : IValueConverter
{
    public static readonly HexToColorConverter Instance = new();
    private static readonly ConcurrentDictionary<string, SolidColorBrush> BrushCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly SolidColorBrush TransparentBrush = Brushes.Transparent;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string hex && !string.IsNullOrWhiteSpace(hex))
        {
            hex = hex.Trim();
            if (!hex.StartsWith('#') && (hex.Length == 6 || hex.Length == 8))
                hex = "#" + hex;

            if (BrushCache.TryGetValue(hex, out SolidColorBrush? cached))
                return cached;

            try
            {
                Color color = (Color)ColorConverter.ConvertFromString(hex);
                SolidColorBrush brush = new(color);
                brush.Freeze();
                BrushCache[hex] = brush;
                return brush;
            }
            catch { }
        }
        return TransparentBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is SolidColorBrush brush)
            return brush.Color.ToString();
        return "#000000";
    }
}
