using System.ComponentModel;

namespace NOF;

/// <summary>
/// 事务性消息收集上下文，通过 AsyncLocal 自动传播
/// 为 HandlerBase 提供便捷的透传方法
/// 依赖链: Context -> Sender -> Collector
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class MessageOutboxContext
{
    private static readonly AsyncLocal<MessageOutboxContext?> _current = new();

    private readonly IDeferredCommandSender _commandSender;
    private readonly IDeferredNotificationPublisher _notificationPublisher;

    private MessageOutboxContext(
        IDeferredCommandSender commandSender,
        IDeferredNotificationPublisher notificationPublisher)
    {
        _commandSender = commandSender;
        _notificationPublisher = notificationPublisher;
    }

    public static MessageOutboxContext? Current
    {
        get => _current.Value;
        private set => _current.Value = value;
    }

    /// <summary>
    /// 创建新的上下文作用域
    /// </summary>
    public static IDisposable BeginScope(
        IDeferredCommandSender commandSender,
        IDeferredNotificationPublisher notificationPublisher)
    {
        var context = new MessageOutboxContext(commandSender, notificationPublisher);
        Current = context;
        return new ContextScope(context);
    }

    /// <summary>
    /// 添加命令到事务性上下文（透传方法，供 HandlerBase 使用）
    /// </summary>
    public static void AddCommand(ICommand command, string? destinationEndpointName = null)
    {
        var context = Current;

        if (context == null)
        {
            throw new InvalidOperationException(
                "TransactionalMessageContext is not available. " +
                "Ensure the operation is executed within a UnitOfWork scope.");
        }

        context._commandSender.Send(command, destinationEndpointName);
    }

    /// <summary>
    /// 添加通知到事务性上下文（透传方法，供 HandlerBase 使用）
    /// </summary>
    public static void AddNotification(INotification notification)
    {
        var context = Current;

        if (context == null)
        {
            throw new InvalidOperationException(
                "TransactionalMessageContext is not available. " +
                "Ensure the operation is executed within a UnitOfWork scope.");
        }

        context._notificationPublisher.Publish(notification);
    }

    private sealed class ContextScope : IDisposable
    {
        private readonly MessageOutboxContext _outboxContext;

        public ContextScope(MessageOutboxContext outboxContext)
        {
            _outboxContext = outboxContext;
        }

        public void Dispose()
        {
            if (Current == _outboxContext)
            {
                Current = null;
            }
        }
    }
}
