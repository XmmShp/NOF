using NOF.Application;
using System.Diagnostics;

namespace NOF.Infrastructure.Core;

/// <summary>Tenant resolution step — resolves tenant from claims or headers.</summary>
public class TenantInboundMiddlewareStep : IInboundMiddlewareStep<TenantInboundMiddleware>, IAfter<IdentityInboundMiddlewareStep>;

/// <summary>
/// Inbound middleware that resolves the tenant identifier.
/// Prioritizes tenant from user claims; falls back to transport header.
/// </summary>
public sealed class TenantInboundMiddleware : IInboundMiddleware
{
    private readonly IInvocationContextInternal _invocationContext;

    public TenantInboundMiddleware(IInvocationContextInternal invocationContext)
    {
        _invocationContext = invocationContext;
    }

    public ValueTask InvokeAsync(InboundContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        string? tenantId = null;

        if (_invocationContext.User.IsAuthenticated)
        {
            tenantId = _invocationContext.User
                .FindFirst(NOFInfrastructureCoreConstants.Jwt.ClaimTypes.TenantId)?.Value;
        }

        if (string.IsNullOrEmpty(tenantId) &&
            context.Headers.TryGetValue(NOFInfrastructureCoreConstants.Transport.Headers.TenantId, out var headerTenantId) &&
            !string.IsNullOrEmpty(headerTenantId))
        {
            tenantId = headerTenantId;
        }

        _invocationContext.SetTenantId(tenantId);

        if (!string.IsNullOrEmpty(tenantId))
        {
            var activity = Activity.Current;
            if (activity is { IsAllDataRequested: true })
            {
                activity.SetTag(NOFInfrastructureCoreConstants.InboundPipeline.Tags.TenantId, tenantId);
            }
        }

        return next(cancellationToken);
    }
}
