using NOF.Hosting;
using System.Diagnostics;

namespace NOF.Infrastructure;

/// <summary>Activity tracing step resolves trace/span IDs from headers and creates distributed tracing Activity per handler execution.</summary>
/// <summary>
/// Inbound middleware that resolves tracing information from transport headers
/// and creates a distributed tracing <see cref="Activity"/> for each handler execution.
/// </summary>
public sealed class TracingInboundMiddleware : IInboundMiddleware, IAfter<TenantInboundMiddleware>
{
    private readonly IExecutionContext _executionContext;

    public TracingInboundMiddleware(IExecutionContext executionContext)
    {
        _executionContext = executionContext;
    }

    public async ValueTask InvokeAsync(InboundContext context, InboundDelegate next, CancellationToken cancellationToken)
    {
        // Resolve trace/span IDs from headers
        _executionContext.TryGetValue(NOFHostingConstants.Transport.Headers.TraceId, out var traceId);
        _executionContext.TryGetValue(NOFHostingConstants.Transport.Headers.SpanId, out var spanId);

        // Create Activity with resolved tracing context
        using var activity = CreateActivity(context, traceId, spanId);

        // Stop propagating inbound header-based tracing once Activity is created
        _executionContext.Remove(NOFHostingConstants.Transport.Headers.TraceId);
        _executionContext.Remove(NOFHostingConstants.Transport.Headers.SpanId);

        var handlerType = context.Metadatas.TryGetValue("HandlerType", out var handlerTypeObj) && handlerTypeObj is Type type ? type : null;
        var handlerName = context.Metadatas.TryGetValue("HandlerName", out var hn) ? hn as string : handlerType?.FullName;
        var messageName = context.Metadatas.TryGetValue("MessageName", out var mn) ? mn as string : context.Message?.GetType().FullName;
        var methodName = context.Metadatas.TryGetValue("MethodName", out var mdn) ? mdn as string : null;
        activity?.SetTag(NOFInfrastructureConstants.InboundPipeline.Tags.HandlerType, handlerName);
        activity?.SetTag(NOFInfrastructureConstants.InboundPipeline.Tags.MessageType, messageName);
        if (methodName is not null)
        {
            activity?.SetTag("rpc.method", methodName);
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
            ? new TracingInfo(traceId, spanId)
            : null;
        var handlerType = context.Metadatas.TryGetValue("HandlerType", out var handlerTypeObj) && handlerTypeObj is Type type ? type : null;
        var handlerName2 = context.Metadatas.TryGetValue("HandlerName", out var hn2) ? hn2 as string : handlerType?.FullName ?? "UnknownHandler";
        var messageName2 = context.Metadatas.TryGetValue("MessageName", out var mn2) ? mn2 as string : context.Message?.GetType().FullName ?? "<null>";
        return NOFInfrastructureConstants.InboundPipeline.Source.StartActivityWithParent(
            $"{handlerName2}.Handle: {messageName2}",
            ActivityKind.Consumer,
            parent);
    }
}
