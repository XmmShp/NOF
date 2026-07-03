using NOF.Abstraction;
using NOF.Hosting;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace NOF.Infrastructure;

/// <summary>Propagates access tokens to outbound commands and notifications.</summary>
public sealed class JwtTokenPropagationOutboundMiddleware(IUserContext userContext, ILogger<JwtTokenPropagationOutboundMiddleware> logger) :
    ICommandOutboundMiddleware,
    INotificationOutboundMiddleware
{
    public TopologyComparison Compare(ICommandOutboundMiddleware other) => TopologyComparison.DoesNotMatter;
    public TopologyComparison Compare(INotificationOutboundMiddleware other) => TopologyComparison.DoesNotMatter;

    public ValueTask InvokeAsync(CommandOutboundContext context, object message, CommandOutboundHandlerDelegate next, CancellationToken cancellationToken)
    {
        Propagate(context.Headers);
        return next(context, message, cancellationToken);
    }

    public ValueTask InvokeAsync(NotificationOutboundContext context, object message, NotificationOutboundHandlerDelegate next, CancellationToken cancellationToken)
    {
        Propagate(context.Headers);
        return next(context, message, cancellationToken);
    }

    private void Propagate(IDictionary<string, string?> headers)
    {
        foreach (var identity in userContext.User.GetIdentities<JwtClaimsIdentity>()
            .Where(identity => identity.DownstreamPropagation is not null)
            .Where(identity => identity.Token.Length > 0))
        {
            var propagation = identity.DownstreamPropagation!;
            WriteHeader(headers, propagation.HeaderName, FormatHeaderValue(propagation, identity.Token));
        }
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
                "JWT propagation outbound middleware is overwriting existing header '{HeaderName}'. Check outbound middleware ordering and token propagation configuration.",
                headerName);
        }

        headers[headerName] = headerValue;
    }
}
