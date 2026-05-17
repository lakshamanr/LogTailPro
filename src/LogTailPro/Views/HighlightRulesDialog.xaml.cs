using System.Windows;
using LogTailPro.Models;
using LogTailPro.ViewModels;

namespace LogTailPro.Views;

/// <summary>Highlight rules editor dialog.</summary>
public partial class HighlightRulesDialog : Window
{
    public HighlightRulesViewModel ViewModel { get; }

    public HighlightRulesDialog(IEnumerable<HighlightRule> existingRules)
    {
        InitializeComponent();
        ViewModel = new HighlightRulesViewModel(existingRules);
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
