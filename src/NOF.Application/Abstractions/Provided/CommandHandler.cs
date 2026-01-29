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