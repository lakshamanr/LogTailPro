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

        if (_viewModel != null)
        {
            _viewModel.VisibleLines.CollectionChanged += OnVisibleLinesChanged;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            _viewModel.ScrollToLineRequested += OnScrollToLineRequested;
            _viewModel.SelectAllRequested += OnSelectAllRequested;

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
