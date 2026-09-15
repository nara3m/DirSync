using PCNetworkBackup.Core.Services;
using Xunit;

namespace PCNetworkBackup.Tests;

/// <summary>
/// Exercises RobocopyService end-to-end (real robocopy.exe process, since
/// this runs on the Windows CI runner) but ONLY against throwaway temp
/// directories - never against real user profile folders or a real
/// network drive. This is the safe stand-in for "integration test mode"
/// described in the project spec.
/// </summary>
public class SyncEngineIntegrationTests : IDisposable
{
    private readonly string _root;
    private readonly string _source;
    private readonly string _destination;

    public SyncEngineIntegrationTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"pcnb_it_{Guid.NewGuid():N}");
        _source = Path.Combine(_root, "source");
        _destination = Path.Combine(_root, "destination");
        Directory.CreateDirectory(_source);
        Directory.CreateDirectory(_destination);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort cleanup */ }
    }

    [Fact]
    public void Mirror_CopiesNewFile_FromSourceToDestination()
    {
        File.WriteAllText(Path.Combine(_source, "test.txt"), "hello");

        var result = RobocopyService.Run("TestFolder", _source, _destination, dryRun: false);

        Assert.False(result.IsFatal);
        Assert.True(File.Exists(Path.Combine(_destination, "test.txt")));
    }

    [Fact]
    public void Mirror_DeletesFile_WhenRemovedFromSource_BecauseThisIsATrueMirrorNotAnArchive()
    {
        File.WriteAllText(Path.Combine(_source, "keep.txt"), "keep");
        File.WriteAllText(Path.Combine(_source, "removeMe.txt"), "bye");
        RobocopyService.Run("TestFolder", _source, _destination, dryRun: false);

        File.Delete(Path.Combine(_source, "removeMe.txt"));
        var result = RobocopyService.Run("TestFolder", _source, _destination, dryRun: false);

        Assert.False(result.IsFatal);
        Assert.True(File.Exists(Path.Combine(_destination, "keep.txt")));
        Assert.False(File.Exists(Path.Combine(_destination, "removeMe.txt")));
    }

    [Fact]
    public void DryRun_ReportsWouldCopy_ButDoesNotActuallyCopyAnything()
    {
        File.WriteAllText(Path.Combine(_source, "preview.txt"), "preview only");

        var result = RobocopyService.Run("TestFolder", _source, _destination, dryRun: true);

        Assert.False(File.Exists(Path.Combine(_destination, "preview.txt")));
        Assert.NotNull(result.Summary);
        Assert.True(result.Summary!.FilesCopied >= 1);
    }

    [Fact]
    public void Mirror_NeverModifiesSourceFiles()
    {
        var sourceFile = Path.Combine(_source, "untouched.txt");
        File.WriteAllText(sourceFile, "original content");
        var beforeWriteTime = File.GetLastWriteTimeUtc(sourceFile);

        RobocopyService.Run("TestFolder", _source, _destination, dryRun: false);

        Assert.Equal("original content", File.ReadAllText(sourceFile));
        Assert.Equal(beforeWriteTime, File.GetLastWriteTimeUtc(sourceFile));
    }
}
