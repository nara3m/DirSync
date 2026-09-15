using PCNetworkBackup.Core.Models;

namespace PCNetworkBackup.Core.Services;

/// <summary>One folder's source-to-destination pairing.</summary>
public record FolderMapping(string FolderName, string SourcePath, string DestinationPath);

public record ValidationResult(bool IsValid, string? Message);

public record SyncRunResult(List<RobocopyResult> Results, bool AnyFatal, bool AnyErrors);

public static class SyncEngine
{
    /// <summary>
    /// Builds the C: -> network-drive-root folder pairs for the folders the
    /// user selected. Destinations always sit directly under the drive root
    /// (DRIVE:\FolderName) - never under a "PC-Backup" or similar subfolder,
    /// per this project's explicit requirement.
    /// </summary>
    public static List<FolderMapping> BuildMappings(AppConfig config)
    {
        var mappings = new List<FolderMapping>();
        if (string.IsNullOrWhiteSpace(config.DestinationDrive))
            return mappings;

        var driveRoot = config.DestinationDrive.TrimEnd('\\') + "\\";

        foreach (var folder in config.Folders)
        {
            var source = KnownFolderService.ResolvePath(folder);
            if (source == null)
                continue; // Unknown/unresolvable folder name - skip rather than guess a path.

            var destination = Path.Combine(driveRoot, folder);
            mappings.Add(new FolderMapping(folder, source, destination));
        }

        return mappings;
    }

    /// <summary>
    /// Validates the destination is a real, currently-connected, writable
    /// mapped network drive before any mirroring is allowed to proceed.
    /// </summary>
    public static ValidationResult ValidateDestination(string driveLetter)
    {
        if (string.IsNullOrWhiteSpace(driveLetter))
            return new ValidationResult(false, "No network drive selected.");

        try
        {
            if (!NetworkDriveService.IsNetworkDrive(driveLetter))
                return new ValidationResult(false, $"{driveLetter} is not a mapped network drive.");
        }
        catch (Exception)
        {
            return new ValidationResult(false, $"{driveLetter} is not currently available.");
        }

        var testFile = Path.Combine(driveLetter.TrimEnd('\\') + "\\", $".pcnetworkbackup_test_{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(testFile, "test");
            File.Delete(testFile);
        }
        catch (Exception ex)
        {
            return new ValidationResult(false, $"Cannot write to {driveLetter}: {ex.Message}");
        }

        return new ValidationResult(true, null);
    }

    /// <summary>Safe, non-destructive preview of what a real mirror would do (Robocopy /L).</summary>
    public static List<RobocopyResult> RunDryRun(AppConfig config)
    {
        var mappings = BuildMappings(config);
        var results = new List<RobocopyResult>();
        foreach (var m in mappings)
            results.Add(RobocopyService.Run(m.FolderName, m.SourcePath, m.DestinationPath, dryRun: true, config.ExcludeFiles));
        return results;
    }

    /// <summary>
    /// Runs the real, destructive one-way mirror for every configured folder.
    /// Callers (GUI) are responsible for having already run validation, a
    /// dry-run preview, and obtained explicit user confirmation beforehand -
    /// this method itself does not gate on that, by design, so the headless
    /// scheduled-task path can call it directly once the user has enabled
    /// backup through the GUI.
    /// </summary>
    public static SyncRunResult RunMirror(AppConfig config)
    {
        var mappings = BuildMappings(config);
        var results = new List<RobocopyResult>();

        foreach (var m in mappings)
        {
            try
            {
                Directory.CreateDirectory(m.DestinationPath);
            }
            catch (Exception)
            {
                // Destination folder couldn't be created this cycle (drive
                // disconnected mid-run, permissions changed, etc.) - skip this
                // folder for now; it will be retried on the next scheduled run.
                continue;
            }

            var result = RobocopyService.Run(m.FolderName, m.SourcePath, m.DestinationPath, dryRun: false, config.ExcludeFiles);
            results.Add(result);
            LogService.Append(FormatLogLine(result));
        }

        return new SyncRunResult(
            results,
            AnyFatal: results.Any(r => r.IsFatal),
            AnyErrors: results.Any(r => r.HasErrors));
    }

    private static string FormatLogLine(RobocopyResult r)
    {
        var s = r.Summary;
        var summaryPart = s != null
            ? $"filesCopied={s.FilesCopied} filesDeleted={s.FilesToDelete} filesFailed={s.FilesFailed}"
            : "(no summary parsed)";

        return $"{r.FolderName}: {r.SourcePath} -> {r.DestinationPath} | exit={r.ExitCode} " +
               $"fatal={r.IsFatal} errors={r.HasErrors} duration={r.Duration.TotalSeconds:F1}s {summaryPart}";
    }
}
