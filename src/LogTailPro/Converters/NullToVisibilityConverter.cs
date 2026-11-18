using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace LogTailPro.Converters;

/// <summary>Converts <c>null</c> to <see cref="Visibility.Collapsed"/> and any other value to <see cref="Visibility.Visible"/>.</summary>
[ValueConversion(typeof(object), typeof(Visibility))]
public sealed class NullToVisibilityConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool hasValue = value != null && value != DependencyProperty.UnsetValue;
        if (parameter is string s && s == "Inverse")
            hasValue = !hasValue;
        return hasValue ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <inheritdoc/>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
