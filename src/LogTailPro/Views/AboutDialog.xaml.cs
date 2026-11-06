using System.Diagnostics;
using System.Windows;

namespace LogTailPro.Views;

/// <summary>
/// Code-behind for the AboutDialog window.
/// </summary>
public partial class AboutDialog : Window
{
    public AboutDialog()
    {
        InitializeComponent();
    }

    private void GitHub_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://github.com/lakshamanr/LogTailPro") { UseShellExecute = true });
        }
        catch { }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
