using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
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
        if (_viewModel != null)
        {
            _viewModel.VisibleLines.CollectionChanged -= OnVisibleLinesChanged;
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.ScrollToLineRequested -= OnScrollToLineRequested;
        }

        _viewModel = e.NewValue as LogTabViewModel;

        if (_viewModel != null)
        {
            _viewModel.VisibleLines.CollectionChanged += OnVisibleLinesChanged;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            _viewModel.ScrollToLineRequested += OnScrollToLineRequested;
        }
    }

    private void OnVisibleLinesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_viewModel?.IsFollowTail == true && e.Action == NotifyCollectionChangedAction.Add)
        {
            Dispatcher.InvokeAsync(() =>
            {
                if (LogListView.Items.Count > 0)
                    LogListView.ScrollIntoView(LogListView.Items[^1]);
            }, DispatcherPriority.Background);
        }
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
}
