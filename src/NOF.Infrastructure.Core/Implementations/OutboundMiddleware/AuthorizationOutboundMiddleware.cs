using NOF.Application;

namespace NOF.Infrastructure.Core;

/// <summary>Propagates JWT authorization token to outbound messages.</summary>
public class AuthorizationOutboundMiddlewareStep : IOutboundMiddlewareStep<AuthorizationOutboundMiddleware>,
    IAfter<MessageIdOutboundMiddlewareStep>;

/// <summary>
/// Outbound middleware that propagates the current user's JWT token
/// into the <see cref="NOFConstants.Headers.Authorization"/> header for inter-service calls.
/// </summary>
public sealed class AuthorizationOutboundMiddleware : IOutboundMiddleware
{
    private readonly IInvocationContext _invocationContext;

    public AuthorizationOutboundMiddleware(IInvocationContext invocationContext)
    {
        _invocationContext = invocationContext;
    }

    public ValueTask InvokeAsync(OutboundContext context, OutboundDelegate next, CancellationToken cancellationToken)
    {
        if (_invocationContext.User.IsAuthenticated && !string.IsNullOrEmpty(_invocationContext.User.Token))
        {
            context.Headers.TryAdd(NOFConstants.Headers.Authorization, $"Bearer {_invocationContext.User.Token}");
        }

        return next(cancellationToken);
    }
}
