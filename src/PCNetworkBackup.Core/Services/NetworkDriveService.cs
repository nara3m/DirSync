using System.Runtime.InteropServices;
using System.Text;

namespace PCNetworkBackup.Core.Services;

/// <summary>A mapped network drive as shown to the user, e.g. N: -> \\FileServer\John</summary>
public record MappedDrive(string DriveLetter, string UncPath);

/// <summary>
/// Enumerates the current user's mapped network drives so the GUI never
/// has to hardcode a drive letter, and resolves the underlying UNC path
/// behind a drive letter (drive letters are per-session; the UNC path is
/// the more durable identity of "where does this actually point").
/// </summary>
public static class NetworkDriveService
{
    [DllImport("mpr.dll", CharSet = CharSet.Unicode)]
    private static extern int WNetGetConnection(string lpLocalName, StringBuilder lpRemoteName, ref int lpnLength);

    public static List<MappedDrive> GetMappedDrives()
    {
        var result = new List<MappedDrive>();

        foreach (var drive in DriveInfo.GetDrives())
        {
            bool isNetwork;
            try
            {
                isNetwork = drive.DriveType == DriveType.Network;
            }
            catch (IOException)
            {
                // A drive letter can be mapped but momentarily unreachable
                // (e.g. VPN just dropped). Skip it rather than throw.
                continue;
            }

            if (!isNetwork)
                continue;

            var letter = drive.Name.TrimEnd('\\'); // "N:\" -> "N:"
            var unc = TryGetUncPath(letter) ?? "(share path unavailable)";
            result.Add(new MappedDrive(letter, unc));
        }

        return result;
    }

    public static string? TryGetUncPath(string driveLetter)
    {
        var sb = new StringBuilder(512);
        int size = sb.Capacity;
        int result = WNetGetConnection(driveLetter, sb, ref size);
        return result == 0 ? sb.ToString() : null;
    }

    /// <summary>True only if the given drive letter is currently a mapped network drive.</summary>
    public static bool IsNetworkDrive(string driveLetter)
    {
        var di = new DriveInfo(driveLetter);
        return di.DriveType == DriveType.Network;
    }
}
