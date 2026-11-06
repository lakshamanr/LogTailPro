using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LogTailPro.Models;
using LogTailPro.ViewModels;

namespace LogTailPro.Views;

/// <summary>
/// Interaction logic for ErrorTimelineChartView.xaml
/// </summary>
public partial class ErrorTimelineChartView : UserControl
{
    public ErrorTimelineChartView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (DataContext is MainViewModel mainVm)
        {
            ChartCanvas.DataContext = mainVm.SelectedTab?.TimelineChart;
        }
        else if (DataContext is LogTabViewModel tabVm)
        {
            ChartCanvas.DataContext = tabVm.TimelineChart;
        }
        else if (DataContext is ErrorTimelineChartViewModel chartVm)
        {
            ChartCanvas.DataContext = chartVm;
        }
    }
}

/// <summary>
/// Native high-performance vector-rendered chart canvas for the error timeline.
/// Supports stacked severity bars, hover tooltips, drag selection marquee, and keyboard navigation.
/// </summary>
public sealed class ErrorTimelineChartCanvas : FrameworkElement
{
    private ErrorTimelineChartViewModel? ViewModel => DataContext as ErrorTimelineChartViewModel;

    private int _hoveredBucketIndex = -1;
    private int _dragStartIndex = -1;
    private int _dragCurrentIndex = -1;
    private bool _isDragging;

