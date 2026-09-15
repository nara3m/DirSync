using PCNetworkBackup.Core.Services;
using Xunit;
using System.Threading.Tasks;

namespace PCNetworkBackup.Tests;

public class SingleInstanceGuardTests
{
    [Fact]
    public async Task SecondGuard_CannotAcquire_WhileFirstIsHeld()
    {
        var name = $"Global\\PCNB_Test_{Guid.NewGuid():N}";
        using var first = new SingleInstanceGuard(name);
        
        // Await the background task instead of blocking the thread
        var secondAcquired = await Task.Run(() => 
        {
            using var second = new SingleInstanceGuard(name);
            return second.Acquired;
        });

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
