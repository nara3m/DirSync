using System.Diagnostics;
using System.Text.RegularExpressions;

namespace PCNetworkBackup.Core.Services;

/// <summary>
/// Wraps the Windows-built-in Robocopy utility. We deliberately do NOT
/// implement a custom file-copy engine; Robocopy already handles retries,
/// restartable network transfers, long-running mirrors, and timestamp
/// preservation reliably. This class only builds arguments, runs the
/// process, and interprets its (multi-bit) exit code.
/// </summary>
public static class RobocopyService
{
    // Small, documented default exclude list. Not an arbitrary blocklist:
    //   desktop.ini / thumbs.db - Explorer view metadata, regenerated automatically.
    //   ~$*.tmp                 - Office lock files that exist only while a
    //                             document is open on the source; excluding them
    //                             avoids spurious copy/delete churn every cycle.
    public static readonly string[] DefaultExcludeFiles = { "desktop.ini", "thumbs.db", "~$*.tmp" };

    public static string BuildArguments(string source, string destination, bool dryRun, IEnumerable<string>? excludeFiles = null)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append($"\"{source}\" \"{destination}\"");

        // /MIR      - Mirror: makes destination an exact copy of source, including
        //             deletions. This is the core "true one-way mirror" requirement.
        // /Z        - Restartable mode: an interrupted large-file network copy can
        //             resume instead of silently leaving a half-written file that
        //             would be mistaken for a valid backup.
        //             (/ZB, which also uses backup mode, is intentionally avoided:
        //             it needs SeBackupPrivilege, which would force running as admin -
        //             a hard "no" per this project's security requirements.)
        // /R:3 /W:5 - Retry a locked/in-use file 3 times, 5 seconds apart, then move
        //             on. Robocopy's own default (1,000,000 retries / 30s wait) would
        //             make the whole sync hang for a very long time on one locked file.
        // /FFT      - Compare timestamps with 2-second (FAT-style) granularity. Some
        //             network/SMB destinations round timestamps slightly, which
        //             otherwise makes Robocopy think an unchanged file differs and
        //             re-copy it every single cycle.
        // /XJ       - Never follow junctions or symlinks, to avoid copy loops.
        // /NP       - Suppress per-file percentage-progress spam in captured output.
        // /NDL      - Don't list every directory name; keeps summaries readable.
        // /COPY:DAT - Copy Data, Attributes, Timestamps only - NOT security/owner/
        //             auditing info (that would be /COPY:DATSOU and requires elevated
        //             rights). Consistent with "do not require administrator privileges".
        sb.Append(" /MIR /Z /R:3 /W:5 /FFT /XJ /NP /NDL /COPY:DAT");

        if (dryRun)
            sb.Append(" /L"); // List-only: report what would happen, change nothing.

        var excludes = (excludeFiles ?? DefaultExcludeFiles).ToList();
        if (excludes.Count > 0)
        {
            sb.Append(" /XF");
            foreach (var pattern in excludes)
                sb.Append($" \"{pattern}\"");
        }

        return sb.ToString();
    }

    public static RobocopyResult Run(string folderName, string source, string destination, bool dryRun, IEnumerable<string>? excludeFiles = null)
    {
        var args = BuildArguments(source, destination, dryRun, excludeFiles);
        var psi = new ProcessStartInfo
        {
            FileName = "robocopy.exe",
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        var sw = Stopwatch.StartNew();
        string output;
        int exitCode;

        try
        {
            using var process = new Process { StartInfo = psi };
            process.Start();
            output = process.StandardOutput.ReadToEnd();
            output += process.StandardError.ReadToEnd();
            process.WaitForExit();
            exitCode = process.ExitCode;
        }
        catch (Exception ex)
        {
            // robocopy.exe could not even be launched. Treat as a fatal result
            // for this one folder rather than letting the exception propagate
            // and take down the whole scheduled sync.
            sw.Stop();
            return new RobocopyResult
            {
                FolderName = folderName,
                SourcePath = source,
                DestinationPath = destination,
                ExitCode = -1,
                IsFatal = true,
                HasErrors = true,
                Summary = null,
                RawOutput = $"Failed to start robocopy.exe: {ex.Message}",
                Duration = sw.Elapsed,
            };
        }

        sw.Stop();

        // Robocopy exit codes are a bitmask (0-7 = success variants, bit 8 = some
        // failures, bit 16 = serious/fatal error, nothing copied). We must NOT
        // treat every non-zero code as failure - e.g. 3 (1+2) means "files were
        // copied AND extra destination files were removed", which is success.
        bool isFatal = (exitCode & 16) != 0 || exitCode < 0;
        bool hasErrors = isFatal || (exitCode & 8) != 0;

        return new RobocopyResult
        {
            FolderName = folderName,
            SourcePath = source,
            DestinationPath = destination,
            ExitCode = exitCode,
            IsFatal = isFatal,
            HasErrors = hasErrors,
            Summary = ParseSummary(output),
            RawOutput = output,
            Duration = sw.Elapsed,
        };
    }

    /// <summary>
    /// Parses Robocopy's trailing summary block, e.g.:
    ///                    Total    Copied   Skipped  Mismatch    FAILED    Extras
    ///        Dirs :          5         2         3         0         0         0
    ///       Files :        174       174         0         0         0         3
    /// </summary>
    internal static RobocopySummary? ParseSummary(string output)
    {
        var dirsMatch = Regex.Match(output, @"Dirs\s*:\s*(\d+)\s+(\d+)\s+(\d+)\s+(\d+)\s+(\d+)\s+(\d+)");
        var filesMatch = Regex.Match(output, @"Files\s*:\s*(\d+)\s+(\d+)\s+(\d+)\s+(\d+)\s+(\d+)\s+(\d+)");

        if (!dirsMatch.Success && !filesMatch.Success)
            return null;

        static int G(Match m, int i) => m.Success ? int.Parse(m.Groups[i].Value) : 0;

        return new RobocopySummary
        {
            DirsTotal = G(dirsMatch, 1),
            DirsCopied = G(dirsMatch, 2),
            DirsSkipped = G(dirsMatch, 3),
            DirsMismatch = G(dirsMatch, 4),
            DirsFailed = G(dirsMatch, 5),
            DirsExtras = G(dirsMatch, 6),
            FilesTotal = G(filesMatch, 1),
            FilesCopied = G(filesMatch, 2),
            FilesSkipped = G(filesMatch, 3),
            FilesMismatch = G(filesMatch, 4),
            FilesFailed = G(filesMatch, 5),
            FilesExtras = G(filesMatch, 6),
        };
    }
}
