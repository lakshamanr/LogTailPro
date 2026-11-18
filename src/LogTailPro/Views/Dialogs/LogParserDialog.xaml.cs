using System.Windows;
using LogTailPro.ViewModels;

namespace LogTailPro.Views;

/// <summary>Dialog for selecting and testing a log parser preset.</summary>
public partial class LogParserDialog : Window
{
    public LogParserViewModel ViewModel { get; }
    public bool ClearParser { get; private set; }

    public LogParserDialog(string currentPreset)
    {
        InitializeComponent();
        ViewModel = new LogParserViewModel(currentPreset);
        DataContext = ViewModel;
    }

    private void OK_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        ClearParser = true;
        DialogResult = true;
    }
}
