using PCNetworkBackup.App.Forms;
using PCNetworkBackup.Core.Services;

namespace PCNetworkBackup.App;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        // The Windows Task Scheduler task invokes this same exe with
        // "--sync" so the background sync and the GUI share one codebase
        // and one publish artifact - no second executable to keep in sync.
        if (args.Contains("--sync"))
        {
            RunHeadlessSync();
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }

    private static void RunHeadlessSync()
    {
        using var guard = new SingleInstanceGuard();
        if (!guard.Acquired)
        {
            LogService.Append("Sync skipped: another sync is already running.");
            return;
        }

        var config = ConfigService.Load();
        if (!config.Enabled)
        {
            LogService.Append("Sync skipped: backup is not enabled.");
            return;
        }

        var validation = SyncEngine.ValidateDestination(config.DestinationDrive);
        if (!validation.IsValid)
        {
            // Quiet retry-on-next-cycle behavior - no popups, no crash, just a log line.
            LogService.Append($"Sync skipped: destination not available ({validation.Message}). Will retry next cycle.");
            return;
        }

        var result = SyncEngine.RunMirror(config);

        config.LastSyncUtc = DateTime.UtcNow;
        ConfigService.Save(config);

        LogService.Append($"Sync complete. Fatal={result.AnyFatal} Errors={result.AnyErrors} Folders={result.Results.Count}");
    }
}
