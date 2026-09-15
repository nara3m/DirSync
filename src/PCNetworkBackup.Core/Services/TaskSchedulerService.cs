using System.Diagnostics;

namespace PCNetworkBackup.Core.Services;

/// <summary>
/// Registers/removes the recurring Windows Task Scheduler task that runs
/// synchronization in the background, using schtasks.exe (built into Windows
/// since XP - no COM interop dependency needed).
///
/// Deliberately created WITHOUT /RU (run-as user) or /RP (password): this
/// makes the task run under the CURRENT interactive user's own logon
/// session, so it sees the same mapped network drives and permissions the
/// user already has. Running it as SYSTEM would very likely make the
/// mapped drive letter invisible to the task, which is the opposite of
/// what we want, and would also mean storing/handling credentials, which
/// this project's security requirements forbid.
/// </summary>
public static class TaskSchedulerService
{
    public const string TaskName = "PC Network Backup";

    public static (bool Success, string Output) CreateOrUpdateTask(string exePath, int intervalMinutes)
    {
        // /RL LIMITED - run with standard (non-elevated) rights; never request admin.
        // /SC MINUTE /MO n - repeat every n minutes, indefinitely, starting now.
        //   This schedule resumes automatically after reboot once the user logs
        //   back in ("start automatically with Windows" doesn't need a separate
        //   logon trigger for a task that already repeats with no end date).
        var args =
            $"/Create /TN \"{TaskName}\" " +
            $"/TR \"\\\"{exePath}\\\" --sync\" " +
            $"/SC MINUTE /MO {intervalMinutes} /RL LIMITED /F";
        return RunSchtasks(args);
    }

    public static (bool Success, string Output) DeleteTask() =>
        RunSchtasks($"/Delete /TN \"{TaskName}\" /F");

    public static bool TaskExists()
    {
        var (success, _) = RunSchtasks($"/Query /TN \"{TaskName}\"");
        return success;
    }

    private static (bool, string) RunSchtasks(string args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "schtasks.exe",
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        try
        {
            using var process = new Process { StartInfo = psi };
            process.Start();
            string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
            process.WaitForExit();
            return (process.ExitCode == 0, output);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }
}
