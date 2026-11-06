using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using LogTailPro.Models;
using LogTailPro.ViewModels;

namespace LogTailPro.Views;

/// <summary>Code-behind for LogTabView — auto-scroll and scroll-to-line.</summary>
public partial class LogTabView : UserControl
{
    private LogTabViewModel? _viewModel;

    public LogTabView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        LogListView.SizeChanged += (_, _) => UpdateWrapLayout();
        LogListView.Loaded += (_, _) => UpdateWrapLayout();
    }

    /// <summary>Width of the content column when line wrap is off (long lines scroll horizontally).</summary>
    private const double NoWrapContentWidth = 4000;

    /// <summary>
    /// Keeps the content column in step with the Wrap toggle. The column is a fixed-width GridViewColumn,
    /// so wrapping needs it narrowed to the visible width (otherwise lines only wrap at 4000 px, i.e. never),
    /// and the horizontal scrollbar is dropped while wrapping.
    /// </summary>
    private void UpdateWrapLayout()
    {
        if (LogListView.View is not GridView grid || grid.Columns.Count == 0) return;

        // GridViewColumn is outside the visual tree, so ancestor bindings cannot
        // reliably resolve the tab's DataContext. Keep column sizing in UI wiring.
        TimelineTimestampColumn.Width = _viewModel?.TimelineTimeWidth ?? 0;
        TimelineSourceColumn.Width = _viewModel?.TimelineSourceWidth ?? 0;

        GridViewColumn content = grid.Columns[^1];
        bool wrap = _viewModel?.IsLineWrapEnabled == true;

        if (!wrap)
        {
            ScrollViewer.SetHorizontalScrollBarVisibility(LogListView, ScrollBarVisibility.Auto);
            if (content.Width != NoWrapContentWidth) content.Width = NoWrapContentWidth;
            return;
        }

        ScrollViewer.SetHorizontalScrollBarVisibility(LogListView, ScrollBarVisibility.Disabled);

        double gutters = 0;
        for (int i = 0; i < grid.Columns.Count - 1; i++)
            gutters += double.IsNaN(grid.Columns[i].Width) ? grid.Columns[i].ActualWidth : grid.Columns[i].Width;

        ScrollViewer? sv = FindScrollViewer(LogListView);
        double available = sv is { ViewportWidth: > 0 }
            ? sv.ViewportWidth
            : LogListView.ActualWidth - SystemParameters.VerticalScrollBarWidth;

        // Leave room for the item padding / border so the text never touches the scrollbar.
        double width = Math.Max(120, available - gutters - 16);
        if (Math.Abs(content.Width - width) > 0.5) content.Width = width;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        // Save scroll position for the outgoing VM.
        // DataContextChanged fires on the UserControl before child bindings (ItemsSource) rebind,
        // so the ScrollViewer still holds the old tab's offset here.
        if (e.OldValue is LogTabViewModel oldVm)
        {
            ScrollViewer? sv = FindScrollViewer(LogListView);
            if (sv != null)
            {
                oldVm.SavedScrollVertical   = sv.VerticalOffset;
                oldVm.SavedScrollHorizontal = sv.HorizontalOffset;
            }

            oldVm.VisibleLines.CollectionChanged -= OnVisibleLinesChanged;
            oldVm.PropertyChanged -= OnViewModelPropertyChanged;
            oldVm.ScrollToLineRequested -= OnScrollToLineRequested;
            oldVm.SelectAllRequested -= OnSelectAllRequested;
        }

        _viewModel = e.NewValue as LogTabViewModel;
        UpdateWrapLayout();

        if (_viewModel != null)
        {
            _viewModel.VisibleLines.CollectionChanged += OnVisibleLinesChanged;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            _viewModel.ScrollToLineRequested += OnScrollToLineRequested;
            _viewModel.SelectAllRequested += OnSelectAllRequested;
            Dispatcher.InvokeAsync(UpdateWrapLayout, DispatcherPriority.Loaded);

            // Restore scroll position after the new ItemsSource has bound and the ListView
            // has completed its first layout pass (Background priority runs after Render).
            double v = _viewModel.SavedScrollVertical;
            double h = _viewModel.SavedScrollHorizontal;
            if (v > 0 || h > 0)
            {
                Dispatcher.InvokeAsync(() =>
                {
                    ScrollViewer? sv = FindScrollViewer(LogListView);
                    sv?.ScrollToVerticalOffset(v);
                    sv?.ScrollToHorizontalOffset(h);
                }, DispatcherPriority.Background);
            }
        }
    }

    private bool _scrollPending;

    private void OnVisibleLinesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Bulk updates arrive as Reset, appends as Add — both must keep the view pinned to the end.
        if (_viewModel?.IsFollowTail != true) return;
        if (e.Action != NotifyCollectionChangedAction.Add && e.Action != NotifyCollectionChangedAction.Reset) return;
        RequestScrollToEnd();
    }

    /// <summary>Scrolls to the last line once per UI turn, however many lines arrived (coalesced).</summary>
    private void RequestScrollToEnd()
    {
        if (_scrollPending) return;
        _scrollPending = true;
        Dispatcher.InvokeAsync(() =>
        {
            _scrollPending = false;
            if (_viewModel?.IsFollowTail == true && LogListView.Items.Count > 0)
                LogListView.ScrollIntoView(LogListView.Items[^1]);
        }, DispatcherPriority.Background);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(LogTabViewModel.IsLineWrapEnabled) or nameof(LogTabViewModel.ElapsedColumnWidth))
            Dispatcher.InvokeAsync(UpdateWrapLayout, DispatcherPriority.Loaded);

        if (e.PropertyName == nameof(LogTabViewModel.SelectedLogLine) &&
            _viewModel?.SelectedLogLine != null)
        {
            Dispatcher.InvokeAsync(() =>
                LogListView.ScrollIntoView(_viewModel.SelectedLogLine),
                DispatcherPriority.Background);
        }
    }

    private void OnScrollToLineRequested(object? sender, LogLine line)
    {
        Dispatcher.InvokeAsync(() => LogListView.ScrollIntoView(line),
            DispatcherPriority.Background);
    }

    private void LogListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_viewModel != null)
            _viewModel.SelectedLogLines = LogListView.SelectedItems.Cast<LogLine>().ToList();
    }

    private void OnSelectAllRequested(object? sender, EventArgs e)
    {
        Dispatcher.InvokeAsync(() => LogListView.SelectAll(), DispatcherPriority.Input);
    }

    private static ScrollViewer? FindScrollViewer(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, i);
            if (child is ScrollViewer sv) return sv;
            ScrollViewer? found = FindScrollViewer(child);
            if (found != null) return found;
        }
        return null;
    }
}
