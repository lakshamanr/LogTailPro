using System;
using System.Collections.Generic;
using System.Linq;
using LogTailPro.Models;
using LogTailPro.PluginContract;
using LogTailPro.ViewModels;

namespace LogTailPro.Services;

public sealed class PluginLogContext : ILogContext
{
    public string? FilePath { get; }
    public string TabTitle { get; }
    public int CurrentLineNumber { get; }
    public string? SelectedText { get; }
    public IReadOnlyList<string> SelectedLines { get; }
    public string? CurrentFilterExpression { get; }
    public bool IsStructuredJson { get; }
    public int TotalLineCount { get; }

    public PluginLogContext(LogTabViewModel? tab)
    {
        if (tab == null)
        {
            FilePath = null;
            TabTitle = string.Empty;
            CurrentLineNumber = -1;
            SelectedText = null;
            SelectedLines = Array.Empty<string>();
            CurrentFilterExpression = null;
            IsStructuredJson = false;
            TotalLineCount = 0;
            return;
        }

        FilePath = !string.IsNullOrWhiteSpace(tab.FilePath) ? tab.FilePath : tab.TempFilePath;
        TabTitle = tab.DisplayName;
        CurrentLineNumber = (int)(tab.SelectedLogLine?.LineNumber ?? -1);
        SelectedText = tab.SelectedLogLine?.Text;
        
        var effectiveLines = tab.SelectedLinesProvider?.Invoke();
        if (effectiveLines != null && effectiveLines.Count > 0)
        {
            SelectedLines = effectiveLines.Select(l => l.Text).ToList();
        }
        else if (tab.SelectedLogLines != null && tab.SelectedLogLines.Count > 0)
        {
            SelectedLines = tab.SelectedLogLines.Select(l => l.Text).ToList();
        }
        else if (tab.SelectedLogLine != null)
        {
            SelectedLines = new[] { tab.SelectedLogLine.Text };
        }
        else
        {
            SelectedLines = Array.Empty<string>();
        }

        CurrentFilterExpression = !string.IsNullOrWhiteSpace(tab.SearchText) ? tab.SearchText : null;
        IsStructuredJson = tab.IsStructuredViewEnabled;
        TotalLineCount = tab.VisibleLines.Count;
    }
}
