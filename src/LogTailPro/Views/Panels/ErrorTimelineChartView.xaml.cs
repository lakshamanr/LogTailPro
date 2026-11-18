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
/// Interaction logic for ErrorTimelineChartView.xaml
/// </summary>
public partial class ErrorTimelineChartView : UserControl, INotifyPropertyChanged
{
    private MainViewModel? _hookedMainVm;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ErrorTimelineChartViewModel? ActiveChartViewModel
    {
        get
        {
            if (DataContext is MainViewModel mainVm)
                return mainVm.SelectedTab?.TimelineChart;
            if (DataContext is LogTabViewModel tabVm)
                return tabVm.TimelineChart;
            if (DataContext is ErrorTimelineChartViewModel chartVm)
                return chartVm;
            return null;
        }
    }

    public ErrorTimelineChartCanvas Canvas => ChartCanvas;

    public ErrorTimelineChartView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_hookedMainVm != null)
        {
            _hookedMainVm.PropertyChanged -= OnMainViewModelPropertyChanged;
            _hookedMainVm = null;
        }

        if (DataContext is MainViewModel mainVm)
        {
            _hookedMainVm = mainVm;
            _hookedMainVm.PropertyChanged += OnMainViewModelPropertyChanged;
        }

        UpdateActiveChart();
    }

    private void OnMainViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.SelectedTab))
        {
            UpdateActiveChart();
        }
    }

    private void UpdateActiveChart()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ActiveChartViewModel)));
        ChartCanvas.DataContext = ActiveChartViewModel;
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

    private static readonly Brush DimmedErrorBrush = MakeDimmed(ErrorBrush);
    private static readonly Brush DimmedWarnBrush = MakeDimmed(WarnBrush);
    private static readonly Brush DimmedInfoBrush = MakeDimmed(InfoBrush);
    private static readonly Brush DimmedOtherBrush = MakeDimmed(OtherBrush);

    private static readonly Pen BaselinePen = new(BaselineBrush, 1.0);
    private static readonly Pen GridPen = new(GridLineBrush, 1.0);
    private static readonly Pen SelectedBorderPen = new(SelectedBorderBrush, 1.5);
    private static readonly Pen MarqueePen = new(SelectedBorderBrush, 1.0);

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

        BaselinePen.Freeze();
        GridPen.Freeze();
        SelectedBorderPen.Freeze();
        MarqueePen.Freeze();
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
        if (width < 10 || height < 20) return;

        double bottomMargin = 22.0;
        double chartHeight = Math.Max(10, height - bottomMargin - 4);
        double baselineY = chartHeight + 2;
        double leftMargin = 38.0;
        double availableWidth = Math.Max(10, width - leftMargin);
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        Typeface labelFont = new("Segoe UI");

        // Draw baseline from leftMargin to width
        dc.DrawLine(BaselinePen, new Point(leftMargin, baselineY), new Point(width, baselineY));

        // Draw background grid lines at 25%, 50%, 75%
        for (int step = 1; step <= 3; step++)
        {
            double y = baselineY - (chartHeight * step / 4.0);
            dc.DrawLine(GridPen, new Point(leftMargin, y), new Point(width, y));
        }

        ErrorTimelineChartViewModel? vm = ViewModel;
        if (vm == null || vm.Buckets.Count == 0)
        {
            FormattedText emptyText = new(
                "No dated log events to display in the chart",
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                labelFont,
                12,
                TextBrush,
                dpi);

            dc.DrawText(emptyText, new Point((width - emptyText.Width) / 2, (height - emptyText.Height) / 2));
            return;
        }

        // Draw Y-axis scale labels along leftMargin
        int maxTotal = vm.Buckets[0].MaxBucketTotal;
        if (maxTotal > 0)
        {
            FormattedText zeroLabel = new("0", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, labelFont, 9, TextBrush, dpi);
            dc.DrawText(zeroLabel, new Point(Math.Max(0, leftMargin - zeroLabel.Width - 6), baselineY - (zeroLabel.Height / 2.0)));

            for (int step = 1; step <= 3; step++)
            {
                double y = baselineY - (chartHeight * step / 4.0);
                int val = (int)Math.Round(maxTotal * step / 4.0);
                string txt = val >= 1000 ? $"{val / 1000.0:0.#}k" : val.ToString("N0", CultureInfo.InvariantCulture);
                FormattedText stepLabel = new(txt, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, labelFont, 9, TextBrush, dpi);
                dc.DrawText(stepLabel, new Point(Math.Max(0, leftMargin - stepLabel.Width - 6), y - (stepLabel.Height / 2.0)));
            }

            // Top peak label
            double topY = baselineY - chartHeight;
            string topTxt = maxTotal >= 1000 ? $"{maxTotal / 1000.0:0.#}k" : maxTotal.ToString("N0", CultureInfo.InvariantCulture);
            FormattedText topLabel = new(topTxt, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, labelFont, 9, TextBrush, dpi);
            dc.DrawText(topLabel, new Point(Math.Max(0, leftMargin - topLabel.Width - 6), topY - (topLabel.Height / 2.0)));
        }

        int count = vm.Buckets.Count;
        double barWidth = availableWidth / count;
        double barSpacing = Math.Max(0.5, Math.Min(2.0, barWidth * 0.1));
        double actualBarWidth = Math.Max(1.0, barWidth - barSpacing);

        bool hasFilter = vm.HasTimeRangeFilter;
        bool showOther = vm.ShowOthers;
        bool showInfo = vm.ShowInfos;
        bool showWarn = vm.ShowWarnings;
        bool showError = vm.ShowErrors;

        // Determine sensible step for drawing x-axis labels (minimum 65px per label)
        int labelStep = Math.Max(1, (int)Math.Ceiling(65.0 / barWidth));

        for (int i = 0; i < count; i++)
        {
            TimeBucket b = vm.Buckets[i];
            double x = leftMargin + (i * barWidth);

            bool isDimmed = hasFilter && !b.IsSelected;

            if (b.TotalCount > 0)
            {
                double totalBarH = Math.Max(3.0, chartHeight * b.TotalHeightRatio);
                double currentY = baselineY;

                // Stack order: Other, Info, Warning, Error (Errors on top for visibility)
                double otherH = showOther ? totalBarH * ((double)b.OtherCount / b.TotalCount) : 0;
                double infoH  = showInfo  ? totalBarH * ((double)b.InfoCount / b.TotalCount) : 0;
                double warnH  = showWarn  ? totalBarH * ((double)b.WarningCount / b.TotalCount) : 0;
                double errorH = showError ? totalBarH * ((double)b.ErrorCount / b.TotalCount) : 0;

                // Draw Other
                if (otherH > 0)
                {
                    currentY -= otherH;
                    Brush brush = isDimmed ? DimmedOtherBrush : OtherBrush;
                    dc.DrawRectangle(brush, null, new Rect(x, currentY, actualBarWidth, otherH));
                }

                // Draw Info
                if (infoH > 0)
                {
                    currentY -= infoH;
                    Brush brush = isDimmed ? DimmedInfoBrush : InfoBrush;
                    dc.DrawRectangle(brush, null, new Rect(x, currentY, actualBarWidth, infoH));
                }

                // Draw Warning
                if (warnH > 0)
                {
                    currentY -= warnH;
                    Brush brush = isDimmed ? DimmedWarnBrush : WarnBrush;
                    dc.DrawRectangle(brush, null, new Rect(x, currentY, actualBarWidth, warnH));
                }

                // Draw Error
                if (errorH > 0)
                {
                    currentY -= errorH;
                    Brush brush = isDimmed ? DimmedErrorBrush : ErrorBrush;
                    dc.DrawRectangle(brush, null, new Rect(x, currentY, actualBarWidth, errorH));
                }

                // If selected, draw highlight border
                if (b.IsSelected)
                {
                    dc.DrawRectangle(null, SelectedBorderPen, new Rect(x - 1, baselineY - totalBarH - 2, actualBarWidth + 2, totalBarH + 2));
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

                double labelX = Math.Min(width - label.Width, Math.Max(leftMargin, x + (actualBarWidth / 2.0) - (label.Width / 2.0)));
                dc.DrawText(label, new Point(labelX, baselineY + 4));
            }
        }

        // Draw drag marquee if currently dragging
        if (_isDragging && _dragStartIndex >= 0 && _dragCurrentIndex >= 0)
        {
            int minIdx = Math.Min(_dragStartIndex, _dragCurrentIndex);
            int maxIdx = Math.Max(_dragStartIndex, _dragCurrentIndex);
            double mX = leftMargin + (minIdx * barWidth);
            double mW = (maxIdx - minIdx + 1) * barWidth;

            dc.DrawRectangle(MarqueeBrush, MarqueePen, new Rect(mX, 2, mW, chartHeight));
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
        double leftMargin = 38.0;
        double availableWidth = ActualWidth - leftMargin;
        if (availableWidth <= 0) return -1;
        double relX = x - leftMargin;
        if (relX < 0) relX = 0;
        double barWidth = availableWidth / vm.Buckets.Count;
        int idx = (int)(relX / barWidth);
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
                UpdateRichToolTip(vm.Buckets[idx]);
            }
        }
    }

    private void UpdateRichToolTip(TimeBucket bucket)
    {
        ToolTip tip = new()
        {
            Background = Application.Current.TryFindResource("BgElevated") as Brush ?? Brushes.White,
            BorderBrush = Application.Current.TryFindResource("BorderNormal") as Brush ?? Brushes.Gray,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10, 8, 10, 8),
            Foreground = Application.Current.TryFindResource("TextPrimary") as Brush ?? Brushes.Black
        };

        StackPanel sp = new() { Orientation = Orientation.Vertical };

        // Header: Time Range
        string sTime = bucket.StartTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        string eTime = bucket.EndTime.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        TextBlock header = new()
        {
            Text = $"⏱  {sTime} – {eTime}",
            FontWeight = FontWeights.SemiBold,
            FontSize = 11,
            Foreground = Application.Current.TryFindResource("TextPrimary") as Brush ?? Brushes.Black,
            Margin = new Thickness(0, 0, 0, 6)
        };
        sp.Children.Add(header);

        // Grid with 4 rows for severities
        Grid grid = new() { Margin = new Thickness(0, 0, 0, 6) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(85) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        void AddRow(int row, Brush dotBrush, string label, int count, Brush fgBrush, bool isBold = false)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            System.Windows.Shapes.Ellipse dot = new()
            {
                Width = 8,
                Height = 8,
                Fill = dotBrush,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            Grid.SetRow(dot, row);
            Grid.SetColumn(dot, 0);
            grid.Children.Add(dot);

            TextBlock lbl = new()
            {
                Text = label,
                FontSize = 11,
                Foreground = Application.Current.TryFindResource("TextSecondary") as Brush ?? Brushes.DarkGray,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(lbl, row);
            Grid.SetColumn(lbl, 1);
            grid.Children.Add(lbl);

            TextBlock val = new()
            {
                Text = count.ToString("N0", CultureInfo.InvariantCulture),
                FontSize = 11,
                FontWeight = isBold ? FontWeights.Bold : FontWeights.SemiBold,
                Foreground = fgBrush,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(val, row);
            Grid.SetColumn(val, 2);
            grid.Children.Add(val);
        }

        Brush statusError = Application.Current.TryFindResource("StatusError") as Brush ?? ErrorBrush;
        Brush statusWarn = Application.Current.TryFindResource("StatusWarn") as Brush ?? WarnBrush;
        Brush accentBrush = Application.Current.TryFindResource("Accent") as Brush ?? InfoBrush;
        Brush textSec = Application.Current.TryFindResource("TextSecondary") as Brush ?? OtherBrush;

        AddRow(0, ErrorBrush, "Errors:", bucket.ErrorCount, statusError, bucket.ErrorCount > 0);
        AddRow(1, WarnBrush, "Warnings:", bucket.WarningCount, statusWarn);
        AddRow(2, InfoBrush, "Information:", bucket.InfoCount, accentBrush);
        AddRow(3, OtherBrush, "Other:", bucket.OtherCount, textSec);

        sp.Children.Add(grid);

        // Separator
        sp.Children.Add(new Separator { Margin = new Thickness(0, 2, 0, 4), Background = Application.Current.TryFindResource("BorderSubtle") as Brush ?? Brushes.LightGray });

        // Total
        DockPanel totalPanel = new() { LastChildFill = false, Margin = new Thickness(0, 0, 0, 4) };
        TextBlock totalLbl = new() { Text = "Total Events:", FontSize = 11, FontWeight = FontWeights.Bold, Foreground = Application.Current.TryFindResource("TextPrimary") as Brush ?? Brushes.Black };
        TextBlock totalVal = new() { Text = bucket.TotalCount.ToString("N0", CultureInfo.InvariantCulture), FontSize = 11, FontWeight = FontWeights.Bold, Foreground = Application.Current.TryFindResource("TextPrimary") as Brush ?? Brushes.Black };
        DockPanel.SetDock(totalLbl, Dock.Left);
        DockPanel.SetDock(totalVal, Dock.Right);
        totalPanel.Children.Add(totalLbl);
        totalPanel.Children.Add(totalVal);
        sp.Children.Add(totalPanel);

        // Footer tip
        TextBlock tipText = new()
        {
            Text = "Click to filter • Double-click to jump to first event",
            FontSize = 9.5,
            FontStyle = FontStyles.Italic,
            Foreground = Application.Current.TryFindResource("TextMuted") as Brush ?? Brushes.Gray
        };
        sp.Children.Add(tipText);

        tip.Content = sp;
        ToolTip = tip;
    }

    protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseRightButtonUp(e);
        ErrorTimelineChartViewModel? vm = ViewModel;
        if (vm == null) return;

        Point p = e.GetPosition(this);
        int idx = GetBucketIndexAtX(p.X);
        TimeBucket? bucket = (idx >= 0 && idx < vm.Buckets.Count) ? vm.Buckets[idx] : null;

        ContextMenu cm = new();

        if (bucket != null)
        {
            string sTime = bucket.StartTime.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture);
            string eTime = bucket.EndTime.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture);

            MenuItem headerItem = new()
            {
                Header = $"⏱ {sTime} – {eTime} ({bucket.TotalCount:N0} events)",
                IsEnabled = false,
                FontWeight = FontWeights.SemiBold
            };
            cm.Items.Add(headerItem);
            cm.Items.Add(new Separator());

            MenuItem filterItem = new() { Header = "🔍 Filter to this time bucket" };
            filterItem.Click += (_, _) => vm.SelectBucket(bucket);
            cm.Items.Add(filterItem);

            MenuItem drillItem = new() { Header = "⏱ Drill down to first event" };
            drillItem.Click += (_, _) => vm.DrillToFirstEvent(bucket);
            cm.Items.Add(drillItem);

            MenuItem copyItem = new() { Header = "📋 Copy time range" };
            copyItem.Click += (_, _) =>
            {
                string fullS = bucket.StartTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                string fullE = bucket.EndTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                Clipboard.SetText($"{fullS} – {fullE}");
            };
            cm.Items.Add(copyItem);
        }

        if (vm.HasTimeRangeFilter)
        {
            if (cm.Items.Count > 0) cm.Items.Add(new Separator());
            MenuItem clearFilter = new() { Header = "✕ Clear time filter" };
            clearFilter.Click += (_, _) => vm.ClearFilter();
            cm.Items.Add(clearFilter);
        }

        if (cm.Items.Count > 0) cm.Items.Add(new Separator());

        MenuItem layersMenu = new() { Header = "👁 Toggle Severity Layers" };

        MenuItem errToggle = new() { Header = "Show Errors", IsCheckable = true, IsChecked = vm.ShowErrors };
        errToggle.Click += (_, _) => vm.ToggleShowErrors();
        layersMenu.Items.Add(errToggle);

        MenuItem warnToggle = new() { Header = "Show Warnings", IsCheckable = true, IsChecked = vm.ShowWarnings };
        warnToggle.Click += (_, _) => vm.ToggleShowWarnings();
        layersMenu.Items.Add(warnToggle);

        MenuItem infoToggle = new() { Header = "Show Information", IsCheckable = true, IsChecked = vm.ShowInfos };
        infoToggle.Click += (_, _) => vm.ToggleShowInfos();
        layersMenu.Items.Add(infoToggle);

        MenuItem otherToggle = new() { Header = "Show Other", IsCheckable = true, IsChecked = vm.ShowOthers };
        otherToggle.Click += (_, _) => vm.ToggleShowOthers();
        layersMenu.Items.Add(otherToggle);

        layersMenu.Items.Add(new Separator());
        MenuItem resetLayers = new() { Header = "Show All Layers" };
        resetLayers.Click += (_, _) => vm.ResetLayers();
        layersMenu.Items.Add(resetLayers);

        cm.Items.Add(layersMenu);

        cm.PlacementTarget = this;
        cm.IsOpen = true;
        e.Handled = true;
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _hoveredBucketIndex = -1;
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
