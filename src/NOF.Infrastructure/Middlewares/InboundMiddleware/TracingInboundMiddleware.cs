using NOF.Application;
using System.Diagnostics;

namespace NOF.Infrastructure;

/// <summary>Activity tracing step resolves trace/span IDs from headers and creates distributed tracing Activity per handler execution.</summary>
public class TracingInboundMiddlewareStep : IInboundMiddlewareStep<TracingInboundMiddlewareStep, TracingInboundMiddleware>,
    IAfter<TenantInboundMiddlewareStep>;

/// <summary>
/// Inbound middleware that resolves tracing information from transport headers
/// and creates a distributed tracing <see cref="Activity"/> for each handler execution.
/// </summary>
public sealed class TracingInboundMiddleware : IInboundMiddleware
{
    private readonly IExecutionContext _executionContext;

    public TracingInboundMiddleware(IExecutionContext executionContext)
    {
        _executionContext = executionContext;
    }

    public async ValueTask InvokeAsync(InboundContext context, InboundDelegate next, CancellationToken cancellationToken)
    {
        // Resolve trace/span IDs from headers
        context.Headers.TryGetValue(NOFInfrastructureConstants.Transport.Headers.TraceId, out var traceId);
        context.Headers.TryGetValue(NOFInfrastructureConstants.Transport.Headers.SpanId, out var spanId);
        _executionContext.SetTracingInfo(traceId, spanId);

        // Create Activity with resolved tracing context
        using var activity = CreateActivity(context, traceId, spanId);

        if (activity is { IsAllDataRequested: true })
        {
            activity.SetTag(NOFInfrastructureConstants.InboundPipeline.Tags.HandlerType, context.HandlerType.FullName);
            activity.SetTag(NOFInfrastructureConstants.InboundPipeline.Tags.MessageType, context.Message.GetType().FullName);
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

    private static Activity? CreateActivity(InboundContext context, string? traceId, string? spanId)
    {
        var activityContext = new ActivityContext(
            traceId: string.IsNullOrEmpty(traceId) ? ActivityTraceId.CreateRandom() : ActivityTraceId.CreateFromString(traceId),
            spanId: string.IsNullOrEmpty(spanId) ? ActivitySpanId.CreateRandom() : ActivitySpanId.CreateFromString(spanId),
            traceFlags: ActivityTraceFlags.Recorded,
            isRemote: true);

        return NOFInfrastructureConstants.InboundPipeline.Source.StartActivity(
            $"{context.HandlerType.FullName}.Handle: {context.Message.GetType().FullName}",
            kind: ActivityKind.Consumer,
            parentContext: activityContext);
    }
}
