using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LogTailPro.ViewModels;

namespace LogTailPro.Views;

/// <summary>Dialog listing discovered log files so the user can choose which to open.</summary>
public partial class LogDiscoveryDialog : Window
{
    /// <summary>Creates the dialog bound to <paramref name="viewModel"/>.</summary>
    public LogDiscoveryDialog(LogDiscoveryViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    /// <summary>The ViewModel (for reading <see cref="LogDiscoveryViewModel.SelectedPaths"/>).</summary>
    public LogDiscoveryViewModel ViewModel => (LogDiscoveryViewModel)DataContext;

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedPaths.Count == 0)
        {
            MessageBox.Show(this, "Tick at least one log file to open.", "Open Log Files",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    /// <summary>Double-clicking a row opens just that file.</summary>
    private void List_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListView { SelectedItem: LogCandidateItem item }) return;
        // Ignore double-clicks on the check box column.
        if (e.OriginalSource is DependencyObject d && FindAncestor<CheckBox>(d) != null) return;

        ViewModel.SelectNoneCommand.Execute(null);
        item.IsChecked = true;
        DialogResult = true;
    }

    private static T? FindAncestor<T>(DependencyObject start) where T : DependencyObject
    {
        for (DependencyObject? cur = start; cur != null; cur = System.Windows.Media.VisualTreeHelper.GetParent(cur))
            if (cur is T match) return match;
        return null;
    }
}
