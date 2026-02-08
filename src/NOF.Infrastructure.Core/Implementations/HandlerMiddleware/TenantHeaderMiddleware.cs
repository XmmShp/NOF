using System.Diagnostics;

namespace NOF;

/// <summary>
/// Tenant header processing middleware
/// Extracts tenant information from message headers and sets it to InvocationContext
/// </summary>
public sealed class TenantHeaderMiddleware : IHandlerMiddleware
{
    private readonly IInvocationContextInternal _invocationContext;

    public TenantHeaderMiddleware(IInvocationContextInternal invocationContext)
    {
        _invocationContext = invocationContext;
    }

    public async ValueTask InvokeAsync(HandlerContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        var activity = Activity.Current;

        // Extract tenant ID from message headers (now in InvocationContext.Items)
        if (_invocationContext.Items.TryGetValue(NOFConstants.TenantId, out var tenantIdObj) &&
            tenantIdObj is string tenantId &&
            !string.IsNullOrEmpty(tenantId))
        {
            _invocationContext.SetTenantId(tenantId);
            if (activity is { IsAllDataRequested: true })
            {
                activity.SetTag(HandlerPipelineTracing.Tags.TenantId, tenantId);
            }
        }

        await next(cancellationToken);
    }
}
