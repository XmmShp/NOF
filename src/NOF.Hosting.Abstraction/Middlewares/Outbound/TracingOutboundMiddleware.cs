using NOF.Abstraction;
using System.Diagnostics;

namespace NOF.Hosting;

public sealed class CommandTracingOutboundMiddleware : ICommandOutboundMiddleware
{
    public async ValueTask InvokeAsync(CommandOutboundContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        var messageTypeFullName = context.MessageType.FullName ?? context.MessageType.Name;
        using var activity = NOFHostingConstants.Outbound.Source.StartActivity(
            $"Outbound: {messageTypeFullName}",
            ActivityKind.Producer);

        var currentActivity = Activity.Current;
        context.Headers[NOFAbstractionConstants.Transport.Headers.TraceId] = currentActivity?.TraceId.ToString();
        context.Headers[NOFAbstractionConstants.Transport.Headers.SpanId] = currentActivity?.SpanId.ToString();

        try
        {
            await next(cancellationToken);

            context.Headers.TryGetValue(NOFAbstractionConstants.Transport.Headers.MessageId, out var messageId);
            activity?.SetTag(NOFHostingConstants.Outbound.Tags.MessageId, messageId);
            activity?.SetTag(NOFHostingConstants.Outbound.Tags.MessageType, context.MessageType.Name);
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

public sealed class NotificationTracingOutboundMiddleware : INotificationOutboundMiddleware
{
    public async ValueTask InvokeAsync(NotificationOutboundContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        var messageTypeFullName = context.MessageType.FullName ?? context.MessageType.Name;
        using var activity = NOFHostingConstants.Outbound.Source.StartActivity(
            $"Outbound: {messageTypeFullName}",
            ActivityKind.Producer);

        var currentActivity = Activity.Current;
        context.Headers[NOFAbstractionConstants.Transport.Headers.TraceId] = currentActivity?.TraceId.ToString();
        context.Headers[NOFAbstractionConstants.Transport.Headers.SpanId] = currentActivity?.SpanId.ToString();

        try
        {
            await next(cancellationToken);

            context.Headers.TryGetValue(NOFAbstractionConstants.Transport.Headers.MessageId, out var messageId);
            activity?.SetTag(NOFHostingConstants.Outbound.Tags.MessageId, messageId);
            activity?.SetTag(NOFHostingConstants.Outbound.Tags.MessageType, context.MessageType.Name);
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

public sealed class RequestTracingOutboundMiddleware : IRequestOutboundMiddleware
{
    public async ValueTask InvokeAsync(RequestOutboundContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        var rpcMethod = $"{context.ServiceType.FullName ?? context.ServiceType.Name}.{context.MethodName}";
        using var activity = NOFHostingConstants.Outbound.Source.StartActivity(
            $"Outbound: {rpcMethod}",
            ActivityKind.Producer);

        var currentActivity = Activity.Current;
        context.Headers[NOFAbstractionConstants.Transport.Headers.TraceId] = currentActivity?.TraceId.ToString();
        context.Headers[NOFAbstractionConstants.Transport.Headers.SpanId] = currentActivity?.SpanId.ToString();

        try
        {
            await next(cancellationToken);

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
