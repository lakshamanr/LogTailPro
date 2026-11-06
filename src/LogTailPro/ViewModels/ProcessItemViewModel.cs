using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LogTailPro.Models;

namespace LogTailPro.ViewModels;

/// <summary>Single node in the Process Explorer tree view.</summary>
public sealed partial class ProcessItemViewModel : ObservableObject
{
    private readonly Action<ProcessInfo, ProcessAttachmentType> _attach;
    private readonly Action _launch;
    private readonly Action<ProcessInfo> _kill;

    /// <summary>Creates a node; actions are supplied by the owning <see cref="ProcessExplorerViewModel"/>.</summary>
    public ProcessItemViewModel(
        ProcessInfo info,
        Action<ProcessInfo, ProcessAttachmentType> attach,
        Action launch,
        Action<ProcessInfo> kill)
    {
        Info = info;
        _attach = attach;
        _launch = launch;
        _kill = kill;
        Children = new ObservableCollection<ProcessItemViewModel>();
    }

    /// <summary>Underlying snapshot.</summary>
    public ProcessInfo Info { get; }

    /// <summary>Process id.</summary>
    public int Pid => Info.Pid;

    /// <summary>"name [pid]".</summary>
    public string DisplayName => Info.DisplayName;

    /// <summary>CPU usage text.</summary>
    public string CpuText => Info.CpuText;

    /// <summary>Working-set text.</summary>
    public string MemoryText => Info.MemoryText;

    /// <summary>Executable path.</summary>
    public string ExecutablePath => Info.ExecutablePath;

    /// <summary>Owning account.</summary>
    public string UserName => Info.UserName;

    /// <summary>Start time text.</summary>
    public string StartTimeText => Info.StartTimeText;

    /// <summary>True for .NET processes.</summary>
    public bool IsDotNet => Info.IsDotNet;

    /// <summary>Responsiveness state (drives the status dot colour via theme brushes).</summary>
    public ProcessStatus Status => Info.Status;

    /// <summary>Whether the tree node is expanded.</summary>
    [ObservableProperty] private bool _isExpanded;

    /// <summary>Whether the tree node is selected.</summary>
    [ObservableProperty] private bool _isSelected;

    /// <summary>Child processes.</summary>
    public ObservableCollection<ProcessItemViewModel> Children { get; }

    /// <summary>Used as the accessible name of the tree node (screen readers, UI automation).</summary>
    public override string ToString() => DisplayName;

    [RelayCommand]
    private void AttachDebugOutput() => _attach(Info, ProcessAttachmentType.DebugOutput);

    [RelayCommand]
    private void AttachEventLog() => _attach(Info, ProcessAttachmentType.WindowsEventLog);

    [RelayCommand]
    private void LaunchAndAttach() => _launch();

    [RelayCommand]
    private void OpenFileLocation()
    {
        if (File.Exists(ExecutablePath))
            Process.Start("explorer.exe", $"/select,\"{ExecutablePath}\"");
    }

    [RelayCommand]
    private void CopyPid() => Clipboard.SetText(Pid.ToString());

    [RelayCommand]
    private void CopyPath()
    {
        if (!string.IsNullOrEmpty(ExecutablePath)) Clipboard.SetText(ExecutablePath);
    }

    [RelayCommand]
    private void KillProcess()
    {
        MessageBoxResult r = MessageBox.Show(
            $"Kill {DisplayName}?\n\nThis immediately terminates the process and all its children.",
            "Kill Process", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (r == MessageBoxResult.Yes)
            _kill(Info);
    }
}
