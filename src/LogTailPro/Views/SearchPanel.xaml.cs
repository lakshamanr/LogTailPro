using System.Windows.Controls;
using System.Windows.Input;
using LogTailPro.ViewModels;

namespace LogTailPro.Views;

/// <summary>Code-behind for SearchPanel. Handles focus and keyboard shortcuts.</summary>
public partial class SearchPanel : UserControl
{
    public SearchPanel()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is LogTabViewModel vm)
            vm.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LogTabViewModel.IsSearchPanelVisible) &&
            DataContext is LogTabViewModel vm && vm.IsSearchPanelVisible)
        {
            Dispatcher.InvokeAsync(() => SearchBox.Focus(),
                System.Windows.Threading.DispatcherPriority.Input);
        }
    }

    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not LogTabViewModel vm) return;

        switch (e.Key)
        {
            case Key.Enter:
                vm.NextMatchCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Escape:
                vm.CloseSearchCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.F3 when Keyboard.Modifiers == ModifierKeys.Shift:
                vm.PreviousMatchCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.F3:
                vm.NextMatchCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }
}
