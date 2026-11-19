using System.ComponentModel;

namespace LogTailPro.Models;

/// <summary>A user-defined rule that colorizes matching log lines.</summary>
public sealed class HighlightRule : INotifyPropertyChanged
{
    private static readonly PropertyChangedEventArgs PatternArgs = new(nameof(Pattern));
    private static readonly PropertyChangedEventArgs IsRegexArgs = new(nameof(IsRegex));
    private static readonly PropertyChangedEventArgs IsCaseSensitiveArgs = new(nameof(IsCaseSensitive));
    private static readonly PropertyChangedEventArgs ForegroundColorArgs = new(nameof(ForegroundColor));
    private static readonly PropertyChangedEventArgs BackgroundColorArgs = new(nameof(BackgroundColor));
    private static readonly PropertyChangedEventArgs IsEnabledArgs = new(nameof(IsEnabled));
    private static readonly PropertyChangedEventArgs PriorityArgs = new(nameof(Priority));
    private static readonly PropertyChangedEventArgs NotifyOnMatchArgs = new(nameof(NotifyOnMatch));
    private static readonly PropertyChangedEventArgs NotifyTitleArgs = new(nameof(NotifyTitle));

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Stable unique identifier for this rule.</summary>
    public string Id { get; init; } = Guid.NewGuid().ToString();

    private string _pattern = string.Empty;
    /// <summary>Text or regex pattern to match against.</summary>
    public string Pattern
    {
        get => _pattern;
        set
        {
            if (_pattern == value) return;
            _pattern = value;
            PropertyChanged?.Invoke(this, PatternArgs);
        }
    }

    private bool _isRegex;
    /// <summary>True if <see cref="Pattern"/> is a regular expression.</summary>
    public bool IsRegex
    {
        get => _isRegex;
        set
        {
            if (_isRegex == value) return;
            _isRegex = value;
            PropertyChanged?.Invoke(this, IsRegexArgs);
        }
    }

    private bool _isCaseSensitive;
    /// <summary>True if matching should be case-sensitive.</summary>
    public bool IsCaseSensitive
    {
        get => _isCaseSensitive;
        set
        {
            if (_isCaseSensitive == value) return;
            _isCaseSensitive = value;
            PropertyChanged?.Invoke(this, IsCaseSensitiveArgs);
        }
    }

    private string _foregroundColor = "#FFFFFF";
    /// <summary>Foreground (text) color as an HTML hex string, e.g. "#FFFFFF".</summary>
    public string ForegroundColor
    {
        get => _foregroundColor;
        set
        {
            if (_foregroundColor == value) return;
            _foregroundColor = value;
            PropertyChanged?.Invoke(this, ForegroundColorArgs);
        }
    }

    private string _backgroundColor = "#FF0000";
    /// <summary>Background color as an HTML hex string, e.g. "#FF0000".</summary>
    public string BackgroundColor
    {
        get => _backgroundColor;
        set
        {
            if (_backgroundColor == value) return;
            _backgroundColor = value;
            PropertyChanged?.Invoke(this, BackgroundColorArgs);
        }
    }

    private bool _isEnabled = true;
    /// <summary>Whether this rule is active during rendering.</summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled == value) return;
            _isEnabled = value;
            PropertyChanged?.Invoke(this, IsEnabledArgs);
        }
    }

    private int _priority;
    /// <summary>Evaluation order — lower value means evaluated first.</summary>
    public int Priority
    {
        get => _priority;
        set
        {
            if (_priority == value) return;
            _priority = value;
            PropertyChanged?.Invoke(this, PriorityArgs);
        }
    }

    private bool _notifyOnMatch;
    /// <summary>When true, a Windows toast is shown when a new log line matches this rule.</summary>
    public bool NotifyOnMatch
    {
        get => _notifyOnMatch;
        set
        {
            if (_notifyOnMatch == value) return;
            _notifyOnMatch = value;
            PropertyChanged?.Invoke(this, NotifyOnMatchArgs);
        }
    }

    private string _notifyTitle = "LogTailPro Alert";
    /// <summary>Title shown in the toast notification when <see cref="NotifyOnMatch"/> fires.</summary>
    public string NotifyTitle
    {
        get => _notifyTitle;
        set
        {
            if (_notifyTitle == value) return;
            _notifyTitle = value;
            PropertyChanged?.Invoke(this, NotifyTitleArgs);
        }
    }
}
