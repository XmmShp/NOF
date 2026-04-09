namespace NOF.Hosting;

public sealed class TenantOutboundMiddleware : IOutboundMiddleware
{
    private readonly IExecutionContext _executionContext;

    public TenantOutboundMiddleware(IExecutionContext executionContext)
    {
        _executionContext = executionContext;
    }

    public ValueTask InvokeAsync(OutboundContext context, OutboundDelegate next, CancellationToken cancellationToken)
    {
        _executionContext[NOFHostingConstants.Transport.Headers.TenantId] =
            NOFHostingConstants.Tenant.NormalizeTenantId(_executionContext.TenantId);

        return next(cancellationToken);
    }
}

