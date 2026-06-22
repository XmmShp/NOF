using NOF.Abstraction;
using NOF.Hosting;
using System.Security.Claims;

namespace NOF.Infrastructure;

/// <summary>
/// Exchanges outbound request JWTs when explicitly enabled on downstream propagation settings.
/// </summary>
public sealed class RequestTokenExchangeOutboundMiddleware(
    IUserContext userContext,
    IJwtTokenExchangeService tokenExchangeService) : IRequestOutboundMiddleware
{
    public TopologyComparison Compare(IRequestOutboundMiddleware other)
        => other is Hosting.JwtTokenPropagationOutboundMiddleware ? TopologyComparison.After : TopologyComparison.DoesNotMatter;

    public async ValueTask InvokeAsync(RequestOutboundContext context, object request, RequestOutboundHandlerDelegate next, CancellationToken cancellationToken)
    {
        await ExchangeAsync(context.Headers, cancellationToken).ConfigureAwait(false);
        await next(context, request, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask ExchangeAsync(IDictionary<string, string?> headers, CancellationToken cancellationToken)
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
            headers[propagation.HeaderName] = FormatHeaderValue(propagation, exchangedToken);
        }
    }

    private static string FormatHeaderValue(JwtPropagation propagation, string token)
        => string.IsNullOrEmpty(propagation.TokenType)
            ? token
            : $"{propagation.TokenType} {token}";
}
