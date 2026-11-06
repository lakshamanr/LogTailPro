using System.Windows.Controls;

namespace LogTailPro.Views;

/// <summary>Dockable panel view; DataContext is the MainViewModel and bindings go through SelectedTab.</summary>
public partial class SearchPanelView : UserControl
{
    public SearchPanelView() => InitializeComponent();

    /// <summary>True while the query box (or anything else in this panel) has keyboard focus.</summary>
    public bool HasFocus => IsKeyboardFocusWithin;

    /// <summary>Caret position inside the query box.</summary>
    public int CaretIndex => QueryBox.CaretIndex;

    /// <summary>Puts focus back in the query box with the caret at <paramref name="caretIndex"/> and nothing selected.</summary>
    public void RestoreFocus(int caretIndex)
    {
        QueryBox.Focus();
        QueryBox.Select(Math.Min(caretIndex, QueryBox.Text.Length), 0);
    }

    /// <summary>Moves keyboard focus to the query box and selects its text.</summary>
    public void FocusQuery()
    {
        QueryBox.Focus();
        QueryBox.SelectAll();
    }
}
