using System.Globalization;
using System.Windows.Data;

namespace LogTailPro.Converters;

/// <summary>Formats a numeric line number for display in the gutter column.</summary>
[ValueConversion(typeof(long), typeof(string))]
public sealed class LineNumberConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is long n) return n.ToString("N0", culture);
        if (value is int i) return i.ToString("N0", culture);
        return value?.ToString() ?? string.Empty;
    }

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
