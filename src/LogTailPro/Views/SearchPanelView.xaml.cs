using System.Windows.Controls;

namespace LogTailPro.Views;

/// <summary>Dockable panel view; DataContext is the MainViewModel and bindings go through SelectedTab.</summary>
public partial class SearchPanelView : UserControl
{
    public SearchPanelView() => InitializeComponent();

    /// <summary>Moves keyboard focus to the query box and selects its text.</summary>
    public void FocusQuery()
    {
        QueryBox.Focus();
        QueryBox.SelectAll();
    }
}
