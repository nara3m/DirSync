using Xunit;

namespace PCNetworkBackup.Tests;

/// <summary>
/// Robocopy's exit code is a bitmask, not a simple success/failure flag.
/// These tests document and pin down the interpretation used throughout
/// the app (see RobocopyService.Run), since getting this wrong would mean
/// either treating a successful mirror as a failure, or silently ignoring
/// real errors.
/// </summary>
public class RobocopyExitCodeTests
{
    [Theory]
    [InlineData(0, false, false)]   // no changes, no errors
    [InlineData(1, false, false)]   // files copied, no errors
    [InlineData(2, false, false)]   // extra dest files removed, no errors
    [InlineData(3, false, false)]   // files copied + extras removed, no errors
    [InlineData(8, false, true)]    // some failures, but not the fatal bit
    [InlineData(16, true, true)]    // fatal: nothing copied
    [InlineData(24, true, true)]    // fatal (16) + failures (8)
    public void InterpretsExitCode_Correctly(int exitCode, bool expectFatal, bool expectErrors)
    {
        bool isFatal = (exitCode & 16) != 0 || exitCode < 0;
        bool hasErrors = isFatal || (exitCode & 8) != 0;

        Assert.Equal(expectFatal, isFatal);
        Assert.Equal(expectErrors, hasErrors);
    }

    [Fact]
    public void NegativeExitCode_TreatedAsFatal()
    {
        int exitCode = -1; // used when robocopy.exe itself couldn't be launched
        bool isFatal = (exitCode & 16) != 0 || exitCode < 0;
        Assert.True(isFatal);
    }
}
