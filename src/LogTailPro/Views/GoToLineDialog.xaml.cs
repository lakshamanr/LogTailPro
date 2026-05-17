using System.Windows;
using System.Windows.Input;

namespace LogTailPro.Views;

/// <summary>Simple modal dialog for the Go To Line (Ctrl+G) command.</summary>
public partial class GoToLineDialog : Window
{
    /// <summary>The line number entered by the user (valid only when <see cref="DialogResult"/> is true).</summary>
    public long LineNumber { get; private set; } = 1;

    public GoToLineDialog()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            LineNumberBox.Focus();
            LineNumberBox.SelectAll();
        };
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (long.TryParse(LineNumberBox.Text.Trim(), out long n) && n >= 1)
        {
            LineNumber = n;
            DialogResult = true;
        }
        else
        {
            MessageBox.Show("Please enter a valid line number.", "Go To Line",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void LineNumberBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) OkButton_Click(sender, new RoutedEventArgs());
    }
}
