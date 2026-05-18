using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace LogTailPro.Converters;

/// <summary>Converts a bool to <see cref="TextWrapping"/>: True → Wrap, False → NoWrap.</summary>
[ValueConversion(typeof(bool), typeof(TextWrapping))]
public sealed class BoolToWrapConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? TextWrapping.Wrap : TextWrapping.NoWrap;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is TextWrapping.Wrap;
}
