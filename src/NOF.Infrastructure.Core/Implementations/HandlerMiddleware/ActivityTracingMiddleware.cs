using System.Diagnostics;

namespace NOF;

/// <summary>
/// Activity tracing middleware
/// Creates distributed tracing Activity for each Handler execution
/// </summary>
public sealed class ActivityTracingMiddleware : IHandlerMiddleware
{
    private readonly IInvocationContextInternal _invocationContext;

    public ActivityTracingMiddleware(IInvocationContextInternal invocationContext)
    {
        _invocationContext = invocationContext;
    }

    public async ValueTask InvokeAsync(HandlerContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        // Merge tracing context and create Activity
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
            if (activity is not null)
            {
                activity.SetStatus(ActivityStatusCode.Error, ex.Message);
                activity.AddException(ex);
            }
            throw;
        }
    }

    /// <summary>
    /// Restore tracing context and create Activity: if InvocationContext has TraceId/SpanId, restore tracing context
    /// If there is other existing tracing, try to merge, merge fails silently
    /// </summary>
    /// <param name="context">Handler execution context</param>
    /// <returns>Created Activity, returns null if no tracing information</returns>
    private Activity? RestoreTraceContext(HandlerContext context)
    {
        var traceId = _invocationContext.TraceId;
        var spanId = _invocationContext.SpanId;

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
