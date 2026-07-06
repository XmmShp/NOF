using NOF.Contract;

namespace NOF.Authentication;

public static class AuthenticationContextExtensions
{
    extension(Context context)
    {
        public Context WithServiceToken(string? headerName)
        {
            ArgumentNullException.ThrowIfNull(context);

            return string.IsNullOrWhiteSpace(headerName)
                ? context.WithoutItem(AuthenticationContextKeys.ServiceTokenHeader)
                : context.WithItem(AuthenticationContextKeys.ServiceTokenHeader, headerName);
        }

        public Context WithTokenExchange(params string?[]? headerNames)
        {
            ArgumentNullException.ThrowIfNull(context);

            var normalizedHeaderNames = NormalizeHeaderNames(headerNames);
            return normalizedHeaderNames.Count == 0
                ? context.WithoutItem(AuthenticationContextKeys.TokenExchangeHeaders)
                : context.WithItem(AuthenticationContextKeys.TokenExchangeHeaders, normalizedHeaderNames);
        }

        public string? GetServiceTokenHeaderName()
        {
            ArgumentNullException.ThrowIfNull(context);

            return context.TryGetItem(AuthenticationContextKeys.ServiceTokenHeader, out var value)
                ? value as string
                : null;
        }

        public IReadOnlySet<string> GetTokenExchangeHeaderNames()
        {
            ArgumentNullException.ThrowIfNull(context);

            if (!context.TryGetItem(AuthenticationContextKeys.TokenExchangeHeaders, out var value)
                || value is null)
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            return value as HashSet<string>
                ?? throw new InvalidOperationException("Context token exchange headers must be stored as HashSet<string>.");
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
