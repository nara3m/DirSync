namespace PCNetworkBackup.Core.Services;

/// <summary>Simple rotating text log, kept local to the machine (never transmitted anywhere).</summary>
public static class LogService
{
    public static string LogDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PCNetworkBackup", "Logs");

    private const long MaxLogSizeBytes = 5 * 1024 * 1024; // 5 MB
    private const int MaxRotatedFiles = 5;

    public static void Append(string message)
    {
        Directory.CreateDirectory(LogDirectory);
        var path = Path.Combine(LogDirectory, "sync.log");
        RotateIfNeeded(path);
        File.AppendAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
    }

    private static void RotateIfNeeded(string path)
    {
        if (!File.Exists(path) || new FileInfo(path).Length < MaxLogSizeBytes)
            return;

        for (int i = MaxRotatedFiles - 1; i >= 1; i--)
        {
            var src = $"{path}.{i}";
            var dst = $"{path}.{i + 1}";
            if (File.Exists(src))
                File.Copy(src, dst, overwrite: true);
        }

        File.Copy(path, $"{path}.1", overwrite: true);
        File.Delete(path);
    }
}
