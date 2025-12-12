using System.Diagnostics.CodeAnalysis;

namespace NOF;

/// <summary>
/// Default implementation of <see cref="INOFAppMetadata"/>.
/// Not thread-safe—intended for use during single-threaded application configuration.
/// </summary>
internal class NOFAppMetadata : INOFAppMetadata
{
    private readonly Dictionary<string, object?> _data = new();

    public void Set<T>(string name, T value)
    {
        ArgumentNullException.ThrowIfNull(name);
        _data[name] = value;
    }

    public T Get<T>(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (!_data.TryGetValue(name, out var value))
        {
            throw new KeyNotFoundException($"Metadata key '{name}' not found.");
        }

        if (value is not T typedValue)
        {
            throw new InvalidCastException(
                $"Metadata value for key '{name}' cannot be cast to type '{typeof(T)}'.");
        }

        return typedValue;
    }

    public bool TryGet<T>(string name, [MaybeNullWhen(false)] out T value)
    {
        ArgumentNullException.ThrowIfNull(name);
        value = default;

        if (!_data.TryGetValue(name, out var obj) || obj is not T typedValue)
        {
            return false;
        }

        value = typedValue;
        return true;
    }

    public bool ContainsKey(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _data.ContainsKey(name);
    }

    public bool Remove(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _data.Remove(name);
    }

    public void Clear()
    {
        _data.Clear();
    }

    public IReadOnlyCollection<string> Keys => _data.Keys.ToList().AsReadOnly();
}