using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using LogTailPro.Models;
using LogTailPro.ViewModels;

namespace LogTailPro.Views;

/// <summary>
/// Code-behind for LogTabView.
/// Responsibilities: auto-scroll, scroll-to-line, elapsed column toggle, navigation bar drawing.
/// </summary>
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
            _viewModel.NavigationBarInvalidated -= OnNavigationBarInvalidated;
        }

        _viewModel = e.NewValue as LogTabViewModel;

        if (_viewModel != null)
        {
            _viewModel.VisibleLines.CollectionChanged += OnVisibleLinesChanged;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            _viewModel.ScrollToLineRequested += OnScrollToLineRequested;
            _viewModel.NavigationBarInvalidated += OnNavigationBarInvalidated;

            UpdateElapsedColumnWidth(_viewModel.ShowElapsedColumn);
            DrawNavigationBar();
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
        switch (e.PropertyName)
        {
            case nameof(LogTabViewModel.SelectedLogLine):
                if (_viewModel?.SelectedLogLine != null)
                {
                    Dispatcher.InvokeAsync(() =>
                        LogListView.ScrollIntoView(_viewModel.SelectedLogLine),
                        DispatcherPriority.Background);
                }
                break;

            case nameof(LogTabViewModel.ShowElapsedColumn):
                if (_viewModel != null)
                    UpdateElapsedColumnWidth(_viewModel.ShowElapsedColumn);
                break;
        }
    }

    private void OnScrollToLineRequested(object? sender, LogLine line)
    {
        Dispatcher.InvokeAsync(() => LogListView.ScrollIntoView(line),
            DispatcherPriority.Background);
    }

    private void OnNavigationBarInvalidated(object? sender, EventArgs e)
    {
        Dispatcher.InvokeAsync(DrawNavigationBar, DispatcherPriority.Background);
    }

    private void UpdateElapsedColumnWidth(bool visible)
    {
        ElapsedColumn.Width = visible ? 90 : 0;
        if (ElapsedColumn.Header is string)
            ElapsedColumn.Header = visible ? "Elapsed" : null;
    }

    // ── Multi-select copy ─────────────────────────────────────────────────────

    private void LogListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_viewModel != null)
            _viewModel.SelectedLogLines = LogListView.SelectedItems.Cast<LogLine>().ToList();
    }

    // ── Navigation bar ────────────────────────────────────────────────────────

    private void NavCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        Dispatcher.InvokeAsync(DrawNavigationBar, DispatcherPriority.Background);
    }

    private void DrawNavigationBar()
    {
        NavCanvas.Children.Clear();
        if (_viewModel == null) return;

        IReadOnlyList<LogLine> allLines = _viewModel.AllLines;
        if (allLines.Count == 0) return;

        double canvasH = NavCanvas.ActualHeight;
        if (canvasH <= 0) return;

        double totalLines = allLines.Count;

        // Bucket approach for large files: merge adjacent same-color marks
        int maxMarks = 2000;
        int step = Math.Max(1, (int)Math.Ceiling(totalLines / maxMarks));

        for (int i = 0; i < allLines.Count; i += step)
        {
            LogLine line = allLines[i];
            Color color = GetNavColor(line);
            if (color == Colors.Transparent) continue;

            double y = (i / totalLines) * canvasH;
            double h = Math.Max(2.0, (step / totalLines) * canvasH);

            Rectangle rect = new()
            {
                Width = NavCanvas.Width > 0 ? NavCanvas.Width : 28,
                Height = h,
                Fill = new SolidColorBrush(color)
            };
            Canvas.SetTop(rect, y);
            NavCanvas.Children.Add(rect);
        }
    }

    private Color GetNavColor(LogLine line)
    {
        if (line.IsBookmarked) return Colors.DeepSkyBlue;
        if (line.IsSearchMatch) return Colors.LimeGreen;
        if (line.MatchedRule != null)
        {
            if (line.MatchedRule.BackgroundColor is "#CC2222") return Colors.Red;
            if (line.MatchedRule.BackgroundColor is "#FFA500") return Colors.Orange;
        }
        return Colors.Transparent;
    }

    // ── Search results column sort ────────────────────────────────────────────

    private string _sortColumn = string.Empty;
    private ListSortDirection _sortDirection = ListSortDirection.Ascending;

    private void SearchResults_ColumnHeaderClick(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not GridViewColumnHeader header || header.Content == null) return;
        string column = header.Content.ToString() switch
        {
            "Line"       => nameof(SearchResultRow.LineNumber),
            "Timestamp"  => nameof(SearchResultRow.Timestamp),
            "Match text" => nameof(SearchResultRow.Text),
            "Group 1"    => nameof(SearchResultRow.Group1),
            "Group 2"    => nameof(SearchResultRow.Group2),
            "Group 3"    => nameof(SearchResultRow.Group3),
            _ => string.Empty
        };
        if (string.IsNullOrEmpty(column)) return;

        if (_sortColumn == column)
            _sortDirection = _sortDirection == ListSortDirection.Ascending
                ? ListSortDirection.Descending : ListSortDirection.Ascending;
        else
        {
            _sortColumn = column;
            _sortDirection = ListSortDirection.Ascending;
        }

        ICollectionView view = CollectionViewSource.GetDefaultView(SearchResultsListView.ItemsSource);
        view.SortDescriptions.Clear();
        view.SortDescriptions.Add(new SortDescription(_sortColumn, _sortDirection));
    }

    private void NavCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel == null) return;
        IReadOnlyList<LogLine> allLines = _viewModel.AllLines;
        if (allLines.Count == 0) return;

        double canvasH = NavCanvas.ActualHeight;
        if (canvasH <= 0) return;

        double fraction = e.GetPosition(NavCanvas).Y / canvasH;
        int targetIdx = (int)(fraction * allLines.Count);
        targetIdx = Math.Clamp(targetIdx, 0, allLines.Count - 1);

        LogLine target = allLines[targetIdx];

        // Find the visible line with the closest line number
        LogLine? visible = _viewModel.VisibleLines
            .OrderBy(l => Math.Abs(l.LineNumber - target.LineNumber))
            .FirstOrDefault();

        if (visible != null)
        {
            _viewModel.SelectedLogLine = visible;
            Dispatcher.InvokeAsync(() => LogListView.ScrollIntoView(visible),
                DispatcherPriority.Background);
        }
    }
}
