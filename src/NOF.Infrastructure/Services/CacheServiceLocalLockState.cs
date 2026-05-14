using System.Collections.Concurrent;

namespace NOF.Infrastructure;

public sealed class CacheServiceLocalLockState : IDisposable
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.Ordinal);

    public SemaphoreSlim GetOrAdd(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
    }

    public void Dispose()
    {
        foreach (var semaphore in _locks.Values)
        {
            semaphore.Dispose();
        }

        _locks.Clear();
    }
}
