using PCNetworkBackup.Core.Services;
using Xunit;

namespace PCNetworkBackup.Tests;

public class RobocopyCommandBuilderTests
{
    [Fact]
    public void BuildArguments_IncludesMirrorFlag_ForOneWayMirrorBehavior()
    {
        var args = RobocopyService.BuildArguments(@"C:\src", @"N:\dst", dryRun: false);
        Assert.Contains("/MIR", args);
    }

    [Fact]
    public void BuildArguments_IncludesListOnlyFlag_WhenDryRunRequested()
    {
        var args = RobocopyService.BuildArguments(@"C:\src", @"N:\dst", dryRun: true);
        Assert.Contains("/L", args.Split(' '));
    }

    [Fact]
    public void BuildArguments_OmitsListOnlyFlag_WhenNotDryRun()
    {
        var args = RobocopyService.BuildArguments(@"C:\src", @"N:\dst", dryRun: false);
        Assert.DoesNotContain("/L", args.Split(' '));
    }

    [Fact]
    public void BuildArguments_UsesBoundedRetries_NotRobocopysHugeDefault()
    {
        var args = RobocopyService.BuildArguments(@"C:\src", @"N:\dst", dryRun: false);
        Assert.Contains("/R:3", args);
        Assert.Contains("/W:5", args);
    }

    [Fact]
    public void BuildArguments_CopiesDataAttributesTimestampsOnly_NotSecurityOrOwner()
    {
        var args = RobocopyService.BuildArguments(@"C:\src", @"N:\dst", dryRun: false);
        Assert.Contains("/COPY:DAT", args);
        Assert.DoesNotContain("/COPY:DATSOU", args);
    }

    [Fact]
    public void BuildArguments_UsesRestartableModeNotBackupMode_ToAvoidNeedingAdminRights()
    {
        var args = RobocopyService.BuildArguments(@"C:\src", @"N:\dst", dryRun: false);
        Assert.Contains("/Z", args.Split(' '));
        Assert.DoesNotContain("/ZB", args);
    }

    [Fact]
    public void BuildArguments_IncludesDefaultExcludes_WhenNoneSpecified()
    {
        var args = RobocopyService.BuildArguments(@"C:\src", @"N:\dst", dryRun: false);
        Assert.Contains("desktop.ini", args);
        Assert.Contains("thumbs.db", args);
    }

    [Fact]
    public void BuildArguments_UsesCustomExcludes_WhenProvided()
    {
        var args = RobocopyService.BuildArguments(@"C:\src", @"N:\dst", dryRun: false, new[] { "*.log" });
        Assert.Contains("*.log", args);
        Assert.DoesNotContain("desktop.ini", args);
    }
}
