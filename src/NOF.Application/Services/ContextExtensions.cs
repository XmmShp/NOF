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

        public Context WithTokenExchange(string? headerName)
        {
            ArgumentNullException.ThrowIfNull(context);

            return string.IsNullOrWhiteSpace(headerName)
                ? context.WithoutItem(TokenExchangeHeaderKey)
                : context.WithItem(TokenExchangeHeaderKey, headerName);
        }

        public string? GetServiceTokenHeaderName()
        {
            ArgumentNullException.ThrowIfNull(context);

            return context.TryGetItem(ServiceTokenHeaderKey, out var value)
                ? value as string
                : null;
        }

        public string? GetTokenExchangeHeaderName()
        {
            ArgumentNullException.ThrowIfNull(context);

            return context.TryGetItem(TokenExchangeHeaderKey, out var value)
                ? value as string
                : null;
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
}
