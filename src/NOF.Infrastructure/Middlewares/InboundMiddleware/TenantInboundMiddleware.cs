using Microsoft.Extensions.Options;
using NOF.Application;
using NOF.Abstraction;
using NOF.Contract;
using NOF.Hosting;
using System.Diagnostics;

namespace NOF.Infrastructure;

public sealed class TenantInboundMiddleware : IInboundMiddleware, IAfter<ExceptionInboundMiddleware>
{
    private readonly IExecutionContext _executionContext;
    private readonly TenantOptions _tenantOptions;

    public TenantInboundMiddleware(IExecutionContext executionContext, IOptions<TenantOptions> tenantOptions)
    {
        _executionContext = executionContext;
        _tenantOptions = tenantOptions.Value;
    }

    public async ValueTask InvokeAsync(InboundContext context, InboundDelegate next, CancellationToken cancellationToken)
    {
        var tenantId = NOFAbstractionConstants.Tenant.NormalizeTenantId(_tenantOptions.SingleTenantId);
        if (_tenantOptions.Mode != TenantMode.SingleTenant
            && _executionContext.TryGetValue(NOFHostingConstants.Transport.Headers.TenantId, out var headerTenantId))
        {
            tenantId = NOFAbstractionConstants.Tenant.NormalizeTenantId(headerTenantId);
        }

        _executionContext.SetTenantId(tenantId);
        Activity.Current?.SetTag(NOFInfrastructureConstants.InboundPipeline.Tags.TenantId, tenantId);

        await next(cancellationToken);
    }
}

