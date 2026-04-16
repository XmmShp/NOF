using NOF.Abstraction;
using System.Diagnostics;

namespace NOF.Hosting;

public sealed class TracingOutboundMiddleware : AllMessagesOutboundMiddleware
{
    protected override async ValueTask InvokeAsyncCore(MessageOutboundContext context, Func<CancellationToken, ValueTask> next, CancellationToken cancellationToken)
    {
        var messageTypeFullName = context.MessageName ?? "<null>";
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
            activity?.SetTag(NOFHostingConstants.Outbound.Tags.MessageType, context.Message?.GetType().Name);
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
