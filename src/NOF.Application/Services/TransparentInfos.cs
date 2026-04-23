using NOF.Abstraction;
using System.Collections.ObjectModel;

namespace NOF.Application;

/// <summary>
/// Execution context contract used across the application layer to carry ambient metadata (headers, tenant, tracing).
/// </summary>
public interface ITransparentInfos
{
    bool TryGetHeader(string key, out string? value);

    void SetHeader(string key, string? value);

    bool RemoveHeader(string key);

    bool ContainsHeader(string key);

    void Clear();

    IReadOnlyDictionary<string, string?> Snapshot();
}

/// <summary>
/// Default implementation of ITransparentInfos.
/// </summary>
public sealed class TransparentInfos : ITransparentInfos
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

    public void Clear()
    {
        _headers.Clear();
    }

    public IReadOnlyDictionary<string, string?> Snapshot()
    {
        return new ReadOnlyDictionary<string, string?>(
            new Dictionary<string, string?>(_headers, _headers.Comparer));
    }
}

public static partial class TransparentInfosExtensions
{
    extension(ITransparentInfos context)
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

            foreach (var (headerKey, value) in context.Snapshot())
            {
                if (!headers.ContainsKey(headerKey))
                {
                    headers[headerKey] = value;
                }
            }
        }

        public void ReplaceHeadersFrom(IEnumerable<KeyValuePair<string, string?>>? headers)
        {
            context.Clear();
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
