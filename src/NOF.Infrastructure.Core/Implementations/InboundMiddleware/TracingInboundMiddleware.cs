using NOF.Application;
using System.Diagnostics;

namespace NOF.Infrastructure.Core;

/// <summary>Activity tracing step — resolves trace/span IDs from headers and creates distributed tracing Activity per handler execution.</summary>
public class TracingInboundMiddlewareStep : IInboundMiddlewareStep<TracingInboundMiddleware>, IAfter<TenantInboundMiddlewareStep>;

/// <summary>
/// Inbound middleware that resolves tracing information from transport headers
/// and creates a distributed tracing <see cref="Activity"/> for each handler execution.
/// </summary>
public sealed class TracingInboundMiddleware : IInboundMiddleware
{
    private readonly IInvocationContextInternal _invocationContext;

    public TracingInboundMiddleware(IInvocationContextInternal invocationContext)
    {
        _invocationContext = invocationContext;
    }

    public async ValueTask InvokeAsync(InboundContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        // Resolve trace/span IDs from headers
        context.Headers.TryGetValue(NOFInfrastructureCoreConstants.Transport.Headers.TraceId, out var traceId);
        context.Headers.TryGetValue(NOFInfrastructureCoreConstants.Transport.Headers.SpanId, out var spanId);
        _invocationContext.SetTracingInfo(traceId, spanId);

        // Create Activity with resolved tracing context
        using var activity = CreateActivity(context, traceId, spanId);

        if (activity is { IsAllDataRequested: true })
        {
            activity.SetTag(NOFInfrastructureCoreConstants.InboundPipeline.Tags.HandlerType, context.HandlerType);
            activity.SetTag(NOFInfrastructureCoreConstants.InboundPipeline.Tags.MessageType, context.MessageType);
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

        return NOFInfrastructureCoreConstants.InboundPipeline.Source.StartActivity(
            $"{context.HandlerType}.Handle: {context.MessageType}",
            kind: ActivityKind.Consumer,
            parentContext: activityContext);
    }
}