    private static readonly Brush ErrorBrush = new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28));
    private static readonly Brush WarnBrush = new SolidColorBrush(Color.FromRgb(0xF5, 0x7F, 0x17));
    private static readonly Brush InfoBrush = new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xEB));
    private static readonly Brush OtherBrush = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));
    private static readonly Brush SelectedBorderBrush = new SolidColorBrush(Color.FromRgb(0x1D, 0x4E, 0xD8));
    private static readonly Brush MarqueeBrush = new SolidColorBrush(Color.FromArgb(0x40, 0x25, 0x63, 0xEB));
    private static readonly Brush BaselineBrush = new SolidColorBrush(Color.FromRgb(0xC3, 0xCF, 0xE2));
    private static readonly Brush GridLineBrush = new SolidColorBrush(Color.FromArgb(0x40, 0xC3, 0xCF, 0xE2));
    private static readonly Brush TextBrush = new SolidColorBrush(Color.FromRgb(0x5B, 0x6B, 0x82));

    static ErrorTimelineChartCanvas()
    {
        ErrorBrush.Freeze();
        WarnBrush.Freeze();
        InfoBrush.Freeze();
        OtherBrush.Freeze();
        SelectedBorderBrush.Freeze();
        MarqueeBrush.Freeze();
        BaselineBrush.Freeze();
        GridLineBrush.Freeze();
        TextBrush.Freeze();
    }

    public ErrorTimelineChartCanvas()
    {
        ClipToBounds = true;
        ToolTip = new ToolTip { Visibility = Visibility.Collapsed };
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

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        InvalidateVisual();
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
        if (width < 10 || height < 20) return;

        double bottomMargin = 22.0;
        double chartHeight = Math.Max(10, height - bottomMargin - 4);
        double baselineY = chartHeight + 2;

        // Draw baseline
        dc.DrawLine(new Pen(BaselineBrush, 1.0), new Point(0, baselineY), new Point(width, baselineY));

        // Draw background grid lines at 25%, 50%, 75%
        Pen gridPen = new(GridLineBrush, 1.0);
        for (int step = 1; step <= 3; step++)
        {
            double y = baselineY - (chartHeight * step / 4.0);
            dc.DrawLine(gridPen, new Point(0, y), new Point(width, y));
        }

        ErrorTimelineChartViewModel? vm = ViewModel;
        if (vm == null || vm.Buckets.Count == 0)
        {
            FormattedText emptyText = new(
                "No dated log events to display in the chart",
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                12,
                TextBrush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            dc.DrawText(emptyText, new Point((width - emptyText.Width) / 2, (height - emptyText.Height) / 2));
            return;
        }

        int count = vm.Buckets.Count;
        double barWidth = width / count;
        double barGap = Math.Max(1.0, Math.Min(4.0, barWidth * 0.15));
        double actualBarWidth = Math.Max(1.0, barWidth - barGap);

        bool hasFilter = vm.HasTimeRangeFilter;
        Typeface labelFont = new("Segoe UI");
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        int labelStep = Math.Max(1, (int)Math.Ceiling(count / (width / 65.0)));

        for (int i = 0; i < count; i++)
        {
            TimeBucket b = vm.Buckets[i];
            double x = i * barWidth + (barGap / 2.0);

            bool isDimmed = hasFilter && !b.IsSelected;
            double opacity = isDimmed ? 0.35 : 1.0;

            if (b.TotalCount > 0)
            {
                double totalBarH = Math.Max(3.0, chartHeight * b.TotalHeightRatio);
                double currentY = baselineY;

                // Stack order: Other, Info, Warning, Error (Errors on top for visibility)
                double otherH = totalBarH * ((double)b.OtherCount / b.TotalCount);
                double infoH  = totalBarH * ((double)b.InfoCount / b.TotalCount);
                double warnH  = totalBarH * ((double)b.WarningCount / b.TotalCount);
                double errorH = totalBarH * ((double)b.ErrorCount / b.TotalCount);

                // Draw Other
                if (otherH > 0)
                {
                    currentY -= otherH;
                    Brush brush = isDimmed ? MakeDimmed(OtherBrush) : OtherBrush;
                    dc.DrawRectangle(brush, null, new Rect(x, currentY, actualBarWidth, otherH));
                }

                // Draw Info
                if (infoH > 0)
                {
                    currentY -= infoH;
                    Brush brush = isDimmed ? MakeDimmed(InfoBrush) : InfoBrush;
                    dc.DrawRectangle(brush, null, new Rect(x, currentY, actualBarWidth, infoH));
                }

                // Draw Warning
                if (warnH > 0)
                {
                    currentY -= warnH;
                    Brush brush = isDimmed ? MakeDimmed(WarnBrush) : WarnBrush;
                    dc.DrawRectangle(brush, null, new Rect(x, currentY, actualBarWidth, warnH));
                }

                // Draw Error
                if (errorH > 0)
                {
                    currentY -= errorH;
                    Brush brush = isDimmed ? MakeDimmed(ErrorBrush) : ErrorBrush;
                    dc.DrawRectangle(brush, null, new Rect(x, currentY, actualBarWidth, errorH));
                }

                // If selected or hovered, draw highlight border
                if (b.IsSelected)
                {
                    Pen selectPen = new(SelectedBorderBrush, 1.5);
                    dc.DrawRectangle(null, selectPen, new Rect(x - 1, baselineY - totalBarH - 2, actualBarWidth + 2, totalBarH + 2));
                }
            }

            // Draw x-axis tick label
            if (i % labelStep == 0 || i == count - 1)
            {
                FormattedText label = new(
                    b.TimeLabel,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    labelFont,
                    9,
                    TextBrush,
                    dpi);

                double labelX = Math.Min(width - label.Width, Math.Max(0, x + (actualBarWidth / 2.0) - (label.Width / 2.0)));
                dc.DrawText(label, new Point(labelX, baselineY + 4));
            }
        }

        // Draw drag marquee if currently dragging
        if (_isDragging && _dragStartIndex >= 0 && _dragCurrentIndex >= 0)
        {
            int minIdx = Math.Min(_dragStartIndex, _dragCurrentIndex);
            int maxIdx = Math.Max(_dragStartIndex, _dragCurrentIndex);
            double mX = minIdx * barWidth;
            double mW = (maxIdx - minIdx + 1) * barWidth;

            dc.DrawRectangle(MarqueeBrush, new Pen(SelectedBorderBrush, 1.0), new Rect(mX, 2, mW, chartHeight));
        }
    }

    private static Brush MakeDimmed(Brush baseBrush)
    {
        if (baseBrush is SolidColorBrush scb)
        {
            Color c = scb.Color;
            SolidColorBrush dimmed = new(Color.FromArgb((byte)(c.A * 0.35), c.R, c.G, c.B));
            dimmed.Freeze();
            return dimmed;
        }
        return baseBrush;
    }

    private int GetBucketIndexAtX(double x)
    {
        ErrorTimelineChartViewModel? vm = ViewModel;
        if (vm == null || vm.Buckets.Count == 0) return -1;
        double barWidth = ActualWidth / vm.Buckets.Count;
        int idx = (int)(x / barWidth);
        if (idx < 0) idx = 0;
        if (idx >= vm.Buckets.Count) idx = vm.Buckets.Count - 1;
        return idx;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        Point p = e.GetPosition(this);
        int idx = GetBucketIndexAtX(p.X);

        if (_isDragging)
        {
            _dragCurrentIndex = idx;
            InvalidateVisual();
            return;
        }

        if (idx != _hoveredBucketIndex)
        {
            _hoveredBucketIndex = idx;
            ErrorTimelineChartViewModel? vm = ViewModel;
            if (vm != null && idx >= 0 && idx < vm.Buckets.Count)
            {
                ToolTip = vm.Buckets[idx].ToolTipText;
            }
            InvalidateVisual();
        }
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _hoveredBucketIndex = -1;
        if (!_isDragging) InvalidateVisual();
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.LeftButton != MouseButtonState.Pressed) return;

        Focus();
        Point p = e.GetPosition(this);
        int idx = GetBucketIndexAtX(p.X);

        if (e.ClickCount == 2)
        {
            ErrorTimelineChartViewModel? vm = ViewModel;
            if (vm != null && idx >= 0 && idx < vm.Buckets.Count)
            {
                vm.DrillToFirstEvent(vm.Buckets[idx]);
                e.Handled = true;
                return;
            }
        }
        if (idx >= 0)
        {
            _isDragging = true;
            _dragStartIndex = idx;
            _dragCurrentIndex = idx;
            CaptureMouse();
            InvalidateVisual();
        }
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);
        if (!_isDragging) return;

        ReleaseMouseCapture();
        _isDragging = false;

        ErrorTimelineChartViewModel? vm = ViewModel;
        if (vm != null && _dragStartIndex >= 0 && _dragCurrentIndex >= 0 && vm.Buckets.Count > 0)
        {
            int minIdx = Math.Min(_dragStartIndex, _dragCurrentIndex);
            int maxIdx = Math.Max(_dragStartIndex, _dragCurrentIndex);

            if (minIdx == maxIdx)
            {
                // Single bucket clicked
                vm.SelectBucket(vm.Buckets[minIdx]);
            }
            else
            {
                // Range drag selected
                DateTimeOffset start = vm.Buckets[minIdx].StartTime;
                DateTimeOffset end = vm.Buckets[maxIdx].EndTime;
                vm.SelectRange(start, end);
            }
        }

        _dragStartIndex = -1;
        _dragCurrentIndex = -1;
        InvalidateVisual();
    }


    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        ErrorTimelineChartViewModel? vm = ViewModel;
        if (vm == null) return;

        switch (e.Key)
        {
            case Key.Left:
                vm.SelectPreviousBucket();
                e.Handled = true;
                break;
            case Key.Right:
                vm.SelectNextBucket();
                e.Handled = true;
                break;
            case Key.Escape:
                vm.ClearFilter();
                e.Handled = true;
                break;
        }
    }
}
