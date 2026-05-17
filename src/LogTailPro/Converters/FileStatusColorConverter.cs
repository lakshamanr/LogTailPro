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
                FileStatus.Watching => new SolidColorBrush(Color.FromRgb(0x4E, 0xC9, 0x4E)), // green
                FileStatus.Paused   => new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0x00)), // yellow
                FileStatus.Error    => new SolidColorBrush(Color.FromRgb(0xFF, 0x80, 0x00)), // orange
                FileStatus.Deleted  => new SolidColorBrush(Color.FromRgb(0xCC, 0x22, 0x22)), // red
                _                   => Brushes.Gray
            }
            : Brushes.Gray;
    }

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
