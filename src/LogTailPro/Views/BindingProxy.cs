using System.Windows;

namespace LogTailPro.Views;

/// <summary>
/// Lets elements that are not part of the visual tree (such as DataGrid columns) bind to the
/// surrounding DataContext: declare it in resources with <c>Data="{Binding}"</c> and bind through it.
/// </summary>
public sealed class BindingProxy : Freezable
{
    /// <summary>The proxied value (normally the DataContext).</summary>
    public static readonly DependencyProperty DataProperty =
        DependencyProperty.Register(nameof(Data), typeof(object), typeof(BindingProxy), new UIPropertyMetadata(null));

    /// <summary>The proxied value.</summary>
    public object? Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    /// <inheritdoc/>
    protected override Freezable CreateInstanceCore() => new BindingProxy();
}
