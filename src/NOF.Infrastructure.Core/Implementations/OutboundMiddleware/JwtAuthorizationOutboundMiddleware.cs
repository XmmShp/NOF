using Microsoft.Extensions.Options;
using NOF.Application;

namespace NOF.Infrastructure.Core;

/// <summary>Propagates JWT authorization token to outbound messages.</summary>
public class JwtAuthorizationOutboundMiddlewareStep : IOutboundMiddlewareStep<JwtAuthorizationOutboundMiddleware>,
    IAfter<MessageIdOutboundMiddlewareStep>;

/// <summary>
/// Outbound middleware that propagates the current user's JWT token
/// into the outbound message headers for inter-service calls.
/// Uses pattern matching on <see cref="JwtClaimsPrincipal"/> to extract the raw token.
/// The header name and token type are configurable via <see cref="AuthorizationOptions"/>.
/// </summary>
public sealed class JwtAuthorizationOutboundMiddleware : IOutboundMiddleware
{
    private readonly IInvocationContext _invocationContext;
    private readonly AuthorizationOptions _options;

    public JwtAuthorizationOutboundMiddleware(IInvocationContext invocationContext, IOptions<AuthorizationOptions> options)
    {
        _invocationContext = invocationContext;
        _options = options.Value;
    }

    public ValueTask InvokeAsync(OutboundContext context, OutboundDelegate next, CancellationToken cancellationToken)
    {
        if (_invocationContext.User is JwtClaimsPrincipal { Token: { Length: > 0 } token })
        {
            context.Headers.TryAdd(_options.HeaderName, $"{_options.TokenType} {token}");
        }

        return next(cancellationToken);
    }
}
