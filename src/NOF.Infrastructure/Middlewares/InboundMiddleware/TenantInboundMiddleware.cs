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
    private readonly IUserContext _userContext;

    public TenantInboundMiddleware(IExecutionContext executionContext, IUserContext userContext)
    {
        _executionContext = executionContext;
        _userContext = userContext;
    }

    public async ValueTask InvokeAsync(InboundContext context, InboundDelegate next, CancellationToken cancellationToken)
    {
        string tenantId = NOFContractConstants.Tenant.HostId;

        if (_userContext.User.IsAuthenticated)
        {
            tenantId = NOFContractConstants.Tenant.NormalizeTenantId(
                _userContext.User.FindFirst(ClaimTypes.TenantId)?.Value);
        }

        if (string.IsNullOrWhiteSpace(tenantId) &&
            _executionContext.TryGetValue(NOFContractConstants.Transport.Headers.TenantId, out var headerTenantId))
        {
            tenantId = NOFContractConstants.Tenant.NormalizeTenantId(headerTenantId);
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
