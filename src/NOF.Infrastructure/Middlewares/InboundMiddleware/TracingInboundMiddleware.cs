using NOF.Abstraction;
using NOF.Application;
using NOF.Hosting;
using System.Diagnostics;

namespace NOF.Infrastructure;

public sealed class TracingInboundMiddleware : AllMessagesInboundMiddleware, IAfter<TenantInboundMiddleware>
{
    private readonly IExecutionContext _executionContext;

    public TracingInboundMiddleware(IExecutionContext executionContext)
    {
        _executionContext = executionContext;
    }

    protected override async ValueTask InvokeAsyncCore(MessageInboundContext context, Func<CancellationToken, ValueTask> next, CancellationToken cancellationToken)
    {
        _executionContext.TryGetValue(NOFAbstractionConstants.Transport.Headers.TraceId, out var traceId);
        _executionContext.TryGetValue(NOFAbstractionConstants.Transport.Headers.SpanId, out var spanId);

        using var activity = CreateActivity(context, traceId, spanId);

        _executionContext.Remove(NOFAbstractionConstants.Transport.Headers.TraceId);
        _executionContext.Remove(NOFAbstractionConstants.Transport.Headers.SpanId);

        activity?.SetTag(NOFInfrastructureConstants.InboundPipeline.Tags.HandlerType, context.HandlerName);
        activity?.SetTag(NOFInfrastructureConstants.InboundPipeline.Tags.MessageType, context.MessageName);
        if (context is RequestInboundContext requestContext)
        {
            activity?.SetTag("rpc.method", $"{requestContext.ServiceType.FullName}.{requestContext.OperationName}");
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

    private static Activity? CreateActivity(MessageInboundContext context, string? traceId, string? spanId)
    {
        TracingInfo? parent = (!string.IsNullOrEmpty(traceId) && !string.IsNullOrEmpty(spanId))
            ? new TracingInfo(traceId, spanId)
            : null;
        var handlerName = context.HandlerName ?? "UnknownHandler";
        var messageName = context.MessageName ?? "<null>";
        return NOFInfrastructureConstants.InboundPipeline.Source.StartActivityWithParent(
            $"{handlerName}.Handle: {messageName}",
            ActivityKind.Consumer,
            parent);
    }
}
