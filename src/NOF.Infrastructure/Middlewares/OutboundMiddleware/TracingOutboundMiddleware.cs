using Microsoft.Extensions.Hosting;
using NOF.Abstraction;
using NOF.Hosting;
using System.Diagnostics;

namespace NOF.Infrastructure;

public sealed class TracingOutboundMiddleware : ICommandOutboundMiddleware, INotificationOutboundMiddleware, IRequestOutboundMiddleware
{
    private readonly IHostEnvironment _hostEnvironment;

    public TracingOutboundMiddleware(IHostEnvironment hostEnvironment)
    {
        _hostEnvironment = hostEnvironment;
    }

    public async ValueTask InvokeAsync(CommandOutboundContext context, object message, CommandOutboundHandlerDelegate next, CancellationToken cancellationToken)
    {
        var messageType = message.GetType();
        var messageTypeFullName = messageType.DisplayName;
        using var activity = NOFHostingConstants.Outbound.Source.StartActivity(
            $"Outbound: {messageTypeFullName}",
            ActivityKind.Producer);
        activity?.SetServiceDeploymentTags(_hostEnvironment);

        var currentActivity = Activity.Current;
        context.Headers[NOFAbstractionConstants.Transport.Headers.TraceId] = currentActivity?.TraceId.ToString();
        context.Headers[NOFAbstractionConstants.Transport.Headers.SpanId] = currentActivity?.SpanId.ToString();

        try
        {
            await next(context, message, cancellationToken);

            context.Headers.TryGetValue(NOFAbstractionConstants.Transport.Headers.MessageId, out var messageId);
            activity?.SetTag(NOFHostingConstants.Outbound.Tags.MessageId, messageId);
            activity?.SetTag(NOFHostingConstants.Outbound.Tags.MessageType, messageType.Name);
            context.Headers.TryGetValue(NOFAbstractionConstants.Transport.Headers.TenantId, out var tenantId);
            activity?.SetTag(NOFHostingConstants.Outbound.Tags.TenantId, tenantId);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    public async ValueTask InvokeAsync(NotificationOutboundContext context, object message, NotificationOutboundHandlerDelegate next, CancellationToken cancellationToken)
    {
        var messageType = message.GetType();
        var messageTypeFullName = messageType.DisplayName;
        using var activity = NOFHostingConstants.Outbound.Source.StartActivity(
            $"Outbound: {messageTypeFullName}",
            ActivityKind.Producer);
        activity?.SetServiceDeploymentTags(_hostEnvironment);

        var currentActivity = Activity.Current;
        context.Headers[NOFAbstractionConstants.Transport.Headers.TraceId] = currentActivity?.TraceId.ToString();
        context.Headers[NOFAbstractionConstants.Transport.Headers.SpanId] = currentActivity?.SpanId.ToString();

        try
        {
            await next(context, message, cancellationToken);

            context.Headers.TryGetValue(NOFAbstractionConstants.Transport.Headers.MessageId, out var messageId);
            activity?.SetTag(NOFHostingConstants.Outbound.Tags.MessageId, messageId);
            activity?.SetTag(NOFHostingConstants.Outbound.Tags.MessageType, messageType.Name);
            context.Headers.TryGetValue(NOFAbstractionConstants.Transport.Headers.TenantId, out var tenantId);
            activity?.SetTag(NOFHostingConstants.Outbound.Tags.TenantId, tenantId);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    public async ValueTask InvokeAsync(RequestOutboundContext context, object request, RequestOutboundHandlerDelegate next, CancellationToken cancellationToken)
    {
        var rpcMethod = $"{context.ServiceType.DisplayName}.{context.MethodName}";
        using var activity = NOFHostingConstants.Outbound.Source.StartActivity(
            $"Outbound: {rpcMethod}",
            ActivityKind.Producer);
        activity?.SetServiceDeploymentTags(_hostEnvironment);

        var currentActivity = Activity.Current;
        context.Headers[NOFAbstractionConstants.Transport.Headers.TraceId] = currentActivity?.TraceId.ToString();
        context.Headers[NOFAbstractionConstants.Transport.Headers.SpanId] = currentActivity?.SpanId.ToString();

        try
        {
            await next(context, request, cancellationToken);

            context.Headers.TryGetValue(NOFAbstractionConstants.Transport.Headers.MessageId, out var messageId);
            activity?.SetTag(NOFHostingConstants.Outbound.Tags.MessageId, messageId);
            activity?.SetTag(NOFHostingConstants.Outbound.Tags.MessageType, rpcMethod);
            context.Headers.TryGetValue(NOFAbstractionConstants.Transport.Headers.TenantId, out var tenantId);
            activity?.SetTag(NOFHostingConstants.Outbound.Tags.TenantId, tenantId);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }
}
