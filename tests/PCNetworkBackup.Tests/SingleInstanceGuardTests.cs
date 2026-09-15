using PCNetworkBackup.Core.Services;
using Xunit;

namespace PCNetworkBackup.Tests;

public class SingleInstanceGuardTests
{
    [Fact]
    public void SecondGuard_CannotAcquire_WhileFirstIsHeld()
    {
        var name = $"Global\\PCNB_Test_{Guid.NewGuid():N}";
        using var first = new SingleInstanceGuard(name);
        using var second = new SingleInstanceGuard(name);

        Assert.True(first.Acquired);
        Assert.False(second.Acquired);
    }

    [Fact]
    public void Guard_CanBeReacquired_AfterPreviousOneDisposed()
    {
        var name = $"Global\\PCNB_Test_{Guid.NewGuid():N}";
        using (var first = new SingleInstanceGuard(name))
            Assert.True(first.Acquired);

        using var second = new SingleInstanceGuard(name);
        Assert.True(second.Acquired);
    }
}
