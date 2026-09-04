using System.Collections.Concurrent;

namespace NOF.Infrastructure;

/// <summary>
/// Holds host-local state for the in-memory object storage rider.
/// </summary>
public sealed class MemoryObjectStorageRiderState : IDisposable
{
    internal ConcurrentDictionary<MemoryObjectStorageRider.ObjectIdentifier, MemoryObjectStorageRider.StoredObject> Objects { get; }
        = new();

    /// <inheritdoc />
    public void Dispose()
    {
        Objects.Clear();
        GC.SuppressFinalize(this);
    }
}
