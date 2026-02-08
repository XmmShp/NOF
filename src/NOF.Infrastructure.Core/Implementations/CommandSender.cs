using System.Diagnostics;

namespace NOF;

/// <summary>
/// Command sender implementation.
/// </summary>
public sealed class CommandSender : ICommandSender
{
    private readonly ICommandRider _rider;
    private readonly IInvocationContext _invocationContext;

    public CommandSender(ICommandRider rider, IInvocationContext invocationContext)
    {
        _rider = rider;
        _invocationContext = invocationContext;
    }

    public Task SendAsync(ICommand command, string? destinationEndpointName = null, CancellationToken cancellationToken = default)
    {
        using var activity = MessageTracing.Source.StartActivity(
            $"{MessageTracing.ActivityNames.MessageSending}: {command.GetType().FullName}",
            ActivityKind.Producer);

        var messageId = Guid.NewGuid().ToString();
        var tenantId = _invocationContext.TenantId;
        var currentActivity = Activity.Current;
        var headers = new Dictionary<string, string?>
        {
            [NOFConstants.MessageId] = messageId,
            [NOFConstants.TraceId] = currentActivity?.TraceId.ToString(),
            [NOFConstants.SpanId] = currentActivity?.SpanId.ToString(),

            [NOFConstants.TenantId] = tenantId
        };

        if (activity is { IsAllDataRequested: true })
        {
            activity.SetTag(MessageTracing.Tags.MessageId, messageId);
            activity.SetTag(MessageTracing.Tags.MessageType, command.GetType().Name);
            activity.SetTag(MessageTracing.Tags.Destination, destinationEndpointName ?? "default");

            activity.SetTag(MessageTracing.Tags.TenantId, tenantId);
        }

        try
        {
            var result = _rider.SendAsync(command, headers, destinationEndpointName, cancellationToken);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return result;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }
}

/// <summary>
/// Deferred command sender implementation.
/// </summary>
public sealed class DeferredCommandSender : IDeferredCommandSender
{
    private readonly IOutboxMessageCollector _collector;
    private readonly IInvocationContext _invocationContext;

    public DeferredCommandSender(IOutboxMessageCollector collector, IInvocationContext invocationContext)
    {
        _collector = collector;
        _invocationContext = invocationContext;
    }

    public void Send(ICommand command, string? destinationEndpointName = null)
    {
        var currentActivity = Activity.Current;
        var tenantId = _invocationContext.TenantId;

        var headers = new Dictionary<string, string?>
        {
            [NOFConstants.TenantId] = tenantId
        };

        _collector.AddMessage(new OutboxMessage
        {
            Message = command,
            DestinationEndpointName = destinationEndpointName,
            Headers = headers,
            TraceId = currentActivity?.TraceId,
            SpanId = currentActivity?.SpanId
        });
    }
}