using System.Windows;
using LogTailPro.ViewModels;

namespace LogTailPro.Views;

/// <summary>
/// Code-behind for the HelpDialog window.
/// </summary>
public partial class HelpDialog : Window
{
    public HelpDialogViewModel ViewModel => (HelpDialogViewModel)DataContext;

    public HelpDialog(string? targetIdOrCategory = null)
    {
        InitializeComponent();
        DataContext = new HelpDialogViewModel(initialTarget: targetIdOrCategory);
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
