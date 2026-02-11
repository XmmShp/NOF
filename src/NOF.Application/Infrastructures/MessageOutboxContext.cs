using NOF.Contract;
using System.ComponentModel;

namespace NOF.Application;

/// <summary>
/// Transactional message outbox context, propagated automatically via AsyncLocal.
/// Provides pass-through methods for HandlerBase.
/// Dependency chain: Context -> Sender -> Collector
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
    /// Creates a new outbox context scope.
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
    /// Adds a command to the transactional outbox context (pass-through for HandlerBase).
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
    /// Adds a notification to the transactional outbox context (pass-through for HandlerBase).
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
