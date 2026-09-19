using NOF.Abstraction;
using NOF.Contract;
using NOF.Hosting;
using System.Diagnostics;

namespace NOF.Infrastructure;

/// <summary>
/// Applies contract-level host-tenant selection after authorization and before RPC handler resolution.
/// </summary>
public sealed class HostTenantInboundMiddleware : IRequestInboundMiddleware
{
    /// <inheritdoc />
    public TopologyComparison Compare(IRequestInboundMiddleware other)
        => other is TenantInboundMiddleware or AuthorizationInboundMiddleware
            ? TopologyComparison.After
            : TopologyComparison.DoesNotMatter;

    /// <inheritdoc />
    public async ValueTask InvokeAsync(RequestInboundContext context, object request,
        RequestHandlerDelegate next, CancellationToken cancellationToken)
    {
        if (!UseHostTenantAttribute.IsRequired(context.ServiceMethodInfo))
        {
            await next(context, request, cancellationToken);
            return;
        }

        var hostContext = (RequestInboundContext)context.WithTenantId(NOFAbstractionConstants.Tenant.HostId);
        Activity.Current?.SetTag(NOFInfrastructureConstants.InboundPipeline.Tags.TenantId, hostContext.TenantId);
        using var _ = Context.PushCurrent(hostContext);
        await next(hostContext, request, cancellationToken);
    }
}
