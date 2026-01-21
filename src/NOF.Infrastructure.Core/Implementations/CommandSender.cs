using System.Diagnostics;

namespace NOF;

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

        _collector.AddMessage(new OutboxMessage
        {
            Message = command,
            DestinationEndpointName = destinationEndpointName,
            CreatedAt = DateTimeOffset.UtcNow,
            TraceId = currentActivity?.TraceId.ToString(),
            SpanId = currentActivity?.SpanId.ToString()
        });
    }
}