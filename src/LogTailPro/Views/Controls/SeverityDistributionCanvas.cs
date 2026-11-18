using System.Globalization;
using System.Windows;
using System.Windows.Media;
using LogTailPro.ViewModels;

namespace LogTailPro.Views;

/// <summary>
/// Vector-rendered horizontal proportional bar showing the severity distribution
/// (Error, Warning, Info, Other) for the active log stream.
/// </summary>
public sealed class SeverityDistributionCanvas : FrameworkElement
{
    private ErrorTimelineChartViewModel? ViewModel => DataContext as ErrorTimelineChartViewModel;

    private static readonly Brush ErrorBrush = new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28));
    private static readonly Brush WarnBrush = new SolidColorBrush(Color.FromRgb(0xF5, 0x7F, 0x17));
    private static readonly Brush InfoBrush = new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xEB));
    private static readonly Brush OtherBrush = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));
    private static readonly Brush EmptyBrush = new SolidColorBrush(Color.FromArgb(0x40, 0xC3, 0xCF, 0xE2));

    static SeverityDistributionCanvas()
    {
        ErrorBrush.Freeze();
        WarnBrush.Freeze();
        InfoBrush.Freeze();
        OtherBrush.Freeze();
        EmptyBrush.Freeze();
    }

    public SeverityDistributionCanvas()
    {
        ClipToBounds = true;
        Height = 12;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is ErrorTimelineChartViewModel oldVm)
            oldVm.PropertyChanged -= OnViewModelPropertyChanged;

        if (e.NewValue is ErrorTimelineChartViewModel newVm)
            newVm.PropertyChanged += OnViewModelPropertyChanged;

        InvalidateVisual();
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is null or "" or nameof(ErrorTimelineChartViewModel.TotalEventCount)
            or nameof(ErrorTimelineChartViewModel.ErrorCount)
            or nameof(ErrorTimelineChartViewModel.WarningCount)
            or nameof(ErrorTimelineChartViewModel.InfoCount)
            or nameof(ErrorTimelineChartViewModel.OtherCount))
        {
            InvalidateVisual();
        }
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        double width = ActualWidth;
        double height = ActualHeight;
        if (width < 4 || height < 4) return;

        double radius = Math.Min(height / 2.0, 3.0);
        Rect fullRect = new(0, 0, width, height);

        ErrorTimelineChartViewModel? vm = ViewModel;
        if (vm == null || vm.TotalEventCount <= 0)
        {
            dc.DrawRoundedRectangle(EmptyBrush, null, fullRect, radius, radius);
            return;
        }

        // Clip to rounded rectangle
        RectangleGeometry clipGeom = new(fullRect, radius, radius);
        dc.PushClip(clipGeom);

        double total = vm.TotalEventCount;
        double errW = (vm.ErrorCount / total) * width;
        double warnW = (vm.WarningCount / total) * width;
        double infoW = (vm.InfoCount / total) * width;
        double otherW = width - (errW + warnW + infoW);

        double currentX = 0;

        if (errW > 0)
        {
            dc.DrawRectangle(ErrorBrush, null, new Rect(currentX, 0, errW, height));
            currentX += errW;
        }
        if (warnW > 0)
        {
            dc.DrawRectangle(WarnBrush, null, new Rect(currentX, 0, warnW, height));
            currentX += warnW;
        }
        if (infoW > 0)
        {
            dc.DrawRectangle(InfoBrush, null, new Rect(currentX, 0, infoW, height));
            currentX += infoW;
        }
        if (otherW > 0)
        {
            dc.DrawRectangle(OtherBrush, null, new Rect(currentX, 0, otherW, height));
        }

        dc.Pop();
    }
}
