using NOF.Application;
using NOF.Contract;
using System.Diagnostics;
using System.Security.Claims;

namespace NOF.Infrastructure;

/// <summary>Tenant resolution step resolves tenant from claims or headers.</summary>
public class TenantInboundMiddlewareStep : IInboundMiddlewareStep<TenantInboundMiddlewareStep, TenantInboundMiddleware>, IAfter<ExceptionInboundMiddlewareStep>;

/// <summary>
/// Inbound middleware that resolves the tenant identifier.
/// Prioritizes tenant from user claims; falls back to transport header.
/// </summary>
public sealed class TenantInboundMiddleware : IInboundMiddleware
{
    private readonly IExecutionContext _executionContext;

    public TenantInboundMiddleware(IExecutionContext executionContext)
    {
        _executionContext = executionContext;
    }

    public async ValueTask InvokeAsync(InboundContext context, InboundDelegate next, CancellationToken cancellationToken)
    {
        string tenantId = NOFInfrastructureConstants.Tenant.HostId;

        if (_executionContext.User.IsAuthenticated)
        {
            tenantId = NOFInfrastructureConstants.Tenant.NormalizeTenantId(
                _executionContext.User.FindFirst(ClaimTypes.TenantId)?.Value);
        }

        if (string.IsNullOrWhiteSpace(tenantId) &&
            context.Headers.TryGetValue(NOFInfrastructureConstants.Transport.Headers.TenantId, out var headerTenantId))
        {
            tenantId = NOFInfrastructureConstants.Tenant.NormalizeTenantId(headerTenantId);
        }

        _executionContext.SetTenantId(tenantId);

        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            var activity = Activity.Current;
            if (activity is { IsAllDataRequested: true })
            {
                activity.SetTag(NOFInfrastructureConstants.InboundPipeline.Tags.TenantId, tenantId);
            }
        }

        await next(cancellationToken);
    }
}
