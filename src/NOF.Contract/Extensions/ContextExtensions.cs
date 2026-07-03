namespace NOF.Contract;

public static partial class ContextExtensions
{
    public static object ServiceTokenHeaderKey { get; } = new();

    public static object TokenExchangeHeaderKey { get; } = new();

    extension(Context context)
    {
        public Context WithServiceToken(string? headerName)
        {
            ArgumentNullException.ThrowIfNull(context);

            return string.IsNullOrWhiteSpace(headerName)
                ? context.WithoutItem(ServiceTokenHeaderKey)
                : context.WithItem(ServiceTokenHeaderKey, headerName);
        }

        public Context WithTokenExchange(params string?[]? headerNames)
        {
            ArgumentNullException.ThrowIfNull(context);

            var normalizedHeaderNames = NormalizeHeaderNames(headerNames);
            return normalizedHeaderNames.Count == 0
                ? context.WithoutItem(TokenExchangeHeaderKey)
                : context.WithItem(TokenExchangeHeaderKey, normalizedHeaderNames);
        }

        public string? GetServiceTokenHeaderName()
        {
            ArgumentNullException.ThrowIfNull(context);

            return context.TryGetItem(ServiceTokenHeaderKey, out var value)
                ? value as string
                : null;
        }

        public IReadOnlySet<string> GetTokenExchangeHeaderNames()
        {
            ArgumentNullException.ThrowIfNull(context);

            if (!context.TryGetItem(TokenExchangeHeaderKey, out var value)
                || value is null)
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            return value as HashSet<string>
                ?? throw new InvalidOperationException("Context token exchange headers must be stored as HashSet<string>.");
        }

        public Context CopyHeadersFrom(IEnumerable<KeyValuePair<string, string?>>? headers)
        {
            ArgumentNullException.ThrowIfNull(context);
            if (headers is null)
            {
                return context;
            }

            var items = new Dictionary<object, object?>(context.Items);
            foreach (var (headerKey, value) in headers)
            {
                if (string.IsNullOrWhiteSpace(headerKey))
                {
                    continue;
                }

                items[headerKey] = value;
            }

            return context.WithItems(items);
        }

        public Context ReplaceHeadersFrom(IEnumerable<KeyValuePair<string, string?>>? headers)
        {
            ArgumentNullException.ThrowIfNull(context);
            if (headers is null)
            {
                return Context.Empty;
            }

            var items = new Dictionary<object, object?>();
            foreach (var (headerKey, value) in headers)
            {
                if (string.IsNullOrWhiteSpace(headerKey))
                {
                    continue;
                }

                items[headerKey] = value;
            }

            return Context.Empty.WithItems(items);
        }
    }

    private static HashSet<string> NormalizeHeaderNames(IEnumerable<string?>? headerNames)
    {
        if (headerNames is null)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var normalizedHeaderNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var headerName in headerNames)
        {
            if (string.IsNullOrWhiteSpace(headerName))
            {
                continue;
            }

            normalizedHeaderNames.Add(headerName);
        }

        return normalizedHeaderNames;
    }
}
