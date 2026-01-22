using System.Diagnostics;

namespace NOF;

/// <summary>
/// 延迟命令发送器实现
/// </summary>
public sealed class CommandSender : ICommandSender
{
    private readonly ICommandRider _rider;

    public CommandSender(ICommandRider rider)
    {
        _rider = rider;
    }

    public Task SendAsync(ICommand command, string? destinationEndpointName = null, CancellationToken cancellationToken = default)
    {
        using var activity = MessageTracing.Source.StartActivity(
            $"{MessageTracing.ActivityNames.MessageSending}: {command.GetType().FullName}",
            ActivityKind.Producer);

        var messageId = Guid.NewGuid().ToString();
        var currentActivity = Activity.Current;
        var headers = new Dictionary<string, string?>
        {
            [NOFConstants.MessageId] = messageId,
            [NOFConstants.TraceId] = currentActivity?.TraceId.ToString(),
            [NOFConstants.SpanId] = currentActivity?.SpanId.ToString()
        };

        if (activity is { IsAllDataRequested: true })
        {
            activity.SetTag(MessageTracing.Tags.MessageId, messageId);
            activity.SetTag(MessageTracing.Tags.MessageType, command.GetType().Name);
            activity.SetTag(MessageTracing.Tags.Destination, destinationEndpointName ?? "default");
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
/// 延迟命令发送器实现
/// </summary>
public sealed class DeferredCommandSender : IDeferredCommandSender
{
    private readonly IOutboxMessageCollector _collector;

    public DeferredCommandSender(IOutboxMessageCollector collector)
    {
        _collector = collector;
    }

    public void Send(ICommand command, string? destinationEndpointName = null)
    {
        var currentActivity = Activity.Current;

        var headers = new Dictionary<string, string?>();

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