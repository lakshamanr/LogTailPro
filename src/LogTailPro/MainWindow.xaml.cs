using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using AvalonDock;
using AvalonDock.Controls;
using AvalonDock.Layout;
using AvalonDock.Layout.Serialization;
using LogTailPro.Services;
using LogTailPro.ViewModels;
using LogTailPro.Views;

namespace LogTailPro;

/// <summary>
/// Code-behind for MainWindow — UI wiring only: file drag-drop, keyboard shortcuts and the
/// AvalonDock glue (panel visibility ↔ view-model flags, layout save/restore, document closing).
/// </summary>
public partial class MainWindow : Window
{
    private MainViewModel ViewModel => (MainViewModel)DataContext;

    private readonly DockLayoutService _layoutService = new();

    /// <summary>ContentId → panel content (the user controls defined in XAML).</summary>
    private readonly Dictionary<string, object> _panelContents = new();

    /// <summary>ContentId → the live anchorable model (re-resolved after every layout load).</summary>
    private readonly Dictionary<string, LayoutAnchorable> _anchorables = new();

    private readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromSeconds(1.5) };
    private LayoutRoot? _hookedRoot;
    private string _defaultLayoutXml = string.Empty;
    private bool _hadSavedLayout;
    private bool _syncing;
    private LogTabViewModel? _watchedTab;

    public MainWindow()
    {
        InitializeComponent();

        // Remember each panel's content, then capture the default layout before restoring a saved one.
        foreach (LayoutAnchorable a in DockLayoutRoot.Descendents().OfType<LayoutAnchorable>())
            if (!string.IsNullOrEmpty(a.ContentId) && a.Content != null)
                _panelContents[a.ContentId] = a.Content;

        using (StringWriter sw = new())
        {
            new XmlLayoutSerializer(Dock).Serialize(sw);
            _defaultLayoutXml = sw.ToString();
        }

        _hadSavedLayout = TryRestoreSavedLayout();
        RefreshAnchorables();

        _saveTimer.Tick += (_, _) => { _saveTimer.Stop(); SaveLayout(); };
        DataContextChanged += (_, _) => AssignPanelDataContexts();
        Loaded += OnLoaded;
    }

    public ErrorTimelineChartView ErrorChartView => ChartView;

    /// <summary>
    /// Panels live inside AvalonDock models (not in the window's logical tree), so they don't inherit
    /// the window DataContext — hand it to them explicitly.
    /// </summary>
    private void AssignPanelDataContexts()
    {
        if (DataContext is not MainViewModel vm) return;
        vm.RequestLayoutXml = GetLayoutXml;
        vm.RestoreLayoutXml = LoadLayoutXml;
        foreach ((string id, object content) in _panelContents)
        {
            if (content is FrameworkElement fe)
                fe.DataContext = id == "process" ? vm.ProcessExplorer : vm;
        }
    }

    // ── Docking: layout save / restore ────────────────────────────────────────

    /// <summary>Serializes the current docking layout to an XML string.</summary>
    public string GetLayoutXml()
    {
        using StringWriter sw = new();
        new XmlLayoutSerializer(Dock).Serialize(sw);
        return sw.ToString();
    }

    /// <summary>Restores a docking layout from an XML string, falling back safely on error.</summary>
    public void LoadLayoutXml(string layoutXml)
    {
        if (string.IsNullOrWhiteSpace(layoutXml)) return;
        try
        {
            using StringReader sr = new(layoutXml);
            CreateSerializer().Deserialize(sr);
            RefreshAnchorables();
        }
        catch (Exception)
        {
            try
            {
                CreateSerializer().Deserialize(new StringReader(_defaultLayoutXml));
                RefreshAnchorables();
            }
            catch { }
        }
    }

    private XmlLayoutSerializer CreateSerializer()
    {
        XmlLayoutSerializer serializer = new(Dock);
        serializer.LayoutSerializationCallback += (_, e) =>
        {
            // Panels get their original content back; log documents are recreated from Tabs, never restored.
            if (!string.IsNullOrEmpty(e.Model.ContentId) && _panelContents.TryGetValue(e.Model.ContentId, out object? content))
                e.Content = content;
            else
                e.Cancel = true;
        };
        return serializer;
    }

    private bool TryRestoreSavedLayout()
    {
        string? path = _layoutService.GetSavedLayoutPath();
        if (path == null) return false;
        try
        {
            CreateSerializer().Deserialize(path);
            return true;
        }
        catch (Exception)
        {
            // Corrupt or incompatible layout: fall back to the default one.
            _layoutService.Delete();
            CreateSerializer().Deserialize(new StringReader(_defaultLayoutXml));
            return false;
        }
    }

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void SaveLayout()
    {
        try
        {
            _layoutService.EnsureDirectory();
            new XmlLayoutSerializer(Dock).Serialize(_layoutService.LayoutPath);
        }
        catch (Exception)
        {
            // Layout persistence is best-effort; never block the UI.
        }
    }

    private void ResetLayout_Click(object sender, RoutedEventArgs e)
    {
        _layoutService.Delete();
        CreateSerializer().Deserialize(new StringReader(_defaultLayoutXml));

        // Deserialising replaces the whole layout, so re-bind the document source to recreate the tabs.
        Dock.DocumentsSource = null;
        Dock.SetBinding(DockingManager.DocumentsSourceProperty, new Binding(nameof(MainViewModel.Tabs)));

        _hadSavedLayout = false;
        RefreshAnchorables();
        ApplyAllPanelVisibility();
        if (ViewModel.SelectedTab != null)
            Dock.ActiveContent = ViewModel.SelectedTab;
        SaveLayout();
    }

    // ── Docking: panel ↔ view-model wiring ───────────────────────────────────

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        WatchTab(ViewModel.SelectedTab);
        ApplyAllPanelVisibility();
    }

    /// <summary>Re-resolves anchorables from the live layout and (re)hooks their visibility events.</summary>
    private void RefreshAnchorables()
    {
        // Auto-save the layout shortly after any change so positions survive a crash or kill.
        if (_hookedRoot != null) _hookedRoot.Updated -= OnLayoutUpdated;
        _hookedRoot = Dock.Layout;
        _hookedRoot.Updated += OnLayoutUpdated;

        _anchorables.Clear();
        foreach (LayoutAnchorable a in Dock.Layout.Descendents().OfType<LayoutAnchorable>())
        {
            if (string.IsNullOrEmpty(a.ContentId)) continue;
            _anchorables[a.ContentId] = a;
            a.PropertyChanged -= OnAnchorablePropertyChanged;
            a.PropertyChanged += OnAnchorablePropertyChanged;
        }

        // If a saved layout from an older version lacked newer panels (e.g. "chart"),
        // dynamically inject the missing panel into the live layout tree so it can be opened.
        foreach (KeyValuePair<string, object> pair in _panelContents)
        {
            if (!_anchorables.ContainsKey(pair.Key))
            {
                LayoutAnchorable missing = new()
                {
                    ContentId = pair.Key,
                    Title = GetDefaultPanelTitle(pair.Key),
                    Content = pair.Value,
                    IsVisible = false,
                    CanClose = true,
                    CanHide = true
                };

                LayoutAnchorablePane newPane = new(missing)
                {
                    DockHeight = new GridLength(pair.Key == "chart" ? 160 : 180)
                };

                if (Dock.Layout.RootPanel != null)
                {
                    Dock.Layout.RootPanel.Children.Add(newPane);
                }
                else
                {
                    LayoutAnchorablePane? existingPane = Dock.Layout.Descendents().OfType<LayoutAnchorablePane>().LastOrDefault();
                    if (existingPane != null)
                    {
                        existingPane.Children.Add(missing);
                    }
                }

                _anchorables[pair.Key] = missing;
                missing.PropertyChanged += OnAnchorablePropertyChanged;
            }
        }
    }

    private static string GetDefaultPanelTitle(string id) => id switch
    {
        "chart" => "Error Timeline Chart",
        "process" => "Process Explorer",
        "details" => "Line Details",
        "stats" => "Stats",
        "bookmarks" => "Bookmarks",
        "search" => "Search",
        "results" => "Search Results",
        _ => id
    };

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.SelectedTab):
                WatchTab(ViewModel.SelectedTab);
                if (!_syncing && ViewModel.SelectedTab != null && !ReferenceEquals(Dock.ActiveContent, ViewModel.SelectedTab))
                {
                    _syncing = true;
                    try { Dock.ActiveContent = ViewModel.SelectedTab; }
                    finally { _syncing = false; }
                }
                break;
            case nameof(MainViewModel.IsProcessExplorerVisible):
                SetAnchorableVisible("process", ViewModel.IsProcessExplorerVisible);
                break;
        }
    }

    private void WatchTab(LogTabViewModel? tab)
    {
        if (_watchedTab != null)
        {
            _watchedTab.PropertyChanged -= OnTabPropertyChanged;
            _watchedTab.BookmarkAdded -= OnBookmarkAdded;
        }
        _watchedTab = tab;
        if (_watchedTab != null)
        {
            _watchedTab.PropertyChanged += OnTabPropertyChanged;
            _watchedTab.BookmarkAdded += OnBookmarkAdded;
        }
    }

    /// <summary>Reveals the Bookmarks panel (without stealing focus) when the user bookmarks a line.</summary>
    private void OnBookmarkAdded(object? sender, EventArgs e)
        => SetAnchorableVisible("bookmarks", true, activate: false);

    // ── Split view (side-by-side / stacked tab groups) ────────────────────────

    /// <summary>Opens the Split View dialog so the user picks which tabs go beside the current one.</summary>
    private void SplitView_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Tabs.Count < 2)
        {
            ViewModel.StatusMessage = "Open at least two logs to use Split View.";
            return;
        }

        SplitViewDialog dialog = new(new SplitViewViewModel(ViewModel.Tabs, ViewModel.SelectedTab)) { Owner = this };
        if (dialog.ShowDialog() != true) return;

        PreservePanelVisibility(() => ApplySplit(dialog.ViewModel.SelectedTabs, dialog.ViewModel.IsSideBySide));
    }

    /// <summary>
    /// Moves the first chosen tab into a new tab group (vertical = side by side, horizontal = stacked),
    /// then moves any further chosen tabs into that same group.
    /// </summary>
    private void ApplySplit(IReadOnlyList<LogTabViewModel> chosen, bool sideBySide)
    {
        bool first = true;
        foreach (LogTabViewModel tab in chosen)
        {
            Dock.ActiveContent = tab;
            ViewModel.SelectedTab = tab;
            LayoutDocumentItem? item = GetActiveDocumentItem();
            if (item == null) continue;

            System.Windows.Input.ICommand cmd = first
                ? (sideBySide ? item.NewVerticalTabGroupCommand : item.NewHorizontalTabGroupCommand)
                : item.MoveToNextTabGroupCommand;
            if (cmd.CanExecute(null)) cmd.Execute(null);
            first = false;
        }
    }

    private void MoveToNextGroup_Click(object sender, RoutedEventArgs e)
    {
        if (GetActiveDocumentItem() is { } item && item.MoveToNextTabGroupCommand.CanExecute(null))
            PreservePanelVisibility(() => item.MoveToNextTabGroupCommand.Execute(null));
    }

    /// <summary>
    /// AvalonDock re-opens hidden panels while restructuring document groups; remember which panels
    /// were open before <paramref name="action"/> and put them back afterwards.
    /// </summary>
    private void PreservePanelVisibility(Action action)
    {
        Dictionary<string, bool> before = _anchorables.ToDictionary(kv => kv.Key, kv => kv.Value.IsVisible);
        action();

        void Restore()
        {
            foreach ((string id, bool visible) in before)
                SetAnchorableVisible(id, visible, activate: false);
        }

        Restore();
        Dispatcher.BeginInvoke(Restore, DispatcherPriority.Background);
    }

    private LayoutDocumentItem? GetActiveDocumentItem()
    {
        LayoutDocument? doc = Dock.Layout.Descendents().OfType<LayoutDocument>()
            .FirstOrDefault(d => ReferenceEquals(d.Content, ViewModel.SelectedTab));
        return doc == null ? null : Dock.GetLayoutItemFromModel(doc) as LayoutDocumentItem;
    }

    private void OnTabPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(LogTabViewModel.IsSearchPanelVisible):
                SetAnchorableVisible("search", _watchedTab?.IsSearchPanelVisible == true, focusSearch: true);
                break;
            case nameof(LogTabViewModel.IsSearchResultsVisible):
                SetAnchorableVisible("results", _watchedTab?.IsSearchResultsVisible == true, activate: false);
                break;
            case nameof(LogTabViewModel.IsChartPanelVisible):
                SetAnchorableVisible("chart", _watchedTab?.IsChartPanelVisible == true);
                break;
            case nameof(LogTabViewModel.IsStatsPanelVisible):
                SetAnchorableVisible("stats", _watchedTab?.IsStatsPanelVisible == true);
                break;
            case nameof(LogTabViewModel.IsDetailPanelVisible):
                SetAnchorableVisible("details", _watchedTab?.IsDetailPanelVisible == true);
                break;
        }
    }

    /// <summary>Shows or hides a panel without echoing the change back to the view-model.</summary>
    private void SetAnchorableVisible(string id, bool visible, bool focusSearch = false, bool activate = true)
    {
        if (!_anchorables.TryGetValue(id, out LayoutAnchorable? a)) return;

        // Live search shows/hides the results panel while the user is still typing: keep their focus and caret.
        bool keepSearchFocus = id != "search" && !activate && SearchView.HasFocus;
        int caret = keepSearchFocus ? SearchView.CaretIndex : 0;

        if (a.IsVisible != visible)
        {
            _syncing = true;
            try
            {
                if (visible) a.Show(); else a.Hide();
            }
            finally { _syncing = false; }
        }

        if (keepSearchFocus)
            Dispatcher.BeginInvoke(() => SearchView.RestoreFocus(caret), DispatcherPriority.Input);

        if (visible && activate)
        {
            a.IsActive = true;
            if (focusSearch)
                Dispatcher.BeginInvoke(() => SearchView.FocusQuery(), DispatcherPriority.Input);
        }
    }

    /// <summary>Pushes the user's show/hide (X button, drag) from an anchorable back to the view-model flag.</summary>
    private void OnAnchorablePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_syncing || e.PropertyName != nameof(LayoutAnchorable.IsVisible)) return;
        if (sender is not LayoutAnchorable a || a.ContentId == null) return;
        bool visible = a.IsVisible;
        LogTabViewModel? tab = ViewModel.SelectedTab;

        switch (a.ContentId)
        {
            case "process": ViewModel.IsProcessExplorerVisible = visible; break;
            case "search":  if (tab != null) tab.IsSearchPanelVisible = visible; break;
            case "results": if (tab != null) tab.IsSearchResultsVisible = visible; break;
            case "chart":   if (tab != null) tab.IsChartPanelVisible = visible; break;
            case "stats":   if (tab != null) tab.IsStatsPanelVisible = visible; break;
            case "details": if (tab != null) tab.IsDetailPanelVisible = visible; break;
        }
    }

    /// <summary>Applies the view-model flags to every panel (used at startup and after a layout reset).</summary>
    private void ApplyAllPanelVisibility()
    {
        // A saved layout already remembers which panels were open: keep that, only sync the explorer flag.
        if (_hadSavedLayout)
        {
            if (_anchorables.TryGetValue("process", out LayoutAnchorable? proc))
                ViewModel.IsProcessExplorerVisible = proc.IsVisible;
            return;
        }

        LogTabViewModel? tab = ViewModel.SelectedTab;
        SetAnchorableVisible("process", ViewModel.IsProcessExplorerVisible);
        SetAnchorableVisible("search",  tab?.IsSearchPanelVisible == true);
        SetAnchorableVisible("results", tab?.IsSearchResultsVisible == true);
        SetAnchorableVisible("chart",   tab?.IsChartPanelVisible == true);
        SetAnchorableVisible("stats",   tab?.IsStatsPanelVisible == true);
        SetAnchorableVisible("details", tab?.IsDetailPanelVisible == true);
        if (!_hadSavedLayout)
            SetAnchorableVisible("bookmarks", false);
    }

    // ── Docking: documents ────────────────────────────────────────────────────

    private void Dock_DocumentClosing(object? sender, DocumentClosingEventArgs e)
    {
        // Route closing through the view-model so the tab is disposed and removed from Tabs.
        if (e.Document.Content is LogTabViewModel tab)
        {
            e.Cancel = true;
            ViewModel.CloseTabCommand.Execute(tab);
        }
    }

    private void Dock_ActiveContentChanged(object? sender, EventArgs e)
    {
        if (_syncing) return;
        if (Dock.ActiveContent is LogTabViewModel tab && !ReferenceEquals(ViewModel.SelectedTab, tab))
        {
            _syncing = true;
            try { ViewModel.SelectedTab = tab; }
            finally { _syncing = false; }
        }
    }

    // ── View ▸ Panels menu ────────────────────────────────────────────────────

    private void PanelsMenu_SubmenuOpened(object sender, RoutedEventArgs e)
    {
        foreach (MenuItem item in PanelsMenu.Items.OfType<MenuItem>())
            if (item.Tag is string id && _anchorables.TryGetValue(id, out LayoutAnchorable? a))
                item.IsChecked = a.IsVisible;
    }

    private void PanelMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string id } item) return;
        bool visible = item.IsChecked;
        LogTabViewModel? tab = ViewModel.SelectedTab;

        // Flag-backed panels go through the view-model (which then drives the dock); others directly.
        switch (id)
        {
            case "process": ViewModel.IsProcessExplorerVisible = visible; return;
            case "search"  when tab != null: tab.IsSearchPanelVisible = visible; return;
            case "results" when tab != null: tab.IsSearchResultsVisible = visible; return;
            case "chart"   when tab != null: tab.IsChartPanelVisible = visible; return;
            case "stats"   when tab != null: tab.IsStatsPanelVisible = visible; return;
            case "details" when tab != null: tab.IsDetailPanelVisible = visible; return;
        }
        SetAnchorableVisible(id, visible);
    }

    // ── File drag-and-drop ────────────────────────────────────────────────────

    private async void Window_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
        foreach (string file in files)
            await ViewModel.OpenFileTabAsync(file);
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    // ── Keyboard ──────────────────────────────────────────────────────────────

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
                if (e.Key == Key.T && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            if (ViewModel.SelectedTab != null)
                ViewModel.SelectedTab.IsChartPanelVisible = !ViewModel.SelectedTab.IsChartPanelVisible;
            e.Handled = true;
            return;
        }

        if (e.Key == Key.O && Keyboard.Modifiers == ModifierKeys.Control)
        {
            ViewModel.OpenFileCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.W && Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (ViewModel.SelectedTab != null)
                ViewModel.CloseTabCommand.Execute(ViewModel.SelectedTab);
            e.Handled = true;
            return;
        }

        // F — toggle follow tail (only when not typing in a TextBox)
        if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.None &&
            FocusManager.GetFocusedElement(this) is not TextBox)
        {
            ViewModel.SelectedTab?.ToggleFollowTailCommand.Execute(null);
            e.Handled = true;
        }
    }

    // ── Window lifecycle ──────────────────────────────────────────────────────

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        if (ViewModel?.Settings == null) return;
        ViewModel.Settings.WindowWidth  = ActualWidth;
        ViewModel.Settings.WindowHeight = ActualHeight;
        ViewModel.Settings.WindowMaximized = WindowState == WindowState.Maximized;
        if (WindowState == WindowState.Normal)
        {
            ViewModel.Settings.WindowTop  = Top;
            ViewModel.Settings.WindowLeft = Left;
        }
        ViewModel.SaveSettings();

        _saveTimer.Stop();
        SaveLayout();
    }
}
