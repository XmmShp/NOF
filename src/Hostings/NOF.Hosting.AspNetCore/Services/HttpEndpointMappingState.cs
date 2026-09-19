namespace NOF.Hosting.AspNetCore;
/// <summary>Tracks HTTP endpoint registration state for a host.</summary>

public sealed class HttpEndpointMappingState
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
