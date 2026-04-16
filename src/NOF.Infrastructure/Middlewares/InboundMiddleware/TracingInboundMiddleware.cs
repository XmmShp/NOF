using NOF.Abstraction;
using NOF.Application;
using NOF.Hosting;
using System.Diagnostics;

namespace NOF.Infrastructure;

public sealed class TracingInboundMiddleware :
    ICommandInboundMiddleware,
    INotificationInboundMiddleware,
    IRequestInboundMiddleware,
    IAfter<TenantInboundMiddleware>
{
    private readonly IExecutionContext _executionContext;

    public TracingInboundMiddleware(IExecutionContext executionContext)
    {
        _executionContext = executionContext;
    }

    public async ValueTask InvokeAsync(CommandInboundContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        _executionContext.TryGetValue(NOFAbstractionConstants.Transport.Headers.TraceId, out var traceId);
        _executionContext.TryGetValue(NOFAbstractionConstants.Transport.Headers.SpanId, out var spanId);

        using var activity = CreateCommandActivity(context, traceId, spanId);

        _executionContext.Remove(NOFAbstractionConstants.Transport.Headers.TraceId);
        _executionContext.Remove(NOFAbstractionConstants.Transport.Headers.SpanId);

        activity?.SetTag(NOFInfrastructureConstants.InboundPipeline.Tags.HandlerType, context.HandlerType.DisplayName);
        activity?.SetTag(NOFInfrastructureConstants.InboundPipeline.Tags.MessageType, context.Message.GetType().DisplayName);

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

    private static Activity? CreateCommandActivity(CommandInboundContext context, string? traceId, string? spanId)
    {
        TracingInfo? parent = (!string.IsNullOrEmpty(traceId) && !string.IsNullOrEmpty(spanId))
            ? new TracingInfo(traceId, spanId)
            : null;
        var handlerName = context.HandlerType.DisplayName;
        var messageName = context.Message.GetType().DisplayName;
        return NOFInfrastructureConstants.InboundPipeline.Source.StartActivityWithParent(
            $"{handlerName}.Handle: {messageName}",
            ActivityKind.Consumer,
            parent);
    }

    public async ValueTask InvokeAsync(NotificationInboundContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        _executionContext.TryGetValue(NOFAbstractionConstants.Transport.Headers.TraceId, out var traceId);
        _executionContext.TryGetValue(NOFAbstractionConstants.Transport.Headers.SpanId, out var spanId);

        using var activity = CreateNotificationActivity(context, traceId, spanId);

        _executionContext.Remove(NOFAbstractionConstants.Transport.Headers.TraceId);
        _executionContext.Remove(NOFAbstractionConstants.Transport.Headers.SpanId);

        activity?.SetTag(NOFInfrastructureConstants.InboundPipeline.Tags.HandlerType, context.HandlerType.DisplayName);
        activity?.SetTag(NOFInfrastructureConstants.InboundPipeline.Tags.MessageType, context.Message.GetType().DisplayName);

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

    private static Activity? CreateNotificationActivity(NotificationInboundContext context, string? traceId, string? spanId)
    {
        TracingInfo? parent = (!string.IsNullOrEmpty(traceId) && !string.IsNullOrEmpty(spanId))
            ? new TracingInfo(traceId, spanId)
            : null;
        var handlerName = context.HandlerType.DisplayName;
        var messageName = context.Message.GetType().DisplayName;
        return NOFInfrastructureConstants.InboundPipeline.Source.StartActivityWithParent(
            $"{handlerName}.Handle: {messageName}",
            ActivityKind.Consumer,
            parent);
    }

    public async ValueTask InvokeAsync(RequestInboundContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        _executionContext.TryGetValue(NOFAbstractionConstants.Transport.Headers.TraceId, out var traceId);
        _executionContext.TryGetValue(NOFAbstractionConstants.Transport.Headers.SpanId, out var spanId);

        using var activity = CreateRequestActivity(context, traceId, spanId);

        _executionContext.Remove(NOFAbstractionConstants.Transport.Headers.TraceId);
        _executionContext.Remove(NOFAbstractionConstants.Transport.Headers.SpanId);

        activity?.SetTag(NOFInfrastructureConstants.InboundPipeline.Tags.HandlerType, context.HandlerType.DisplayName);
        activity?.SetTag(NOFInfrastructureConstants.InboundPipeline.Tags.MessageType, $"{context.ServiceType.DisplayName}.{context.MethodName}");
        activity?.SetTag("rpc.method", $"{context.ServiceType.DisplayName}.{context.MethodName}");

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

    private static Activity? CreateRequestActivity(RequestInboundContext context, string? traceId, string? spanId)
    {
        TracingInfo? parent = (!string.IsNullOrEmpty(traceId) && !string.IsNullOrEmpty(spanId))
            ? new TracingInfo(traceId, spanId)
            : null;
        var handlerName = context.HandlerType.DisplayName;
        var requestName = $"{context.ServiceType.DisplayName}.{context.MethodName}";
        return NOFInfrastructureConstants.InboundPipeline.Source.StartActivityWithParent(
            $"{handlerName}.Handle: {requestName}",
            ActivityKind.Consumer,
            parent);
    }
}
