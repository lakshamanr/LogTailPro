using System.Windows;
using System.Windows.Controls;
using LogTailPro.Models;
using LogTailPro.ViewModels;

namespace LogTailPro.Views;

/// <summary>
/// Interaction logic for HelpPanelView.xaml (Dockable Help Guide Panel).
/// </summary>
public partial class HelpPanelView : UserControl
{
    public HelpDialogViewModel? ViewModel => DataContext as HelpDialogViewModel;

    public HelpPanelView()
    {
        InitializeComponent();
    }

    private void ViewMode_Changed(object sender, RoutedEventArgs e)
    {
        if (TopicsListBox == null || DetailsScrollViewer == null) return;

        bool showDetails = RbDetails.IsChecked == true;
        TopicsListBox.Visibility = showDetails ? Visibility.Collapsed : Visibility.Visible;
        DetailsScrollViewer.Visibility = showDetails ? Visibility.Visible : Visibility.Collapsed;
    }

    private void TopicsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TopicsListBox.SelectedItem is HelpTopic)
        {
            // Switch to details view when a topic is clicked
            RbDetails.IsChecked = true;
        }
    }

    private void BackToTopics_Click(object sender, RoutedEventArgs e)
    {
        RbTopics.IsChecked = true;
    }

    private void PopOut_Click(object sender, RoutedEventArgs e)
    {
        HelpDialog dialog = new(ViewModel?.SelectedTopic?.Id)
        {
            Owner = Application.Current.MainWindow
        };
        dialog.Show();
    }
}
