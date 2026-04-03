using NOF.Contract;
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
        _executionContext.TryGetValue(NOFContractConstants.Transport.Headers.TraceId, out var traceId);
        _executionContext.TryGetValue(NOFContractConstants.Transport.Headers.SpanId, out var spanId);

        // Create Activity with resolved tracing context
        using var activity = CreateActivity(context, traceId, spanId);

        // Update ExecutionContext with current Activity's trace and span IDs
        if (activity is not null)
        {
            _executionContext.SetTracingInfo(new TracingInfo(activity.TraceId.ToString(), activity.SpanId.ToString()));
        }
        else if (traceId is not null && spanId is not null)
        {
            _executionContext.SetTracingInfo(new TracingInfo(traceId, spanId));
        }

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
        TracingInfo? parent = (!string.IsNullOrEmpty(traceId) && !string.IsNullOrEmpty(spanId))
            ? new TracingInfo(traceId!, spanId!)
            : null;
        return NOFInfrastructureConstants.InboundPipeline.Source.StartActivityWithParent(
            $"{context.HandlerType.FullName}.Handle: {context.Message.GetType().FullName}",
            ActivityKind.Consumer,
            parent);
    }
}
