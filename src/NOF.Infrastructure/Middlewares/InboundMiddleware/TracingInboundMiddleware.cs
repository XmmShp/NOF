using Microsoft.Extensions.Hosting;
using NOF.Abstraction;
using NOF.Contract;
using NOF.Hosting;
using System.Diagnostics;
using NOF.Application;

namespace NOF.Infrastructure;

public sealed class TracingInboundMiddleware :
    ICommandInboundMiddleware,
    INotificationInboundMiddleware,
    IRequestInboundMiddleware
{
    public TopologyComparison Compare(ICommandInboundMiddleware other)
        => other is TenantInboundMiddleware ? TopologyComparison.After : TopologyComparison.DoesNotMatter;

    public TopologyComparison Compare(INotificationInboundMiddleware other)
        => other is TenantInboundMiddleware ? TopologyComparison.After : TopologyComparison.DoesNotMatter;

    public TopologyComparison Compare(IRequestInboundMiddleware other)
        => other is TenantInboundMiddleware ? TopologyComparison.After : TopologyComparison.DoesNotMatter;

    private readonly IHostEnvironment _hostEnvironment;

    public TracingInboundMiddleware(IHostEnvironment hostEnvironment)
    {
        _hostEnvironment = hostEnvironment;
    }

    public async ValueTask InvokeAsync(CommandInboundContext context, object message, CommandHandlerDelegate next, CancellationToken cancellationToken)
    {
        var executionContext = context;
        executionContext.TryGetHeader(NOFAbstractionConstants.Transport.Headers.TraceId, out var traceId);
        executionContext.TryGetHeader(NOFAbstractionConstants.Transport.Headers.SpanId, out var spanId);

        using var activity = CreateCommandActivity(context, traceId, spanId, _hostEnvironment);

        executionContext = (CommandInboundContext)executionContext
            .WithoutHeader(NOFAbstractionConstants.Transport.Headers.TraceId)
            .WithoutHeader(NOFAbstractionConstants.Transport.Headers.SpanId);

        activity?.SetTag(NOFInfrastructureConstants.InboundPipeline.Tags.HandlerType, context.HandlerType.DisplayName);
        activity?.SetTag(NOFInfrastructureConstants.InboundPipeline.Tags.MessageType, context.MessageType.DisplayName);

        try
        {
            await next(executionContext, message, cancellationToken);
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

    private static Activity? CreateCommandActivity(CommandInboundContext context, string? traceId, string? spanId, IHostEnvironment hostEnvironment)
    {
        TracingInfo? parent = (!string.IsNullOrEmpty(traceId) && !string.IsNullOrEmpty(spanId))
            ? new TracingInfo(traceId, spanId)
            : null;
        var handlerName = context.HandlerType.DisplayName;
        var messageName = context.MessageType.DisplayName;
        return NOFInfrastructureConstants.InboundPipeline.Source.StartActivityWithParent(
            $"{handlerName}.Handle: {messageName}",
            ActivityKind.Consumer,
            parent,
            hostEnvironment);
    }

    public async ValueTask InvokeAsync(NotificationInboundContext context, object message, NotificationHandlerDelegate next, CancellationToken cancellationToken)
    {
        var executionContext = context;
        executionContext.TryGetHeader(NOFAbstractionConstants.Transport.Headers.TraceId, out var traceId);
        executionContext.TryGetHeader(NOFAbstractionConstants.Transport.Headers.SpanId, out var spanId);

        using var activity = CreateNotificationActivity(context, traceId, spanId, _hostEnvironment);

        executionContext = (NotificationInboundContext)executionContext
            .WithoutHeader(NOFAbstractionConstants.Transport.Headers.TraceId)
            .WithoutHeader(NOFAbstractionConstants.Transport.Headers.SpanId);

        activity?.SetTag(NOFInfrastructureConstants.InboundPipeline.Tags.HandlerType, context.HandlerType.DisplayName);
        activity?.SetTag(NOFInfrastructureConstants.InboundPipeline.Tags.MessageType, context.MessageType.DisplayName);

        try
        {
            await next(executionContext, message, cancellationToken);
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

    private static Activity? CreateNotificationActivity(NotificationInboundContext context, string? traceId, string? spanId, IHostEnvironment hostEnvironment)
    {
        TracingInfo? parent = (!string.IsNullOrEmpty(traceId) && !string.IsNullOrEmpty(spanId))
            ? new TracingInfo(traceId, spanId)
            : null;
        var handlerName = context.HandlerType.DisplayName;
        var messageName = context.MessageType.DisplayName;
        return NOFInfrastructureConstants.InboundPipeline.Source.StartActivityWithParent(
            $"{handlerName}.Handle: {messageName}",
            ActivityKind.Consumer,
            parent,
            hostEnvironment);
    }

    public async ValueTask InvokeAsync(RequestInboundContext context, object request, RequestHandlerDelegate next, CancellationToken cancellationToken)
    {
        var executionContext = context;
        executionContext.TryGetHeader(NOFAbstractionConstants.Transport.Headers.TraceId, out var traceId);
        executionContext.TryGetHeader(NOFAbstractionConstants.Transport.Headers.SpanId, out var spanId);

        using var activity = CreateRequestActivity(context, traceId, spanId, _hostEnvironment);

        executionContext = (RequestInboundContext)executionContext
            .WithoutHeader(NOFAbstractionConstants.Transport.Headers.TraceId)
            .WithoutHeader(NOFAbstractionConstants.Transport.Headers.SpanId);

        activity?.SetTag(NOFInfrastructureConstants.InboundPipeline.Tags.HandlerType, context.HandlerType.DisplayName);
        activity?.SetTag(NOFInfrastructureConstants.InboundPipeline.Tags.MessageType, $"{context.ServiceType.DisplayName}.{context.ServiceMethodInfo.Name}");
        activity?.SetTag("rpc.method", $"{context.ServiceType.DisplayName}.{context.ServiceMethodInfo.Name}");

        try
        {
            await next(executionContext, request, cancellationToken);
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

    private static Activity? CreateRequestActivity(RequestInboundContext context, string? traceId, string? spanId, IHostEnvironment hostEnvironment)
    {
        TracingInfo? parent = (!string.IsNullOrEmpty(traceId) && !string.IsNullOrEmpty(spanId))
            ? new TracingInfo(traceId, spanId)
            : null;
        var handlerName = context.HandlerType.DisplayName;
        var requestName = $"{context.ServiceType.DisplayName}.{context.ServiceMethodInfo.Name}";
        return NOFInfrastructureConstants.InboundPipeline.Source.StartActivityWithParent(
            $"{handlerName}.Handle: {requestName}",
            ActivityKind.Consumer,
            parent,
            hostEnvironment);
    }
}
