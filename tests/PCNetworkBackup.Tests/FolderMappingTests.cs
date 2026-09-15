using PCNetworkBackup.Core.Models;
using PCNetworkBackup.Core.Services;
using Xunit;

namespace PCNetworkBackup.Tests;

public class FolderMappingTests
{
    [Fact]
    public void BuildMappings_SkipsFolder_WhenNameIsUnrecognized()
    {
        // "NotARealFolder" isn't a name KnownFolderService understands, so it
        // can't resolve a source path. This guards against a corrupted/hand-
        // edited config.json referencing a bogus folder name: we skip it
        // silently rather than crash the whole sync.
        var config = new AppConfig { DestinationDrive = "N:", Folders = new List<string> { "NotARealFolder" } };
        var mappings = SyncEngine.BuildMappings(config);
        Assert.Empty(mappings);
    }

    [Fact]
    public void BuildMappings_ReturnsEmpty_WhenNoDriveSelected()
    {
        var config = new AppConfig { DestinationDrive = "", Folders = new List<string> { "Documents" } };
        var mappings = SyncEngine.BuildMappings(config);
        Assert.Empty(mappings);
    }

    [Fact]
    public void BuildMappings_PutsDestinationDirectlyUnderDriveRoot_NoParentFolder()
    {
        // Runs on the Windows CI runner, where KnownFolderService can really
        // resolve "Documents" via the Win32 API. This is the single most
        // important layout rule in the whole project: NEVER N:\PC-Backup\...
        var config = new AppConfig { DestinationDrive = "N:", Folders = new List<string> { "Documents" } };
        var mappings = SyncEngine.BuildMappings(config);

        Assert.Single(mappings);
        Assert.Equal(@"N:\Documents", mappings[0].DestinationPath);
        Assert.DoesNotContain("PC-Backup", mappings[0].DestinationPath);
    }
}
