using System.Diagnostics;

namespace NOF;

public interface ICommandSender
{
    Task SendAsync(ICommand command, string? destinationEndpointName = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// 延迟命令发送器接口
/// 用于在不使用 HandlerBase 的情况下手动添加命令到事务性上下文
/// </summary>
public interface IDeferredCommandSender
{
    /// <summary>
    /// 添加命令到事务性上下文
    /// 命令将在 UnitOfWork.SaveChangesAsync 时统一持久化到 Outbox
    /// </summary>
    void Send(ICommand command, string? destinationEndpointName = null);
}

/// <summary>
/// 延迟命令发送器实现
/// </summary>
public sealed class DeferredCommandSender : IDeferredCommandSender
{
    private readonly ITransactionalMessageCollector _collector;

    public DeferredCommandSender(ITransactionalMessageCollector collector)
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