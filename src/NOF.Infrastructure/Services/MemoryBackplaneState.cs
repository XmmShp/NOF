using System.Collections.Concurrent;

namespace NOF.Infrastructure;

public sealed class MemoryBackplaneState
{
    internal ConcurrentDictionary<string, ConcurrentDictionary<Guid, MemoryBackplane.Subscription>> Channels { get; }
        = new(StringComparer.Ordinal);
}
