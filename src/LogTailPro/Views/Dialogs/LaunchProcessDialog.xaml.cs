using System.IO;
using System.Windows;
using LogTailPro.Models;
using Microsoft.Win32;

namespace LogTailPro.Views;

/// <summary>Dialog that collects the parameters for launching a process and capturing its output.</summary>
public partial class LaunchProcessDialog : Window
{
    /// <summary>Executable to launch (valid when the dialog returns true).</summary>
    public string ExecutablePath { get; private set; } = string.Empty;

    /// <summary>Command-line arguments.</summary>
    public string Arguments { get; private set; } = string.Empty;

    /// <summary>Working directory, or null to use the default.</summary>
    public string? WorkingDirectory { get; private set; }

    /// <summary>Which stream(s) to capture.</summary>
    public ProcessAttachmentType CaptureType { get; private set; }

    /// <summary>Initialises the dialog.</summary>
    public LaunchProcessDialog() => InitializeComponent();

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        OpenFileDialog dlg = new()
        {
            Filter = "Executables|*.exe;*.bat;*.cmd|All Files|*.*",
            Title = "Select Executable"
        };
        if (dlg.ShowDialog(this) != true) return;

        ExeBox.Text = dlg.FileName;
        if (string.IsNullOrWhiteSpace(WorkDirBox.Text))
            WorkDirBox.Text = Path.GetDirectoryName(dlg.FileName) ?? string.Empty;
    }

    private void BrowseDir_Click(object sender, RoutedEventArgs e)
    {
        OpenFolderDialog dlg = new() { Title = "Select Working Directory" };
        if (dlg.ShowDialog(this) == true) WorkDirBox.Text = dlg.FolderName;
    }

    private void Launch_Click(object sender, RoutedEventArgs e)
    {
        string exe = ExeBox.Text.Trim().Trim('"');
        if (exe.Length == 0)
        {
            MessageBox.Show(this, "Please specify an executable.", "Launch Process",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        ExecutablePath = exe;
        Arguments = ArgsBox.Text.Trim();
        WorkingDirectory = string.IsNullOrWhiteSpace(WorkDirBox.Text) ? null : WorkDirBox.Text.Trim();

        CaptureType = RbBoth.IsChecked == true ? ProcessAttachmentType.StandardOutputAndError
                    : RbStderr.IsChecked == true ? ProcessAttachmentType.StandardError
                    : ProcessAttachmentType.StandardOutput;

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
