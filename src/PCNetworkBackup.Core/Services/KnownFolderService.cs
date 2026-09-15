using System.Runtime.InteropServices;

namespace PCNetworkBackup.Core.Services;

/// <summary>
/// Resolves the real filesystem path of standard Windows folders
/// (Desktop, Documents, Downloads, Pictures, Music, Videos) for the
/// CURRENT user via the Windows Known Folder API, instead of assuming
/// a hardcoded "C:\Users\{name}\{folder}" layout. This correctly honors
/// folder redirection when Windows/IT policy has configured it.
/// </summary>
public static class KnownFolderService
{
    // Known Folder GUIDs (stable, documented by Microsoft).
    private static readonly Dictionary<string, Guid> FolderIds = new()
    {
        ["Desktop"] = new Guid("B4BFCC3A-DB2C-424C-B029-7FE99A87C641"),
        ["Documents"] = new Guid("FDD39AD0-238F-46AF-ADB4-6C85480369C7"),
        ["Downloads"] = new Guid("374DE290-123F-4565-9164-39C4925E467B"),
        ["Pictures"] = new Guid("33E28130-4E1E-4676-835A-98395C3BC3BB"),
        ["Music"] = new Guid("4BD8D571-6D19-48D3-BE97-422220080E43"),
        ["Videos"] = new Guid("18989B1D-99B5-455B-841C-AB7C74E4DDFC"),
    };

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHGetKnownFolderPath(
        [MarshalAs(UnmanagedType.LPStruct)] Guid rfid,
        uint dwFlags,
        nint hToken,
        out nint pszPath);

    /// <summary>Folder names shown as checkboxes in the GUI, in a stable order.</summary>
    public static IReadOnlyList<string> SupportedFolderNames { get; } =
        new List<string> { "Desktop", "Documents", "Downloads", "Pictures", "Music", "Videos" };

    /// <summary>
    /// Resolves the actual path for a supported folder name. Returns null if the
    /// name isn't recognized or Windows could not resolve it - callers should
    /// skip the folder for this cycle rather than crash or guess a fallback path.
    /// </summary>
    public static string? ResolvePath(string folderName)
    {
        if (!FolderIds.TryGetValue(folderName, out var guid))
            return null;

        int hr = SHGetKnownFolderPath(guid, 0, 0, out var pathPtr);
        if (hr != 0 || pathPtr == 0)
            return null;

        try
        {
            return Marshal.PtrToStringUni(pathPtr);
        }
        finally
        {
            Marshal.FreeCoTaskMem(pathPtr);
        }
    }

    public static string CurrentUserName => Environment.UserName;
}
