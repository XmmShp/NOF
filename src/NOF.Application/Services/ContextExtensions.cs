using NOF.Abstraction;
using NOF.Contract;

namespace NOF.Application;

public static partial class ContextExtensions
{
    private const string HeadersItemKey = "NOF.Context.Headers";

    extension(Context context)
    {
        public IReadOnlyDictionary<string, string?> Headers
            => TryGetHeaders(context, out var headers)
                ? headers
                : EmptyHeaders;

        public bool TryGetHeader(string key, out string? value)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            if (TryGetHeaders(context, out var headers))
            {
                return headers.TryGetValue(key, out value);
            }

            value = null;
            return false;
        }

        public Context WithHeader(string key, string? value)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            var headers = new Dictionary<string, string?>(GetMutableHeaders(context), StringComparer.OrdinalIgnoreCase)
            {
                [key] = value
            };
            return context.WithItem(HeadersItemKey, headers);
        }

        public Context WithoutHeader(string key)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            if (!TryGetHeaders(context, out var headers) || !headers.ContainsKey(key))
            {
                return context;
            }

            var copied = new Dictionary<string, string?>(headers, StringComparer.OrdinalIgnoreCase);
            copied.Remove(key);
            return copied.Count == 0
                ? context.WithoutItem(HeadersItemKey)
                : context.WithItem(HeadersItemKey, copied);
        }

        public Context CopyHeadersFrom(IEnumerable<KeyValuePair<string, string?>>? headers)
        {
            ArgumentNullException.ThrowIfNull(context);
            if (headers is null)
            {
                return context;
            }

            var copied = new Dictionary<string, string?>(GetMutableHeaders(context), StringComparer.OrdinalIgnoreCase);
            foreach (var (headerKey, value) in headers)
            {
                if (string.IsNullOrWhiteSpace(headerKey))
                {
                    continue;
                }

                copied[headerKey] = value;
            }

            return copied.Count == 0
                ? context.WithoutItem(HeadersItemKey)
                : context.WithItem(HeadersItemKey, copied);
        }

        public Context ReplaceHeadersFrom(IEnumerable<KeyValuePair<string, string?>>? headers)
        {
            ArgumentNullException.ThrowIfNull(context);
            return Context.Empty.CopyHeadersFrom(headers);
        }

        public void CopyHeadersTo(IDictionary<string, string?> headers)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(headers);

            foreach (var (headerKey, value) in context.Headers)
            {
                if (!headers.ContainsKey(headerKey))
                {
                    headers[headerKey] = value;
                }
            }
        }

        public string TenantId
            => context.TryGetHeader(NOFAbstractionConstants.Transport.Headers.TenantId, out var tenantId)
                ? NOFAbstractionConstants.Tenant.NormalizeTenantId(tenantId)
                : NOFAbstractionConstants.Tenant.HostId;

        public Context WithTenantId(string? tenantId)
            => context.WithHeader(
                NOFAbstractionConstants.Transport.Headers.TenantId,
                NOFAbstractionConstants.Tenant.NormalizeTenantId(tenantId));
    }

    private static IReadOnlyDictionary<string, string?> EmptyHeaders { get; } =
        new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

    private static bool TryGetHeaders(Context context, out IReadOnlyDictionary<string, string?> headers)
    {
        if (context.TryGetItem(HeadersItemKey, out var item)
            && item is IReadOnlyDictionary<string, string?> readOnlyHeaders)
        {
            headers = readOnlyHeaders;
            return true;
        }

        headers = EmptyHeaders;
        return false;
    }

    private static Dictionary<string, string?> GetMutableHeaders(Context context)
        => TryGetHeaders(context, out var headers)
            ? new Dictionary<string, string?>(headers, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
}
