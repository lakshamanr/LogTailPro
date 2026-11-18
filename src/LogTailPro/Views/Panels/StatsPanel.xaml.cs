using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using LogTailPro.ViewModels;

namespace LogTailPro.Views;

/// <summary>
/// Interaction logic for StatsPanel.xaml.
/// Reactively tracks MainViewModel.SelectedTab to update live analytics
/// whenever tabs are opened, closed, or switched in AvalonDock.
/// </summary>
public partial class StatsPanel : UserControl, INotifyPropertyChanged
{
    private MainViewModel? _hookedMainVm;

    public event PropertyChangedEventHandler? PropertyChanged;

    public LogTabViewModel? ActiveTab
    {
        get
        {
            if (DataContext is MainViewModel mainVm)
                return mainVm.SelectedTab;
            if (DataContext is LogTabViewModel tabVm)
                return tabVm;
            return null;
        }
    }

    public StatsPanel()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_hookedMainVm != null)
        {
            _hookedMainVm.PropertyChanged -= OnMainViewModelPropertyChanged;
            _hookedMainVm = null;
        }

        if (DataContext is MainViewModel mainVm)
        {
            _hookedMainVm = mainVm;
            _hookedMainVm.PropertyChanged += OnMainViewModelPropertyChanged;
        }

        UpdateActiveTab();
    }

    private void OnMainViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.SelectedTab))
        {
            UpdateActiveTab();
        }
    }

    public void UpdateActiveTab()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ActiveTab)));
    }
}
