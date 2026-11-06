using System.Windows;
using System.Windows.Controls;
using LogTailPro.ViewModels;

namespace LogTailPro.Views;

/// <summary>
/// Chooses the AvalonDock container style: log-tab documents get <see cref="DocumentStyle"/>
/// (title bound to the file name); static panels keep AvalonDock's defaults.
/// </summary>
public sealed class DockItemStyleSelector : StyleSelector
{
    /// <summary>Style applied to log-tab document items.</summary>
    public Style? DocumentStyle { get; set; }

    /// <inheritdoc/>
    public override Style? SelectStyle(object item, DependencyObject container)
        => item is LogTabViewModel ? DocumentStyle : null;
}

/// <summary>
/// Applies the log-tab view template only to <see cref="LogTabViewModel"/> documents so that
/// static panels (whose content is already a UI element) are shown as-is.
/// </summary>
public sealed class DockItemTemplateSelector : DataTemplateSelector
{
    /// <summary>Template that renders a log tab.</summary>
    public DataTemplate? LogTabTemplate { get; set; }

    /// <inheritdoc/>
    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
        => item is LogTabViewModel ? LogTabTemplate : null;
}
