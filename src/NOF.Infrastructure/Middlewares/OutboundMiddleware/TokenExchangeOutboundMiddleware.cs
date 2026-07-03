using NOF.Abstraction;
using NOF.Contract;
using NOF.Hosting;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace NOF.Infrastructure;

/// <summary>
/// Exchanges outbound request JWTs when explicitly enabled on downstream propagation settings.
/// </summary>
public sealed class TokenExchangeOutboundMiddleware(
    IUserContext userContext,
    IJwtTokenExchangeService tokenExchangeService,
    ILogger<TokenExchangeOutboundMiddleware> logger) :
    IRequestOutboundMiddleware,
    ICommandOutboundMiddleware,
    INotificationOutboundMiddleware
{
    public TopologyComparison Compare(IRequestOutboundMiddleware other)
        => other is Hosting.JwtTokenPropagationOutboundMiddleware ? TopologyComparison.After : TopologyComparison.DoesNotMatter;

    public TopologyComparison Compare(ICommandOutboundMiddleware other)
        => other is JwtTokenPropagationOutboundMiddleware ? TopologyComparison.After : TopologyComparison.DoesNotMatter;

    public TopologyComparison Compare(INotificationOutboundMiddleware other)
        => other is JwtTokenPropagationOutboundMiddleware ? TopologyComparison.After : TopologyComparison.DoesNotMatter;

    public async ValueTask InvokeAsync(RequestOutboundContext context, object request, RequestOutboundHandlerDelegate next, CancellationToken cancellationToken)
    {
        await ExchangeConfiguredTokensAsync(context.Headers, cancellationToken).ConfigureAwait(false);
        await ExchangeExplicitTokenAsync(context, context.Headers, cancellationToken).ConfigureAwait(false);
        await next(context, request, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask InvokeAsync(CommandOutboundContext context, object message, CommandOutboundHandlerDelegate next, CancellationToken cancellationToken)
    {
        await ExchangeConfiguredTokensAsync(context.Headers, cancellationToken).ConfigureAwait(false);
        await ExchangeExplicitTokenAsync(context, context.Headers, cancellationToken).ConfigureAwait(false);
        await next(context, message, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask InvokeAsync(NotificationOutboundContext context, object message, NotificationOutboundHandlerDelegate next, CancellationToken cancellationToken)
    {
        await ExchangeConfiguredTokensAsync(context.Headers, cancellationToken).ConfigureAwait(false);
        await ExchangeExplicitTokenAsync(context, context.Headers, cancellationToken).ConfigureAwait(false);
        await next(context, message, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask ExchangeConfiguredTokensAsync(IDictionary<string, string?> headers, CancellationToken cancellationToken)
    {
        foreach (var identity in userContext.User.GetIdentities<JwtClaimsIdentity>()
            .Where(identity => identity.DownstreamPropagation is not null)
            .Where(identity => identity.Token.Length > 0))
        {
            var propagation = identity.DownstreamPropagation!;
            if (!propagation.EnableTokenExchange)
            {
                continue;
            }

            var exchangedToken = await tokenExchangeService
                .ExchangeTokenAsync(identity.Token, propagation, cancellationToken)
                .ConfigureAwait(false);
            WriteHeader(headers, propagation.HeaderName, FormatHeaderValue(propagation, exchangedToken));
        }
    }

    private async ValueTask ExchangeExplicitTokenAsync(
        Context context,
        IDictionary<string, string?> headers,
        CancellationToken cancellationToken)
    {
        var headerName = context.GetTokenExchangeHeaderName();
        if (string.IsNullOrWhiteSpace(headerName))
        {
            return;
        }

        var identity = userContext.User.GetIdentities<JwtClaimsIdentity>()
            .FirstOrDefault(candidate => candidate.Token.Length > 0);
        if (identity is null)
        {
            throw new InvalidOperationException("Context.WithTokenExchange(...) requires a JwtClaimsIdentity with a non-empty token.");
        }

        var exchangedToken = await tokenExchangeService
            .ExchangeTokenAsync(identity.Token, identity.DownstreamPropagation ?? new JwtPropagation(), cancellationToken)
            .ConfigureAwait(false);
        WriteHeader(headers, headerName, "Bearer " + exchangedToken);
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
            logger.LogWarning(
                "Token exchange outbound middleware is overwriting existing header '{HeaderName}'. Check outbound middleware ordering and explicit token configuration.",
                headerName);
        }

        headers[headerName] = headerValue;
    }
}
