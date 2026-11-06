using System.Windows.Controls;

namespace LogTailPro.Views;

/// <summary>Dockable panel view; DataContext is the MainViewModel and bindings go through SelectedTab.</summary>
public partial class StatsPanel : UserControl
{
    public StatsPanel() => InitializeComponent();
}
