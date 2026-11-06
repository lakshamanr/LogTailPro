using System.Windows;
using System.Windows.Controls;
using LogTailPro.Models;
using LogTailPro.ViewModels;

namespace LogTailPro.Views;

/// <summary>Dockable panel view; DataContext is the MainViewModel and bindings go through SelectedTab.</summary>
public partial class DetailsPanel : UserControl
{
    public DetailsPanel() => InitializeComponent();

    private void CorrelateProperty_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: LogPropertyNode node } &&
            !string.IsNullOrWhiteSpace(node.Value) &&
            DataContext is MainViewModel mvm)
        {
            mvm.CorrelateId(node.Value);
        }
    }

    private void CopyPropertyValue_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: LogPropertyNode node } &&
            node.Value != null)
        {
            Clipboard.SetText(node.Value);
        }
    }
}
