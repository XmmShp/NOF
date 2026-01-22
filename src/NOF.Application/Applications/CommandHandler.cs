using System.ComponentModel;

namespace NOF;

[EditorBrowsable(EditorBrowsableState.Never)]
public interface IMessageHandler;

[EditorBrowsable(EditorBrowsableState.Never)]
public interface ICommandHandler : IMessageHandler;

public interface ICommandHandler<TCommand> : ICommandHandler
    where TCommand : class, ICommand
{
    Task HandleAsync(TCommand command, CancellationToken cancellationToken);
}

/// <summary>
/// CommandHandler 基类，提供事务性命令发送能力
/// 无需注入任何依赖，通过 AsyncLocal 自动工作
/// </summary>
public abstract class CommandHandler<TCommand> : HandlerBase, ICommandHandler<TCommand>
    where TCommand : class, ICommand
{
    public abstract Task HandleAsync(TCommand command, CancellationToken cancellationToken);
}

/// <summary>
/// Handler 基类，提供事务性消息发送能力
/// 无需注入任何依赖，通过 AsyncLocal 自动工作
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public abstract class HandlerBase
{
    /// <summary>
    /// 添加命令到事务性上下文
    /// 命令将在 UnitOfWork.SaveChangesAsync 时统一持久化到 Outbox
    /// </summary>
    /// <param name="command">要发送的命令</param>
    /// <param name="destinationEndpointName">可选的目标端点名称</param>
    protected void SendCommand(ICommand command, string? destinationEndpointName = null)
    {
        MessageOutboxContext.AddCommand(command, destinationEndpointName);
    }

    /// <summary>
    /// 添加通知到事务性上下文
    /// 通知将在 UnitOfWork.SaveChangesAsync 时统一持久化到 Outbox
    /// </summary>
    /// <param name="notification">要发送的通知</param>
    protected void PublishNotification(INotification notification)
    {
        MessageOutboxContext.AddNotification(notification);
    }
}