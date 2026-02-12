using Microsoft.Extensions.Options;
using NOF.Application;

namespace NOF.Infrastructure.Core;

/// <summary>Propagates JWT authorization token to outbound messages.</summary>
public class AuthorizationOutboundMiddlewareStep : IOutboundMiddlewareStep<AuthorizationOutboundMiddleware>,
    IAfter<MessageIdOutboundMiddlewareStep>;

/// <summary>
/// Outbound middleware that propagates the current user's JWT token
/// into the outbound message headers for inter-service calls.
/// The header name and token type are configurable via <see cref="AuthorizationOutboundOptions"/>.
/// </summary>
public sealed class AuthorizationOutboundMiddleware : IOutboundMiddleware
{
    private readonly IInvocationContext _invocationContext;
    private readonly AuthorizationOptions _options;

    public AuthorizationOutboundMiddleware(IInvocationContext invocationContext, IOptions<AuthorizationOptions> options)
    {
        _invocationContext = invocationContext;
        _options = options.Value;
    }

    public ValueTask InvokeAsync(OutboundContext context, OutboundDelegate next, CancellationToken cancellationToken)
    {
        if (_invocationContext.User.IsAuthenticated && !string.IsNullOrEmpty(_invocationContext.User.Token))
        {
            context.Headers.TryAdd(_options.HeaderName, $"{_options.TokenType} {_invocationContext.User.Token}");
        }

        return next(cancellationToken);
    }
}
