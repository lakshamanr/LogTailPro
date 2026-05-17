using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace LogTailPro.Converters;

/// <summary>Returns <see cref="Visibility.Visible"/> when the bound string is non-empty.</summary>
[ValueConversion(typeof(string), typeof(Visibility))]
public sealed class StringNotEmptyToVisibilityConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
