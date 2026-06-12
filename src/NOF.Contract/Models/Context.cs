using System.Collections.ObjectModel;

namespace NOF.Contract;

/// <summary>
/// Immutable execution context passed explicitly across RPC boundaries.
/// </summary>
public class Context
{
    private static readonly IReadOnlyDictionary<object, object?> EmptyItems =
        new ReadOnlyDictionary<object, object?>(new Dictionary<object, object?>());

    protected Context()
        : this(EmptyItems)
    {
    }

    protected Context(IReadOnlyDictionary<object, object?> items)
    {
        Items = items;
    }

    public static Context Empty { get; } = new();

    public static Context FromItems(IReadOnlyDictionary<object, object?> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        if (items.Count == 0)
        {
            return Empty;
        }

        return new Context(CreateReadOnlyItems(items));
    }

    public IReadOnlyDictionary<object, object?> Items { get; }

    public string TenantId { get; protected set; } = string.Empty;

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
        return Clone(CreateReadOnlyItems(items));
    }

    public Context WithItems(IReadOnlyDictionary<object, object?> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        return Clone(CreateReadOnlyItems(items));
    }

    public Context WithTenantId(string? tenantId)
    {
        var cloned = Clone(Items);
        cloned.TenantId = tenantId ?? string.Empty;
        return cloned;
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
        return Clone(CreateReadOnlyItems(items));
    }

    protected virtual Context Clone(IReadOnlyDictionary<object, object?> items)
        => new(items)
        {
            TenantId = TenantId
        };

    private static IReadOnlyDictionary<object, object?> CreateReadOnlyItems(IReadOnlyDictionary<object, object?> items)
    {
        if (items.Count == 0)
        {
            return EmptyItems;
        }

        return new ReadOnlyDictionary<object, object?>(
            new Dictionary<object, object?>(items));
    }
}
