using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using LogTailPro.ViewModels;

namespace LogTailPro.Views;

/// <summary>Source-selection and ID dialog for Request/Trace ID correlation.</summary>
public partial class CorrelateIdDialog : Window, INotifyPropertyChanged
{
    private string _correlationId = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The ID to correlate across open logs.</summary>
    public string CorrelationId
    {
        get => _correlationId;
        set
        {
            if (_correlationId != value)
            {
                _correlationId = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CorrelationId)));
            }
        }
    }

    /// <summary>Available open log sources.</summary>
    public ObservableCollection<TimelineSourceChoice> Choices { get; }

    /// <summary>Sources selected by the user.</summary>
    public IReadOnlyList<LogTabViewModel> SelectedSources =>
        Choices.Where(choice => choice.IsSelected).Select(choice => choice.Tab).ToArray();

    public CorrelateIdDialog(IEnumerable<LogTabViewModel> tabs, string? initialId = null)
    {
        InitializeComponent();
        _correlationId = initialId?.Trim() ?? string.Empty;
        Choices = new ObservableCollection<TimelineSourceChoice>(
            tabs.Where(t => t.IsTimelineSource).Select(t => new TimelineSourceChoice(t)));
        DataContext = this;

        Loaded += (_, _) =>
        {
            IdTextBox.Focus();
            if (!string.IsNullOrEmpty(CorrelationId))
            {
                IdTextBox.SelectAll();
            }
        };
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (TimelineSourceChoice choice in Choices)
        {
            choice.IsSelected = true;
        }
    }

    private void DeselectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (TimelineSourceChoice choice in Choices)
        {
            choice.IsSelected = false;
        }
    }

    private void Correlate_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(CorrelationId))
        {
            MessageBox.Show(this, "Please enter a Correlation / Trace ID to search.", "Correlate ID",
                MessageBoxButton.OK, MessageBoxImage.Information);
            IdTextBox.Focus();
            return;
        }

        if (SelectedSources.Count == 0)
        {
            MessageBox.Show(this, "Select at least one log source to correlate across.", "Correlate ID",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        DialogResult = true;
    }
}
