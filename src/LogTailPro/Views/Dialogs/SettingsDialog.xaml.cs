using System.Windows;
using LogTailPro.Models;
using LogTailPro.ViewModels;

namespace LogTailPro.Views;

/// <summary>Application settings editor dialog.</summary>
public partial class SettingsDialog : Window
{
    public SettingsViewModel ViewModel { get; }

    public SettingsDialog(AppSettings settings)
    {
        InitializeComponent();
        ViewModel = new SettingsViewModel(settings);
        DataContext = ViewModel;
    }

    private void OK_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
