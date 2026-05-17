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
    /// <inheritdoc/>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not HighlightRule rule)
            return System.Windows.DependencyProperty.UnsetValue;

        string colorHex = parameter is string p && p == "Foreground"
            ? rule.ForegroundColor
            : rule.BackgroundColor;

        try
        {
            Color color = (Color)ColorConverter.ConvertFromString(colorHex);
            return new SolidColorBrush(color);
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
