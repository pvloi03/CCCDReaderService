using System.Collections.Concurrent;

namespace CCCDReaderService.Services;

public class HardwareLock : IHardwareLock
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.OrdinalIgnoreCase);

    private SemaphoreSlim GetSemaphore(string resourceName)
    {
        return _locks.GetOrAdd(resourceName, _ => new SemaphoreSlim(1, 1));
    }

    public bool IsLocked(string resourceName = "Default")
    {
        var sem = GetSemaphore(resourceName);
        return sem.CurrentCount == 0;
    }

    public IDisposable? TryAcquire(string resourceName = "Default", TimeSpan? timeout = null)
    {
        var sem = GetSemaphore(resourceName);
        var effectiveTimeout = timeout ?? TimeSpan.Zero;
        var acquired = sem.Wait(effectiveTimeout);

        if (!acquired)
        {
            return null;
        }

        return new LockReleaser(sem);
    }

    public async Task<IDisposable?> TryAcquireAsync(string resourceName = "Default", TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        var sem = GetSemaphore(resourceName);
        var effectiveTimeout = timeout ?? TimeSpan.Zero;
        var acquired = await sem.WaitAsync(effectiveTimeout, cancellationToken);

        if (!acquired)
        {
            return null;
        }

        return new LockReleaser(sem);
    }

    public void ReleaseAll()
    {
        foreach (var sem in _locks.Values)
        {
            if (sem.CurrentCount == 0)
            {
                try
                {
                    sem.Release();
                }
                catch (SemaphoreFullException)
                {
                    // Ignore
                }
            }
        }
    }

    private sealed class LockReleaser : IDisposable
    {
        private SemaphoreSlim? _semaphore;

        public LockReleaser(SemaphoreSlim semaphore)
        {
            _semaphore = semaphore;
        }

        public void Dispose()
        {
            var sem = Interlocked.Exchange(ref _semaphore, null);
            if (sem != null)
            {
                try
                {
                    sem.Release();
                }
                catch (SemaphoreFullException)
                {
                    // Ignore if already released
                }
            }
        }
    }
}
