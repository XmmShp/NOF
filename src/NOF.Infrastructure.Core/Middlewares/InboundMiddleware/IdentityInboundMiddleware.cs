using NOF.Application;
using NOF.Infrastructure.Abstraction;

namespace NOF.Infrastructure.Core;

/// <summary>Identity resolution step — resolves user identity from inbound context.</summary>
public class IdentityInboundMiddlewareStep : IInboundMiddlewareStep<IdentityInboundMiddleware>, IAfter<ExceptionInboundMiddlewareStep>;

/// <summary>
/// Inbound middleware that resolves user identity via a pluggable <see cref="IIdentityResolver"/>.
/// If no resolver is registered, the user remains anonymous.
/// </summary>
public sealed class IdentityInboundMiddleware : IInboundMiddleware
{
    private readonly IInvocationContextInternal _invocationContext;
    private readonly IIdentityResolver? _identityResolver;

    public IdentityInboundMiddleware(
        IInvocationContextInternal invocationContext,
        IIdentityResolver? identityResolver = null)
    {
        _invocationContext = invocationContext;
        _identityResolver = identityResolver;
    }

    public async ValueTask InvokeAsync(InboundContext context, InboundDelegate next, CancellationToken cancellationToken)
    {
        if (_identityResolver is not null)
        {
            var principal = await _identityResolver.ResolveAsync(context, cancellationToken);
            if (principal is not null)
            {
                _invocationContext.SetUser(principal);
                await next(cancellationToken);
                return;
            }
        }

        _invocationContext.UnsetUser();
        await next(cancellationToken);
    }
}
