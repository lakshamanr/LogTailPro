using System.Windows;
using System.Windows.Input;

namespace LogTailPro.Views;

/// <summary>Simple single-line text-input dialog used for naming saved patterns and workspaces.</summary>
public partial class NameInputDialog : Window
{
    public string InputValue => InputBox.Text;

    public NameInputDialog(string windowTitle, string prompt, string defaultValue = "")
    {
        InitializeComponent();
        Title = windowTitle;
        PromptLabel.Content = prompt;
        InputBox.Text = defaultValue;
        Loaded += (_, _) => { InputBox.Focus(); InputBox.SelectAll(); };
    }

    private void OK_Click(object sender, RoutedEventArgs e) => DialogResult = true;
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void InputBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { DialogResult = true; e.Handled = true; }
    }
}
