using System.Diagnostics;
using System.Reflection;
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
        SetVersionText();
    }

    private void SetVersionText()
    {
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            var infoVer = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            string verStr;
            if (!string.IsNullOrWhiteSpace(infoVer))
            {
                // Strip build metadata or commit SHA if present (e.g. "2.10.0+a1b2c3d")
                verStr = infoVer.Split('+')[0];
            }
            else
            {
                var v = asm.GetName().Version;
                verStr = v != null ? $"{v.Major}.{v.Minor}.{v.Build}" : "2.10.0";
            }

            VersionText.Text = $"Version {verStr} — Windows x64";
        }
        catch
        {
            VersionText.Text = "Version 2.10.0 — Windows x64";
        }
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
