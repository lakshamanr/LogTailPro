using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LogTailPro.ViewModels;

namespace LogTailPro;

/// <summary>Code-behind for MainWindow — drag-drop (files + tabs), keyboard shortcuts, tab placement.</summary>
public partial class MainWindow : Window
{
    private MainViewModel ViewModel => (MainViewModel)DataContext;

    // Tab drag state
    private LogTabViewModel? _draggedTab;
    private Point _dragStartPoint;

    public MainWindow()
    {
        InitializeComponent();
    }

    // ── File drag-and-drop ────────────────────────────────────────────────────

    private async void Window_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
        foreach (string file in files)
            await ViewModel.OpenFileTabAsync(file);
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    // ── Tab drag-to-reorder ───────────────────────────────────────────────────

    private void TabItem_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
    }

    private void TabItem_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        if (sender is not FrameworkElement fe) return;
        if (fe.DataContext is not LogTabViewModel tab) return;

        Point pos = e.GetPosition(null);
        if (Math.Abs(pos.X - _dragStartPoint.X) > SystemParameters.MinimumHorizontalDragDistance ||
            Math.Abs(pos.Y - _dragStartPoint.Y) > SystemParameters.MinimumVerticalDragDistance)
        {
            _draggedTab = tab;
            DragDrop.DoDragDrop(fe, tab, DragDropEffects.Move);
            _draggedTab = null;
        }
    }

    private void TabItem_Drop(object sender, DragEventArgs e)
    {
        if (_draggedTab == null) return;
        if (sender is not FrameworkElement fe) return;
        if (fe.DataContext is not LogTabViewModel target || target == _draggedTab) return;

        int from = ViewModel.Tabs.IndexOf(_draggedTab);
        int to = ViewModel.Tabs.IndexOf(target);
        if (from >= 0 && to >= 0)
            ViewModel.Tabs.Move(from, to);
    }

    // ── Keyboard ──────────────────────────────────────────────────────────────

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.O && Keyboard.Modifiers == ModifierKeys.Control)
        {
            ViewModel.OpenFileCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.W && Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (ViewModel.SelectedTab != null)
                ViewModel.CloseTabCommand.Execute(ViewModel.SelectedTab);
            e.Handled = true;
            return;
        }

        // F — toggle follow tail (only when not typing in a TextBox)
        if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.None &&
            FocusManager.GetFocusedElement(this) is not TextBox)
        {
            ViewModel.SelectedTab?.ToggleFollowTailCommand.Execute(null);
            e.Handled = true;
        }
    }

    // ── Toolbar ───────────────────────────────────────────────────────────────

    private void ToolBar_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not ToolBar toolbar) return;
        if (toolbar.Template.FindName("OverflowGrid", toolbar) is FrameworkElement overflow)
            overflow.Visibility = Visibility.Collapsed;
        if (toolbar.Template.FindName("MainPanelBorder", toolbar) is FrameworkElement mainPanel)
            mainPanel.Margin = new Thickness(0);
    }

    // ── Window lifecycle ──────────────────────────────────────────────────────

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        ViewModel.Settings.WindowWidth  = ActualWidth;
        ViewModel.Settings.WindowHeight = ActualHeight;
        ViewModel.Settings.WindowMaximized = WindowState == WindowState.Maximized;
        if (WindowState == WindowState.Normal)
        {
            ViewModel.Settings.WindowTop  = Top;
            ViewModel.Settings.WindowLeft = Left;
        }
        ViewModel.SaveSettings();
    }
}
