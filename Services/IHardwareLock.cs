namespace CCCDReaderService.Services;

public interface IHardwareLock
{
    bool IsLocked(string resourceName = "Default");
    IDisposable? TryAcquire(string resourceName = "Default", TimeSpan? timeout = null);
    Task<IDisposable?> TryAcquireAsync(string resourceName = "Default", TimeSpan? timeout = null, CancellationToken cancellationToken = default);
    void ReleaseAll();
}
