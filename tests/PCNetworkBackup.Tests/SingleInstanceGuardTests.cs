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
        
        // Push the second guard to a background thread so the Mutex 
        // correctly recognizes it as a separate, competing requester.
        var secondAcquired = Task.Run(() => 
        {
            using var second = new SingleInstanceGuard(name);
            return second.Acquired;
        }).GetAwaiter().GetResult();

        Assert.True(first.Acquired);
        Assert.False(secondAcquired);
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
