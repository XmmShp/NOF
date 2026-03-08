using NOF.Application;
using NOF.Infrastructure.Abstraction;
using System.Diagnostics;
using System.Security.Claims;

namespace NOF.Infrastructure.Core;

/// <summary>Tenant resolution step — resolves tenant from claims or headers.</summary>
public class TenantInboundMiddlewareStep : IInboundMiddlewareStep<TenantInboundMiddlewareStep, TenantInboundMiddleware>, IAfter<IdentityInboundMiddlewareStep>;

/// <summary>
/// Inbound middleware that resolves the tenant identifier.
/// Prioritizes tenant from user claims; falls back to transport header.
/// </summary>
public sealed class TenantInboundMiddleware : IInboundMiddleware
{
    private readonly IMutableInvocationContext _invocationContext;

    public TenantInboundMiddleware(IMutableInvocationContext invocationContext)
    {
        _invocationContext = invocationContext;
    }

    public async ValueTask InvokeAsync(InboundContext context, InboundDelegate next, CancellationToken cancellationToken)
    {
        string? tenantId = null;

        if (_invocationContext.UserContext.User.IsAuthenticated)
        {
            tenantId = _invocationContext.UserContext.User
                .FindFirst(ClaimTypes.TenantId)?.Value;
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

        await next(cancellationToken);
    }
}
