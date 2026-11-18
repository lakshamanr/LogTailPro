using System.Windows;
using LogTailPro.ViewModels;

namespace LogTailPro.Views;

/// <summary>Lets the user choose which tabs to show beside the current one and in which orientation.</summary>
public partial class SplitViewDialog : Window
{
    /// <summary>The dialog's view-model.</summary>
    public SplitViewViewModel ViewModel { get; }

    /// <summary>Creates the dialog bound to <paramref name="viewModel"/>.</summary>
    public SplitViewDialog(SplitViewViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;
    }

    private void OK_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedTabs.Count == 0)
        {
            MessageBox.Show(this, "Tick at least one tab to show next to the current one.", "Split View",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        DialogResult = true;
    }
}
