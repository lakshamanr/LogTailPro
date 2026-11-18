using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LogTailPro.Models;
using LogTailPro.ViewModels;

namespace LogTailPro.Views;

/// <summary>
/// Lightweight vector-rendered mini timeline sparkline for the Stats panel.
/// Visualizes the chronological error and event distribution at a glance.
/// </summary>
public sealed class MiniTimelineSparklineCanvas : FrameworkElement
{
    private ErrorTimelineChartViewModel? ViewModel => DataContext as ErrorTimelineChartViewModel;

    private static readonly Brush ErrorBrush = new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28));
    private static readonly Brush WarnBrush = new SolidColorBrush(Color.FromRgb(0xF5, 0x7F, 0x17));
    private static readonly Brush InfoBrush = new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xEB));
    private static readonly Brush OtherBrush = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));
    private static readonly Brush BaselineBrush = new SolidColorBrush(Color.FromArgb(0x40, 0xC3, 0xCF, 0xE2));
    private static readonly Brush TextBrush = new SolidColorBrush(Color.FromRgb(0x5B, 0x6B, 0x82));
    private static readonly Pen BaselinePen = new(BaselineBrush, 1.0);

    static MiniTimelineSparklineCanvas()
    {
        ErrorBrush.Freeze();
        WarnBrush.Freeze();
        InfoBrush.Freeze();
        OtherBrush.Freeze();
        BaselineBrush.Freeze();
        TextBrush.Freeze();
        BaselinePen.Freeze();
    }

    public MiniTimelineSparklineCanvas()
    {
        ClipToBounds = true;
        Height = 55;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is ErrorTimelineChartViewModel oldVm)
        {
            oldVm.PropertyChanged -= OnViewModelPropertyChanged;
            oldVm.Buckets.CollectionChanged -= OnBucketsCollectionChanged;
        }

        if (e.NewValue is ErrorTimelineChartViewModel newVm)
        {
            newVm.PropertyChanged += OnViewModelPropertyChanged;
            newVm.Buckets.CollectionChanged += OnBucketsCollectionChanged;
        }

        InvalidateVisual();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is null or "" or nameof(ErrorTimelineChartViewModel.Buckets)
            or nameof(ErrorTimelineChartViewModel.HasTimeRangeFilter)
            or nameof(ErrorTimelineChartViewModel.SelectedStartTime)
            or nameof(ErrorTimelineChartViewModel.SelectedEndTime)
            or nameof(ErrorTimelineChartViewModel.ShowErrors)
            or nameof(ErrorTimelineChartViewModel.ShowWarnings)
            or nameof(ErrorTimelineChartViewModel.ShowInfos)
            or nameof(ErrorTimelineChartViewModel.ShowOthers))
        {
            InvalidateVisual();
        }
    }

    private void OnBucketsCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        double width = ActualWidth;
        double height = ActualHeight;
        if (width < 10 || height < 10) return;

        double baselineY = height - 2;
        dc.DrawLine(BaselinePen, new Point(0, baselineY), new Point(width, baselineY));

        ErrorTimelineChartViewModel? vm = ViewModel;
        if (vm == null || vm.Buckets.Count == 0)
        {
            FormattedText emptyText = new(
                "No dated events yet",
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                10,
                TextBrush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            dc.DrawText(emptyText, new Point((width - emptyText.Width) / 2, (height - emptyText.Height) / 2));
            return;
        }

        int count = vm.Buckets.Count;
        double barWidth = width / count;
        double barSpacing = Math.Max(0.2, Math.Min(1.5, barWidth * 0.1));
        double actualBarWidth = Math.Max(1.0, barWidth - barSpacing);
        double maxH = height - 4;

        bool hasFilter = vm.HasTimeRangeFilter;

        for (int i = 0; i < count; i++)
        {
            TimeBucket b = vm.Buckets[i];
            if (b.TotalCount == 0) continue;

            double x = i * barWidth;
            double totalBarH = Math.Max(2.0, maxH * b.TotalHeightRatio);
            double currentY = baselineY;

            double otherH = vm.ShowOthers ? totalBarH * ((double)b.OtherCount / b.TotalCount) : 0;
            double infoH  = vm.ShowInfos  ? totalBarH * ((double)b.InfoCount / b.TotalCount) : 0;
            double warnH  = vm.ShowWarnings ? totalBarH * ((double)b.WarningCount / b.TotalCount) : 0;
            double errorH = vm.ShowErrors ? totalBarH * ((double)b.ErrorCount / b.TotalCount) : 0;

            if (otherH > 0)
            {
                currentY -= otherH;
                dc.DrawRectangle(OtherBrush, null, new Rect(x, currentY, actualBarWidth, otherH));
            }
            if (infoH > 0)
            {
                currentY -= infoH;
                dc.DrawRectangle(InfoBrush, null, new Rect(x, currentY, actualBarWidth, infoH));
            }
            if (warnH > 0)
            {
                currentY -= warnH;
                dc.DrawRectangle(WarnBrush, null, new Rect(x, currentY, actualBarWidth, warnH));
            }
            if (errorH > 0)
            {
                currentY -= errorH;
                dc.DrawRectangle(ErrorBrush, null, new Rect(x, currentY, actualBarWidth, errorH));
            }
        }
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.LeftButton != MouseButtonState.Pressed) return;

        ErrorTimelineChartViewModel? vm = ViewModel;
        if (vm == null || vm.Buckets.Count == 0) return;

        Point p = e.GetPosition(this);
        double barWidth = ActualWidth / vm.Buckets.Count;
        int idx = (int)(p.X / barWidth);
        if (idx < 0) idx = 0;
        if (idx >= vm.Buckets.Count) idx = vm.Buckets.Count - 1;

        if (idx >= 0 && idx < vm.Buckets.Count)
        {
            TimeBucket b = vm.Buckets[idx];
            if (e.ClickCount == 2)
                vm.DrillToFirstEvent(b);
            else
                vm.SelectBucket(b);
        }
    }
}
