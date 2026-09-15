namespace PCNetworkBackup.Core.Services;

/// <summary>
/// Prevents two synchronization runs (a scheduled run and a manual
/// "Sync Now", or two overlapping scheduled runs) from mirroring the same
/// destination at the same time.
/// </summary>
public sealed class SingleInstanceGuard : IDisposable
{
    private readonly Mutex _mutex;
    private bool _acquired;

    public SingleInstanceGuard(string name = "Global\\PCNetworkBackup_SyncMutex")
    {
        _mutex = new Mutex(initiallyOwned: false, name, out _);
        try
        {
            _acquired = _mutex.WaitOne(TimeSpan.Zero);
        }
        catch (AbandonedMutexException)
        {
            // A previous run crashed while holding the mutex. The mutex object
            // itself is still valid; this process now owns it.
            _acquired = true;
        }
    }

    public bool Acquired => _acquired;

    public void Dispose()
    {
        if (_acquired)
        {
            _mutex.ReleaseMutex();
            _acquired = false;
        }
        _mutex.Dispose();
    }
}
