using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace LogTailPro.Converters;

/// <summary>Converts <c>null</c> to <see cref="Visibility.Collapsed"/> and any other value to <see cref="Visibility.Visible"/>.</summary>
[ValueConversion(typeof(object), typeof(Visibility))]
public sealed class NullToVisibilityConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value == null ? Visibility.Collapsed : Visibility.Visible;

    /// <inheritdoc/>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
