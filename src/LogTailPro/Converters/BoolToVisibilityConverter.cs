using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace LogTailPro.Converters;

/// <summary>Converts a <see cref="bool"/> to a <see cref="Visibility"/> value.</summary>
[ValueConversion(typeof(bool), typeof(Visibility))]
public sealed class BoolToVisibilityConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool flag = value is bool b && b;
        // Pass "Inverse" as parameter to flip the logic
        if (parameter is string s && s == "Inverse")
            flag = !flag;
        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is Visibility v && v == Visibility.Visible;
    }
}
