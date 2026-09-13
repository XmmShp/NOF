using NOF.Abstraction;
using System.Collections.ObjectModel;

namespace NOF.Contract;

/// <summary>
/// Immutable execution context passed explicitly across NOF execution boundaries.
/// </summary>
/// <remarks>
/// String item keys use ordinal, case-insensitive comparison so transported HTTP header names retain their protocol semantics.
/// Non-string keys use their default equality semantics.
/// </remarks>
public class Context
{
    private static readonly AsyncLocal<Context?> AmbientContext = new();
    private static readonly IReadOnlyDictionary<object, object?> EmptyItems =
        new ReadOnlyDictionary<object, object?>(CreateMutableItems());

    protected Context()
        : this(EmptyItems)
    {
    }

    protected Context(IReadOnlyDictionary<object, object?> items)
    {
        Items = CreateReadOnlyItems(items);
    }

    public static Context Empty { get; } = new();

    /// <summary>
    /// Gets the context bound to the current asynchronous control flow.
    /// </summary>
    public static Context Current => AmbientContext.Value ?? Empty;

    public IReadOnlyDictionary<object, object?> Items { get; }

    /// <summary>
    /// Gets the normalized tenant identifier carried by this context.
    /// </summary>
    public string TenantId
        => TryGetItem(NOFAbstractionConstants.Transport.Headers.TenantId, out var value)
            && value is string tenantId
                ? NOFAbstractionConstants.Tenant.NormalizeTenantId(tenantId)
                : NOFAbstractionConstants.Tenant.HostId;

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

        var items = CreateMutableItems(Items);
        items[key] = value;
        return Clone(CreateReadOnlyItems(items));
    }

    /// <summary>
    /// Creates a context carrying the specified normalized tenant identifier.
    /// </summary>
    public Context WithTenantId(string? tenantId)
        => WithItem(
            NOFAbstractionConstants.Transport.Headers.TenantId,
            NOFAbstractionConstants.Tenant.NormalizeTenantId(tenantId));

    /// <summary>
    /// Binds a context to the current asynchronous control flow until the returned scope is disposed.
    /// </summary>
    public static IDisposable PushCurrent(Context context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (ReferenceEquals(AmbientContext.Value, context))
        {
            return EmptyScope.Instance;
        }

        var previous = AmbientContext.Value;
        AmbientContext.Value = context;
        return new AmbientContextScope(previous);
    }

    public Context WithItems(IReadOnlyDictionary<object, object?> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items.Count == 0)
        {
            return this;
        }

        var merged = CreateMutableItems(Items);
        foreach (var item in items)
        {
            merged[item.Key] = item.Value;
        }

        return Clone(CreateReadOnlyItems(merged));
    }

    public Context WithoutItem(object key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (!Items.ContainsKey(key))
        {
            return this;
        }

        var items = CreateMutableItems(Items);
        items.Remove(key);
        return Clone(CreateReadOnlyItems(items));
    }

    protected virtual Context Clone(IReadOnlyDictionary<object, object?> items)
        => new(items);

    private static IReadOnlyDictionary<object, object?> CreateReadOnlyItems(IReadOnlyDictionary<object, object?> items)
    {
        if (items.Count == 0)
        {
            return EmptyItems;
        }

        return new ReadOnlyDictionary<object, object?>(
            CreateMutableItems(items));
    }

    private static Dictionary<object, object?> CreateMutableItems()
        => new(ContextItemKeyComparer.Instance);

    private static Dictionary<object, object?> CreateMutableItems(
        IEnumerable<KeyValuePair<object, object?>> items)
    {
        var result = CreateMutableItems();
        foreach (var item in items)
        {
            result[item.Key] = item.Value;
        }

        return result;
    }

    private sealed class ContextItemKeyComparer : IEqualityComparer<object>
    {
        public static ContextItemKeyComparer Instance { get; } = new();

        public new bool Equals(object? x, object? y)
        {
            if (x is string xString && y is string yString)
            {
                return StringComparer.OrdinalIgnoreCase.Equals(xString, yString);
            }

            if (x is string || y is string)
            {
                return false;
            }

            return EqualityComparer<object>.Default.Equals(x, y);
        }

        public int GetHashCode(object obj)
            => obj is string value
                ? StringComparer.OrdinalIgnoreCase.GetHashCode(value)
                : EqualityComparer<object>.Default.GetHashCode(obj);
    }

    private sealed class AmbientContextScope(Context? previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            AmbientContext.Value = previous;
            _disposed = true;
        }
    }

    private sealed class EmptyScope : IDisposable
    {
        public static EmptyScope Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}
