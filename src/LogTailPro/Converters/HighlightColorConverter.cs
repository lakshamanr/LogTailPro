using System.Collections.Concurrent;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using LogTailPro.Models;

namespace LogTailPro.Converters;

/// <summary>
/// Converts a <see cref="HighlightRule"/> to a <see cref="Brush"/> for the log list item.
/// Pass "Background" or "Foreground" as <c>ConverterParameter</c>.
/// Returns <see cref="System.Windows.DependencyProperty.UnsetValue"/> when no rule matches,
/// which causes the item to fall through to its default style.
/// </summary>
[ValueConversion(typeof(HighlightRule), typeof(Brush))]
public sealed class HighlightColorConverter : IValueConverter
{
    private static readonly ConcurrentDictionary<string, Brush> BrushCache = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not HighlightRule rule)
            return System.Windows.DependencyProperty.UnsetValue;

        string? colorHex = parameter is string p && p == "Foreground"
            ? rule.ForegroundColor
            : rule.BackgroundColor;

        if (string.IsNullOrWhiteSpace(colorHex))
            return System.Windows.DependencyProperty.UnsetValue;

        colorHex = colorHex.Trim();
        if (!colorHex.StartsWith('#') && (colorHex.Length == 6 || colorHex.Length == 8))
            colorHex = "#" + colorHex;

        if (BrushCache.TryGetValue(colorHex, out Brush? cached))
            return cached;

        try
        {
            Color color = (Color)ColorConverter.ConvertFromString(colorHex);
            SolidColorBrush brush = new(color);
            brush.Freeze();
            BrushCache[colorHex] = brush;
            return brush;
        }
        catch
        {
            return System.Windows.DependencyProperty.UnsetValue;
        }
    }

    /// <inheritdoc/>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
