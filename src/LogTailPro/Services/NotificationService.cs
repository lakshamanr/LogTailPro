using System.Windows;
using System.Windows.Threading;

namespace LogTailPro.Services;

/// <summary>Shows non-blocking in-app toast notifications in the bottom-right of the screen.</summary>
public static class NotificationService
{
    public static void Show(string title, string message)
    {
        Application.Current.Dispatcher.InvokeAsync(
            () => new ToastWindow(title, message).Show(),
            DispatcherPriority.Background);
    }
}

/// <summary>Small always-on-top popup that auto-closes after 4 seconds.</summary>
internal sealed class ToastWindow : Window
{
    public ToastWindow(string title, string message)
    {
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        Topmost = true;
        ShowInTaskbar = false;
        Width = 340;
        Height = 80;
        Background = System.Windows.Media.Brushes.DimGray;
        Opacity = 0.92;

        // Position bottom-right of primary screen
        Left = SystemParameters.WorkArea.Right - Width - 12;
        Top  = SystemParameters.WorkArea.Bottom - Height - 12;

        System.Windows.Controls.StackPanel panel = new() { Margin = new Thickness(12, 8, 12, 8) };
        panel.Children.Add(new System.Windows.Controls.TextBlock
        {
            Text = title,
            FontWeight = FontWeights.Bold,
            Foreground = System.Windows.Media.Brushes.White,
            FontSize = 13,
            TextTrimming = System.Windows.TextTrimming.CharacterEllipsis
        });
        panel.Children.Add(new System.Windows.Controls.TextBlock
        {
            Text = message.Length > 120 ? message[..117] + "…" : message,
            Foreground = System.Windows.Media.Brushes.LightGray,
            FontSize = 12,
            TextWrapping = System.Windows.TextWrapping.NoWrap,
            TextTrimming = System.Windows.TextTrimming.CharacterEllipsis
        });
        Content = panel;

        MouseLeftButtonDown += (_, _) => Close();

        DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(4) };
        timer.Tick += (_, _) => { timer.Stop(); Close(); };
        Loaded += (_, _) => timer.Start();
    }
}
