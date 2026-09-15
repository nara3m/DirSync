namespace PCNetworkBackup.Core.Services;

/// <summary>Parsed counts from Robocopy's summary table.</summary>
public class RobocopySummary
{
    public int DirsTotal, DirsCopied, DirsSkipped, DirsMismatch, DirsFailed, DirsExtras;
    public int FilesTotal, FilesCopied, FilesSkipped, FilesMismatch, FilesFailed, FilesExtras;

    /// <summary>
    /// Under /MIR, "Extras" = present in destination but not in source.
    /// In a real run these are the files Robocopy deletes; in a /L dry run
    /// these are the files that WOULD be deleted.
    /// </summary>
    public int FilesToDelete => FilesExtras;
}

/// <summary>Outcome of running Robocopy for a single mirrored folder.</summary>
public class RobocopyResult
{
    public required string FolderName { get; init; }
    public required string SourcePath { get; init; }
    public required string DestinationPath { get; init; }
    public int ExitCode { get; init; }

    /// <summary>Robocopy copied nothing at all due to a serious error (bit 16).</summary>
    public bool IsFatal { get; init; }

    /// <summary>Some failures occurred (bit 8 and/or bit 16) - not necessarily fatal.</summary>
    public bool HasErrors { get; init; }

    public RobocopySummary? Summary { get; init; }
    public string RawOutput { get; init; } = string.Empty;
    public TimeSpan Duration { get; init; }
}
