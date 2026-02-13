using Microsoft.Extensions.Options;
using NOF.Application;
using NOF.Infrastructure.Core;

namespace NOF.Infrastructure.Extension.Authorization.Jwt;

/// <summary>Propagates JWT authorization token to outbound messages.</summary>
public class JwtAuthorizationOutboundMiddlewareStep : IOutboundMiddlewareStep<JwtAuthorizationOutboundMiddleware>,
    IAfter<MessageIdOutboundMiddlewareStep>;

/// <summary>
/// Outbound middleware that propagates the current user's JWT token
/// into the outbound message headers for inter-service calls.
/// Uses pattern matching on <see cref="JwtClaimsPrincipal"/> to extract the raw token.
/// The header name and token type are configurable via <see cref="JwtAuthorizationOptions"/>.
/// </summary>
public sealed class JwtAuthorizationOutboundMiddleware : IOutboundMiddleware
{
    private readonly IInvocationContext _invocationContext;
    private readonly JwtAuthorizationOptions _options;

    public JwtAuthorizationOutboundMiddleware(IInvocationContext invocationContext, IOptions<JwtAuthorizationOptions> options)
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
