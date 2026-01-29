using System.Diagnostics;

namespace NOF;

/// <summary>
/// Activity 追踪中间件
/// 为每个 Handler 执行创建分布式追踪 Activity
/// </summary>
public sealed class ActivityTracingMiddleware : IHandlerMiddleware
{
    public async ValueTask InvokeAsync(HandlerContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        // 合并追踪上下文并创建Activity
        using var activity = RestoreTraceContext(context);

        if (activity is { IsAllDataRequested: true })
        {
            activity.SetTag(HandlerPipelineTracing.Tags.HandlerType, context.HandlerType);
            activity.SetTag(HandlerPipelineTracing.Tags.MessageType, context.MessageType);
        }

        try
        {
            await next(cancellationToken);

            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            if (activity != null)
            {
                activity.SetStatus(ActivityStatusCode.Error, ex.Message);
                activity.AddException(ex);
            }
            throw;
        }
    }

    /// <summary>
    /// 恢复追踪上下文并创建Activity：如果HandlerContext.Items中有TraceId/SpanId，则恢复追踪上下文
    /// 如果当前已有其它追踪，则尝试合并，合并静默失败
    /// </summary>
    /// <param name="context">Handler执行上下文</param>
    /// <returns>创建的Activity，如果没有追踪信息则返回null</returns>
    private static Activity? RestoreTraceContext(HandlerContext context)
    {
        var traceId = context.Items.GetOrDefault(NOFConstants.TraceId, string.Empty);
        var spanId = context.Items.GetOrDefault(NOFConstants.SpanId, string.Empty);

        var activityContext = new ActivityContext(
            traceId: string.IsNullOrEmpty(traceId) ? ActivityTraceId.CreateRandom() : ActivityTraceId.CreateFromString(traceId),
            spanId: string.IsNullOrEmpty(spanId) ? ActivitySpanId.CreateRandom() : ActivitySpanId.CreateFromString(spanId),
            traceFlags: ActivityTraceFlags.Recorded,
            isRemote: true);

        var activity = HandlerPipelineTracing.Source.StartActivity(
            $"{context.HandlerType}.Handle: {context.MessageType}",
            kind: ActivityKind.Consumer,
            parentContext: activityContext);

        return activity;
    }
}
