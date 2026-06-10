using NOF.Abstraction;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

namespace NOF.Application;

/// <summary>
/// Unified ambient context used to carry execution metadata across the current scope.
/// </summary>
public sealed class NOFContext
{
    private readonly Dictionary<string, string?> _headers = new(StringComparer.OrdinalIgnoreCase);

    public bool TryGetHeader(string key, out string? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _headers.TryGetValue(key, out value);
    }

    public void SetHeader(string key, string? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _headers[key] = value;
    }

    public bool RemoveHeader(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _headers.Remove(key);
    }

    public bool ContainsHeader(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _headers.ContainsKey(key);
    }

    public void ClearHeaders()
    {
        _headers.Clear();
    }

    public IReadOnlyDictionary<string, string?> SnapshotHeaders()
    {
        return new ReadOnlyDictionary<string, string?>(
            new Dictionary<string, string?>(_headers, _headers.Comparer));
    }
}

public interface IContextAccessor
{
    NOFContext? Context { get; set; }
}

public sealed class ContextAccessor : IContextAccessor
{
    private static readonly AsyncLocal<ContextHolder?> CurrentContext = new();

    public NOFContext? Context
    {
        get => CurrentContext.Value?.Context;
        set
        {
            var holder = CurrentContext.Value;
            if (holder is not null)
            {
                holder.Context = null;
            }

            if (value is not null)
            {
                CurrentContext.Value = new ContextHolder
                {
                    Context = value
                };
            }
        }
    }

    private sealed class ContextHolder
    {
        public NOFContext? Context { get; set; }
    }
}

public static class AmbientContext
{
    public static IDisposable PushCurrent(IContextAccessor accessor, NOFContext context)
    {
        ArgumentNullException.ThrowIfNull(accessor);
        ArgumentNullException.ThrowIfNull(context);

        var previous = accessor.Context;
        accessor.Context = context;
        return new AmbientContextScope(accessor, previous);
    }

    public static IDisposable PushCurrent(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var accessor = services.GetRequiredService<IContextAccessor>();
        var context = services.GetRequiredService<NOFContext>();
        return PushCurrent(accessor, context);
    }

    private sealed class AmbientContextScope(IContextAccessor accessor, NOFContext? previousContext) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            accessor.Context = previousContext;
            _disposed = true;
        }
    }
}

public static partial class NOFContextExtensions
{
    extension(NOFContext context)
    {
        public void CopyHeadersFrom(IEnumerable<KeyValuePair<string, string?>>? headers)
        {
            if (headers is null)
            {
                return;
            }

            foreach (var (headerKey, value) in headers)
            {
                context.SetHeader(headerKey, value);
            }
        }

        public void CopyHeadersTo(IDictionary<string, string?> headers)
        {
            ArgumentNullException.ThrowIfNull(headers);

            foreach (var (headerKey, value) in context.SnapshotHeaders())
            {
                if (!headers.ContainsKey(headerKey))
                {
                    headers[headerKey] = value;
                }
            }
        }

        public void ReplaceHeadersFrom(IEnumerable<KeyValuePair<string, string?>>? headers)
        {
            context.ClearHeaders();
            context.CopyHeadersFrom(headers);
        }

        public string TenantId
        {
            get
            {
                context.TryGetHeader(NOFAbstractionConstants.Transport.Headers.TenantId, out var tenantId);
                return NOFAbstractionConstants.Tenant.NormalizeTenantId(tenantId);
            }
            set
            {
                context.SetHeader(NOFAbstractionConstants.Transport.Headers.TenantId, NOFAbstractionConstants.Tenant.NormalizeTenantId(value));
            }
        }
    }
}
