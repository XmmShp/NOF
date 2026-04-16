using Microsoft.Extensions.Options;
using NOF.Abstraction;
using NOF.Application;
using NOF.Hosting;
using System.Diagnostics;

namespace NOF.Infrastructure;

public sealed class TenantInboundMiddleware : AllMessagesInboundMiddleware, IAfter<ExceptionInboundMiddleware>
{
    private readonly IExecutionContext _executionContext;
    private readonly TenantOptions _tenantOptions;

    public TenantInboundMiddleware(IExecutionContext executionContext, IOptions<TenantOptions> tenantOptions)
    {
        _executionContext = executionContext;
        _tenantOptions = tenantOptions.Value;
    }

    protected override async ValueTask InvokeAsyncCore(MessageInboundContext context, Func<CancellationToken, ValueTask> next, CancellationToken cancellationToken)
    {
        var tenantId = NOFAbstractionConstants.Tenant.NormalizeTenantId(_tenantOptions.SingleTenantId);
        if (_tenantOptions.Mode != TenantMode.SingleTenant
            && _executionContext.TryGetValue(NOFAbstractionConstants.Transport.Headers.TenantId, out var headerTenantId))
        {
            tenantId = NOFAbstractionConstants.Tenant.NormalizeTenantId(headerTenantId);
        }

        _executionContext.TenantId = tenantId;
        Activity.Current?.SetTag(NOFInfrastructureConstants.InboundPipeline.Tags.TenantId, tenantId);

        await next(cancellationToken);
    }
}
