using System.Text.Json.Serialization;

namespace PCNetworkBackup.Core.Models;

/// <summary>
/// Persisted application configuration. Stored as plain JSON under the
/// user's AppData folder. Deliberately contains NO passwords or network
/// credentials - the app only ever relies on the Windows user's already
/// existing session/permissions to reach the mapped network drive.
/// </summary>
public class AppConfig
{
    /// <summary>Selected mapped drive letter, e.g. "N:"</summary>
    public string DestinationDrive { get; set; } = string.Empty;

    /// <summary>Underlying UNC path behind the drive letter, e.g. \\FileServer\John</summary>
    public string DestinationUnc { get; set; } = string.Empty;

    /// <summary>Selected known-folder names, e.g. ["Desktop", "Documents"]</summary>
    public List<string> Folders { get; set; } = new();

    public int IntervalMinutes { get; set; } = 10;

    public bool Enabled { get; set; } = false;

    public bool StartWithWindows { get; set; } = true;

    public bool FirstRunCompleted { get; set; } = false;

    public DateTime? LastSyncUtc { get; set; }

    /// <summary>
    /// Small, documented default exclusion list for known noise files that are
    /// safe to skip in a "current state" mirror (see RobocopyService for why).
    /// Configurable so it never silently grows without the user's knowledge.
    /// </summary>
    public List<string> ExcludeFiles { get; set; } = new() { "desktop.ini", "thumbs.db", "~$*.tmp" };

    [JsonIgnore]
    public static readonly int[] AllowedIntervals = { 5, 10, 15, 30, 60, 120 };
}
