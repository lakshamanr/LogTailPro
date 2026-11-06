using System.Windows;
using LogTailPro.ViewModels;

namespace LogTailPro.Views;

/// <summary>Source-selection UI for a combined timeline.</summary>
public partial class TimelineSourcesDialog : Window
{
    private readonly TimelineSourceChoice[] _choices;

    /// <summary>Creates a picker for currently open file tabs.</summary>
    public TimelineSourcesDialog(IEnumerable<LogTabViewModel> tabs)
    {
        InitializeComponent();
        _choices = tabs.Select(tab => new TimelineSourceChoice(tab)).ToArray();
        DataContext = _choices;
    }

    /// <summary>Selected tabs, in their original display order.</summary>
    public IReadOnlyList<LogTabViewModel> SelectedSources =>
        _choices.Where(choice => choice.IsSelected).Select(choice => choice.Tab).ToArray();

    private void Create_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedSources.Count < 2)
        {
            MessageBox.Show(this, "Select at least two log files.", "Combined Timeline",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        DialogResult = true;
    }
}
