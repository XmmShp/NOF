using System.Diagnostics;

namespace NOF.Infrastructure.Core;

/// <summary>
/// Outermost outbound middleware step — creates the tracing activity span that wraps
/// the entire outbound pipeline (header population + dispatch).
/// Runs first so it can wrap everything.
/// </summary>
public class TracingOutboundMiddlewareStep : IOutboundMiddlewareStep<TracingOutboundMiddleware>;

/// <summary>
/// Outbound middleware that:
/// 1. Creates a producer <see cref="Activity"/> span for the outbound message.
/// 2. Propagates trace/span IDs into outbound message headers.
/// 3. Calls <c>next</c> (inner middleware populate headers, then dispatch runs).
/// 4. Sets activity tags from populated headers and sets span status.
/// </summary>
public sealed class TracingOutboundMiddleware : IOutboundMiddleware
{
    public async ValueTask InvokeAsync(OutboundContext context, OutboundDelegate next, CancellationToken cancellationToken)
    {
        using var activity = NOFInfrastructureCoreConstants.Messaging.Source.StartActivity(
            $"{NOFInfrastructureCoreConstants.Messaging.ActivityNames.MessageSending}: {context.Message.GetType().FullName}",
            ActivityKind.Producer);

        // Propagate trace/span IDs into headers before inner middleware run
        var currentActivity = Activity.Current;
        if (currentActivity is not null)
        {
            context.Headers.TryAdd(NOFInfrastructureCoreConstants.Transport.Headers.TraceId, currentActivity.TraceId.ToString());
            context.Headers.TryAdd(NOFInfrastructureCoreConstants.Transport.Headers.SpanId, currentActivity.SpanId.ToString());
        }

        try
        {
            // Inner middleware populate remaining headers, then dispatch runs
            await next(cancellationToken);

            // Set tags from populated headers after successful dispatch
            if (activity is { IsAllDataRequested: true })
            {
                context.Headers.TryGetValue(NOFInfrastructureCoreConstants.Transport.Headers.MessageId, out var messageId);
                activity.SetTag(NOFInfrastructureCoreConstants.Messaging.Tags.MessageId, messageId);
                activity.SetTag(NOFInfrastructureCoreConstants.Messaging.Tags.MessageType, context.Message.GetType().Name);
                activity.SetTag(NOFInfrastructureCoreConstants.Messaging.Tags.Destination, context.DestinationEndpointName ?? "default");

                context.Headers.TryGetValue(NOFInfrastructureCoreConstants.Transport.Headers.TenantId, out var tenantId);
                activity.SetTag(NOFInfrastructureCoreConstants.Messaging.Tags.TenantId, tenantId);
            }

            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }
}
