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
        var headers = new Dictionary<string, object?>
        {
            [NOFConstants.MessageId] = Guid.NewGuid()
        };

        return _rider.SendAsync(command,
            headers,
            destinationEndpointName,
            cancellationToken);
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

        _collector.AddMessage(OutboxMessage.Create(
            id: Guid.NewGuid(),
            message: command,
            destinationEndpointName: destinationEndpointName,
            traceId: currentActivity?.TraceId.ToString(),
            spanId: currentActivity?.SpanId.ToString()
        ));
    }
}