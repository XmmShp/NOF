using System.Diagnostics;

namespace NOF;

/// <summary>
/// 租户头处理中间件
/// 从消息头中提取租户信息并设置到 HandlerContext.Items 中
/// </summary>
public sealed class TenantHeaderMiddleware : IHandlerMiddleware
{
    private readonly ITenantContextInternal _tenantContext;

    public TenantHeaderMiddleware(ITenantContextInternal tenantContext)
    {
        _tenantContext = tenantContext;
    }

    public async ValueTask InvokeAsync(HandlerContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        var activity = Activity.Current;

        // 从消息头中提取租户ID
        if (context.Items.TryGetValue(NOFConstants.TenantId, out var tenantIdObj) &&
            tenantIdObj is string tenantId &&
            !string.IsNullOrEmpty(tenantId))
        {
            _tenantContext.SetCurrentTenantId(tenantId);
            if (activity is { IsAllDataRequested: true })
            {
                activity.SetTag(HandlerPipelineTracing.Tags.TenantId, tenantId);
            }
        }

        await next(cancellationToken);
    }
}
