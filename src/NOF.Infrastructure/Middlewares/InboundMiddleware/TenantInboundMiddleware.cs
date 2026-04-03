using NOF.Contract;
using System.Diagnostics;

namespace NOF.Infrastructure;

public class TenantInboundMiddlewareStep : IInboundMiddlewareStep<TenantInboundMiddlewareStep, TenantInboundMiddleware>, IAfter<ExceptionInboundMiddlewareStep>;

public sealed class TenantInboundMiddleware : IInboundMiddleware
{
    private readonly IExecutionContext _executionContext;

    public TenantInboundMiddleware(IExecutionContext executionContext)
    {
        _executionContext = executionContext;
    }

    public async ValueTask InvokeAsync(InboundContext context, InboundDelegate next, CancellationToken cancellationToken)
    {
        string tenantId = NOFContractConstants.Tenant.HostId;

        if (_executionContext.TryGetValue(NOFContractConstants.Transport.Headers.TenantId, out var headerTenantId))
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
