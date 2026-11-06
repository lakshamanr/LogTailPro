using System.Diagnostics;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LogTailPro.Models;

namespace LogTailPro.ViewModels;

/// <summary>Row in the Process Explorer services list.</summary>
public sealed partial class ServiceItemViewModel : ObservableObject
{
    private readonly Action<ProcessInfo> _openLogs;
    private readonly Action<ProcessInfo, ProcessAttachmentType> _attach;
    private readonly Action<ServiceInfo, ServiceAction> _control;
    private readonly Action _launch;
    private readonly Action<ProcessInfo> _kill;

    /// <summary>Creates a row; actions are supplied by the owning <see cref="ProcessExplorerViewModel"/>.</summary>
    /// <param name="info">The service.</param>
    /// <param name="host">Live snapshot of the hosting process, or null when the service is stopped.</param>
    /// <param name="openLogs">Log discovery for the service.</param>
    /// <param name="attach">Debug-output / Event Log capture.</param>
    /// <param name="control">Start / stop / restart.</param>
    /// <param name="launch">Opens the Launch &amp; Attach dialog.</param>
    /// <param name="kill">Terminates the hosting process.</param>
    public ServiceItemViewModel(
        ServiceInfo info,
        ProcessInfo? host,
        Action<ProcessInfo> openLogs,
        Action<ProcessInfo, ProcessAttachmentType> attach,
        Action<ServiceInfo, ServiceAction> control,
        Action launch,
        Action<ProcessInfo> kill)
    {
        Info = info;
        Host = host;
        _openLogs = openLogs;
        _attach = attach;
        _control = control;
        _launch = launch;
        _kill = kill;

        // The service itself as a "process": the same discovery / capture code that serves the process tree
        // works on it. The service's own executable is used so stopped services can still be searched
        // (config files and log folders next to the exe); the hosting PID adds open file handles when running.
        string exe = Info.ExecutablePath;
        Target = new ProcessInfo
        {
            Pid = host?.Pid ?? 0,
            Name = Info.Name,
            ExecutablePath = exe.Length > 0 ? exe : host?.ExecutablePath ?? string.Empty,
            MemoryBytes = host?.MemoryBytes ?? 0,
            CpuPercent = host?.CpuPercent ?? 0,
            Status = host?.Status ?? ProcessStatus.Running,
            IsDotNet = host?.IsDotNet ?? false,
            ParentPid = host?.ParentPid ?? 0,
            StartTime = host?.StartTime,
            UserName = host?.UserName is { Length: > 0 } u ? u : Info.AccountName
        };
    }

    /// <summary>Underlying snapshot.</summary>
    public ServiceInfo Info { get; }

    /// <summary>Hosting process snapshot (null when stopped or inaccessible).</summary>
    public ProcessInfo? Host { get; }

    /// <summary>The service expressed as a process, used for log discovery and capture.</summary>
    public ProcessInfo Target { get; }

    /// <summary>Short service name.</summary>
    public string Name => Info.Name;

    /// <summary>Display name.</summary>
    public string DisplayName => Info.DisplayName;

    /// <summary>Run state.</summary>
    public ServiceState State => Info.State;

    /// <summary>Run state text.</summary>
    public string StateText => Info.StateText;

    /// <summary>Start type text.</summary>
    public string StartTypeText => Info.StartTypeText;

    /// <summary>Hosting PID text ("—" when stopped).</summary>
    public string PidText => Info.IsRunning ? Info.Pid.ToString() : "—";

    /// <summary>Account the service runs under.</summary>
    public string AccountName => Info.AccountName;

    /// <summary>Command line of the service binary.</summary>
    public string BinaryPath => Info.BinaryPath;

    /// <summary>Service executable path.</summary>
    public string ExecutablePath => Target.ExecutablePath;

    /// <summary>True when the service has a hosting process (enables capture and kill commands).</summary>
    public bool IsRunning => Info.IsRunning;

    /// <summary>True when the hosting process runs the .NET runtime.</summary>
    public bool IsDotNet => Target.IsDotNet;

    /// <summary>Hosting process CPU usage text.</summary>
    public string CpuText => Target.CpuText;

    /// <summary>Hosting process working-set text.</summary>
    public string MemoryText => Target.MemoryText;

    /// <summary>Hosting process start time text.</summary>
    public string StartTimeText => Target.StartTimeText;

    /// <summary>Account of the hosting process.</summary>
    public string UserName => Target.UserName;

    /// <summary>Whether the row is selected.</summary>
    [ObservableProperty] private bool _isSelected;

    /// <summary>Used as the accessible name of the row.</summary>
    public override string ToString() => $"{DisplayName} ({StateText})";

    /// <summary>Finds the service's log files (open handles, config files, log folders) and opens them in tabs.</summary>
    [RelayCommand]
    private void OpenLogs() => _openLogs(Target);

    [RelayCommand(CanExecute = nameof(IsRunning))]
    private void AttachDebugOutput() => _attach(Target, ProcessAttachmentType.DebugOutput);

    [RelayCommand(CanExecute = nameof(IsRunning))]
    private void AttachEventLog() => _attach(Target, ProcessAttachmentType.WindowsEventLog);

    [RelayCommand]
    private void LaunchAndAttach() => _launch();

    [RelayCommand(CanExecute = nameof(CanStart))]
    private void StartService() => Confirm("Start", ServiceAction.Start);

    [RelayCommand(CanExecute = nameof(IsRunning))]
    private void StopService() => Confirm("Stop", ServiceAction.Stop);

    [RelayCommand(CanExecute = nameof(IsRunning))]
    private void RestartService() => Confirm("Restart", ServiceAction.Restart);

    [RelayCommand]
    private void OpenFileLocation()
    {
        if (File.Exists(ExecutablePath))
            Process.Start("explorer.exe", $"/select,\"{ExecutablePath}\"");
    }

    [RelayCommand(CanExecute = nameof(IsRunning))]
    private void CopyPid() => Clipboard.SetText(Info.Pid.ToString());

    [RelayCommand]
    private void CopyPath()
    {
        if (!string.IsNullOrEmpty(ExecutablePath)) Clipboard.SetText(ExecutablePath);
    }

    [RelayCommand]
    private void CopyName() => Clipboard.SetText(Name);

    [RelayCommand(CanExecute = nameof(IsRunning))]
    private void KillProcess()
    {
        // Shared hosts (svchost.exe) carry several services; say so before terminating.
        string shared = string.Equals(Host?.Name, "svchost", StringComparison.OrdinalIgnoreCase)
            ? "\n\nThis is a shared service host: other services running inside it will stop too."
            : string.Empty;

        MessageBoxResult r = MessageBox.Show(
            $"Kill the process hosting '{DisplayName}' ({Host?.DisplayName ?? $"PID {Info.Pid}"})?\n\n" +
            $"This immediately terminates the process and all its children.{shared}",
            "Kill Process", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (r == MessageBoxResult.Yes && Host != null)
            _kill(Host);
    }

    private bool CanStart() => !Info.IsRunning && Info.StartType != ServiceStartType.Disabled;

    private void Confirm(string verb, ServiceAction action)
    {
        MessageBoxResult r = MessageBox.Show(
            $"{verb} service '{DisplayName}' ({Name})?",
            $"{verb} Service", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (r == MessageBoxResult.Yes)
            _control(Info, action);
    }
}

/// <summary>Control operation on a Windows service.</summary>
public enum ServiceAction
{
    /// <summary>Start the service.</summary>
    Start,

    /// <summary>Stop the service.</summary>
    Stop,

    /// <summary>Stop then start the service.</summary>
    Restart
}
