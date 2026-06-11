using System.Collections.ObjectModel;

namespace NOF.Contract;

/// <summary>
/// Immutable execution context passed explicitly across RPC boundaries.
/// </summary>
public sealed class Context
{
    private static readonly IReadOnlyDictionary<object, object?> EmptyItems =
        new ReadOnlyDictionary<object, object?>(new Dictionary<object, object?>());

    private Context(IReadOnlyDictionary<object, object?> items)
    {
        Items = items;
    }

    public static Context Empty { get; } = new(EmptyItems);

    public static Context FromItems(IReadOnlyDictionary<object, object?> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        if (items.Count == 0)
        {
            return Empty;
        }

        return new Context(new ReadOnlyDictionary<object, object?>(
            new Dictionary<object, object?>(items)));
    }

    public IReadOnlyDictionary<object, object?> Items { get; }

    public object? this[object key]
        => TryGetItem(key, out var value)
            ? value
            : null;

    public bool TryGetItem(object key, out object? value)
    {
        ArgumentNullException.ThrowIfNull(key);
        return Items.TryGetValue(key, out value);
    }

    public Context WithItem(object key, object? value)
    {
        ArgumentNullException.ThrowIfNull(key);

        var items = new Dictionary<object, object?>(Items)
        {
            [key] = value
        };
        return FromItems(items);
    }

    public Context WithoutItem(object key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (!Items.ContainsKey(key))
        {
            return this;
        }

        var items = new Dictionary<object, object?>(Items);
        items.Remove(key);
        return FromItems(items);
    }
}
