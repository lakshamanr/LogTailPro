namespace LogTailPro.Models;

/// <summary>Determines where application preferences are persisted.</summary>
public enum PreferenceStorage
{
    /// <summary>Save to %AppData%\LogTailPro\settings.json (default).</summary>
    File,
    /// <summary>Save to HKCU\Software\LogTailPro in the Windows registry.</summary>
    Registry,
    /// <summary>Do not save on exit — always starts with defaults.</summary>
    None
}
