using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using LogTailPro.Models;

namespace LogTailPro.Converters;

/// <summary>Converts a <see cref="FileStatus"/> value to a status-indicator <see cref="Brush"/>.</summary>
[ValueConversion(typeof(FileStatus), typeof(Brush))]
public sealed class FileStatusColorConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is FileStatus status
            ? status switch
            {
                FileStatus.Watching => new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A)), // green
                FileStatus.Paused   => new SolidColorBrush(Color.FromRgb(0xD9, 0x77, 0x06)), // yellow
                FileStatus.Error    => new SolidColorBrush(Color.FromRgb(0xEA, 0x58, 0x0C)), // orange
                FileStatus.Deleted  => new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26)), // red
                _                   => Brushes.Gray
            }
            : Brushes.Gray;
    }

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
