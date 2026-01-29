using System.Diagnostics;

namespace NOF;

/// <summary>
/// 租户头处理中间件
/// 从消息头中提取租户信息并设置到 InvocationContext 中
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

        // 从消息头中提取租户ID
        if (context.Items.TryGetValue(NOFConstants.TenantId, out var tenantIdObj) &&
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
