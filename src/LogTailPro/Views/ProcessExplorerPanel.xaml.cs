using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LogTailPro.ViewModels;

namespace LogTailPro.Views;

/// <summary>Process Explorer side panel. Code-behind is limited to keyboard wiring.</summary>
public partial class ProcessExplorerPanel : UserControl
{
    /// <summary>Initialises the panel.</summary>
    public ProcessExplorerPanel()
    {
        InitializeComponent();

        // Make the tree's selection colours follow the active theme instead of the OS accent colour.
        Resources[SystemColors.HighlightBrushKey] = ThemeBrush("BgOverlay");
        Resources[SystemColors.InactiveSelectionHighlightBrushKey] = ThemeBrush("BgOverlay");
        Resources[SystemColors.HighlightTextBrushKey] = ThemeBrush("TextPrimary");
        Resources[SystemColors.InactiveSelectionHighlightTextBrushKey] = ThemeBrush("TextPrimary");
    }

    private static Brush ThemeBrush(string key) => (Brush)Application.Current.FindResource(key);

    private void TreeView_KeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not ProcessExplorerViewModel vm) return;

        if (e.Key == Key.F5)
        {
            _ = vm.RefreshAsync();
            e.Handled = true;
        }
        else if (e.Key == Key.Delete && vm.SelectedProcess != null)
        {
            vm.SelectedProcess.KillProcessCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && vm.SelectedProcess != null)
        {
            vm.SelectedProcess.OpenLogsCommand.Execute(null);
            e.Handled = true;
        }
    }

    /// <summary>
    /// Double-clicking a process opens its log files. Handled in the preview phase so the tree
    /// does not also toggle the node's expansion.
    /// </summary>
    private void TreeView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2 || e.OriginalSource is not DependencyObject source) return;

        TreeViewItem? container = null;
        for (DependencyObject? cur = source; cur != null && cur != sender; cur = VisualTreeHelper.GetParent(cur))
        {
            if (cur is System.Windows.Controls.Primitives.ToggleButton) return; // the expander arrow
            if (cur is TreeViewItem tvi && container == null) container = tvi;
        }

        if (container?.DataContext is ProcessItemViewModel item)
        {
            item.OpenLogsCommand.Execute(null);
            e.Handled = true;
        }
    }
}
