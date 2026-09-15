using PCNetworkBackup.Core.Services;
using Xunit;
using System.Threading.Tasks;

namespace PCNetworkBackup.Tests;

public class SingleInstanceGuardTests
{
    [Fact]
    public void SecondGuard_CannotAcquire_WhileFirstIsHeld()
    {
        var name = $"Global\\PCNB_Test_{Guid.NewGuid():N}";
        
        // This is acquired on the main test thread
        using var first = new SingleInstanceGuard(name);

        bool secondAcquired = true; 

        // Spin up a raw OS thread. This acts as our "competing process" 
        // without triggering xUnit's async Task analyzer warnings.
        var backgroundThread = new Thread(() => 
        {
            using var second = new SingleInstanceGuard(name);
            secondAcquired = second.Acquired;
        });

        backgroundThread.Start();
        backgroundThread.Join(); // Wait for the thread to finish

        Assert.True(first.Acquired);
        Assert.False(secondAcquired);
        
        // When the test ends, 'first' is disposed safely on the exact 
        // same main test thread that created it.
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
