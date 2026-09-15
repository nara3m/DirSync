using System.Reflection;
using PCNetworkBackup.Core.Services;
using Xunit;

namespace PCNetworkBackup.Tests;

public class RobocopySummaryParsingTests
{
    // ParseSummary is internal to the Core assembly; tests reach it via reflection
    // so the parser can be exercised without spinning up a real robocopy.exe process.
    private static RobocopySummary? ParseSummary(string output)
    {
        var method = typeof(RobocopyService).GetMethod("ParseSummary", BindingFlags.NonPublic | BindingFlags.Static);
        return (RobocopySummary?)method!.Invoke(null, new object[] { output });
    }

    [Fact]
    public void ParsesTypicalRobocopySummaryBlock()
    {
        var output =
            "               Total    Copied   Skipped  Mismatch    FAILED    Extras\r\n" +
            "    Dirs :          5         2         3         0         0         0\r\n" +
            "   Files :        174       174         0         0         0         3\r\n" +
            "   Bytes :   12.4 m    12.4 m         0         0         0         0\r\n";

        var summary = ParseSummary(output);

        Assert.NotNull(summary);
        Assert.Equal(174, summary!.FilesCopied);
        Assert.Equal(3, summary.FilesToDelete); // Extras column, under /MIR = destination-only files removed
        Assert.Equal(0, summary.FilesFailed);
    }

    [Fact]
    public void ReturnsNull_WhenOutputHasNoRecognizableSummary()
    {
        var summary = ParseSummary("robocopy.exe not found or produced unexpected output");
        Assert.Null(summary);
    }
}
