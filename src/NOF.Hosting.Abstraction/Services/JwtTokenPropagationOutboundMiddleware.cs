using NOF.Abstraction;
using Microsoft.Extensions.Logging;
using NOF.Contract;

namespace NOF.Hosting;

/// <summary>Propagates or exchanges JWT tokens for outbound RPC requests.</summary>
public sealed class JwtTokenPropagationOutboundMiddleware : IRequestOutboundMiddleware
{
    public TopologyComparison Compare(IRequestOutboundMiddleware other) => TopologyComparison.DoesNotMatter;

    private readonly IUserContext _userContext;
    private readonly IJwtTokenExchangeService? _tokenExchangeService;
    private readonly ILogger<JwtTokenPropagationOutboundMiddleware> _logger;

    public JwtTokenPropagationOutboundMiddleware(
        IUserContext userContext,
        ILogger<JwtTokenPropagationOutboundMiddleware> logger,
        IJwtTokenExchangeService? tokenExchangeService = null)
    {
        _userContext = userContext;
        _tokenExchangeService = tokenExchangeService;
        _logger = logger;
    }

    public async ValueTask InvokeAsync(RequestOutboundContext context, object request, RequestOutboundHandlerDelegate next, CancellationToken cancellationToken)
    {
        await PropagateAsync(context, context.Headers, cancellationToken).ConfigureAwait(false);
        await next(context, request, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask PropagateAsync(
        Context context,
        IDictionary<string, string?> headers,
        CancellationToken cancellationToken)
    {
        foreach (var identity in _userContext.User.Identities.OfType<JwtClaimsIdentity>()
            .Where(identity => identity.DownstreamPropagation is not null)
            .Where(identity => identity.Token.Length > 0))
        {
            var propagation = identity.DownstreamPropagation!;
            var token = propagation.EnableTokenExchange
                ? await ExchangeTokenAsync(identity.Token, propagation, cancellationToken).ConfigureAwait(false)
                : identity.Token;
            WriteHeader(headers, propagation.HeaderName, FormatHeaderValue(propagation, token));
        }

        await ExchangeExplicitTokenAsync(context, headers, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask ExchangeExplicitTokenAsync(
        Context context,
        IDictionary<string, string?> headers,
        CancellationToken cancellationToken)
    {
        var headerNames = context.GetTokenExchangeHeaderNames();
        if (headerNames.Count == 0)
        {
            return;
        }

        var identity = _userContext.User.Identities.OfType<JwtClaimsIdentity>()
            .FirstOrDefault(candidate => candidate.Token.Length > 0);
        if (identity is null)
        {
            throw new InvalidOperationException("Context.WithTokenExchange(...) requires a JwtClaimsIdentity with a non-empty token.");
        }

        var exchangedToken = await ExchangeTokenAsync(
            identity.Token,
            identity.DownstreamPropagation ?? new JwtPropagation(),
            cancellationToken).ConfigureAwait(false);
        foreach (var headerName in headerNames)
        {
            WriteHeader(headers, headerName, "Bearer " + exchangedToken);
        }
    }

    private async ValueTask<string> ExchangeTokenAsync(
        string subjectToken,
        JwtPropagation propagation,
        CancellationToken cancellationToken)
    {
        var tokenExchangeService = _tokenExchangeService
            ?? throw new InvalidOperationException("JWT token exchange requires IJwtTokenExchangeService to be registered.");
        return await tokenExchangeService.ExchangeTokenAsync(subjectToken, propagation, cancellationToken).ConfigureAwait(false);
    }

    private static string FormatHeaderValue(JwtPropagation propagation, string token)
        => string.IsNullOrEmpty(propagation.TokenType)
            ? token
            : $"{propagation.TokenType} {token}";

    private void WriteHeader(IDictionary<string, string?> headers, string headerName, string headerValue)
    {
        if (headers.TryGetValue(headerName, out var existingValue)
            && !string.IsNullOrWhiteSpace(existingValue)
            && !string.Equals(existingValue, headerValue, StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "JWT propagation outbound middleware is overwriting existing header '{HeaderName}'. Check outbound middleware ordering and token propagation configuration.",
                headerName);
        }

        headers[headerName] = headerValue;
    }
}
