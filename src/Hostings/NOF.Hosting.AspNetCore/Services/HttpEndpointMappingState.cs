using System.Threading;

namespace NOF.Hosting.AspNetCore;

internal sealed class HttpEndpointMappingState
{
    private readonly Lock _lock = new();
    private readonly HashSet<string> _mappedKeys = [];

    public bool TryMarkMapped(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        lock (_lock)
        {
            return _mappedKeys.Add(key);
        }
    }
}
