using System.Globalization;
using System.Windows.Data;

namespace LogTailPro.Converters;

/// <summary>Converts a <see cref="bool"/> to one of two text strings.</summary>
[ValueConversion(typeof(bool), typeof(string))]
public sealed class BoolToTextConverter : IValueConverter
{
    public string TrueText  { get; set; } = "True";
    public string FalseText { get; set; } = "False";

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && b ? TrueText : FalseText;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value?.ToString() == TrueText;
}
