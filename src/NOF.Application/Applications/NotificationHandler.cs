using System.ComponentModel;

namespace NOF;

[EditorBrowsable(EditorBrowsableState.Never)]
public interface INotificationHandler : IMessageHandler;

public interface INotificationHandler<in TNotification> : INotificationHandler
    where TNotification : class, INotification
{
    Task HandleAsync(TNotification notification, CancellationToken cancellationToken);
}

/// <summary>
/// NotificationHandler 基类，提供事务性消息发送能力
/// 无需注入任何依赖，通过 AsyncLocal 自动工作
/// </summary>
public abstract class NotificationHandler<TNotification> : HandlerBase, INotificationHandler<TNotification>
    where TNotification : class, INotification
{
    public abstract Task HandleAsync(TNotification notification, CancellationToken cancellationToken);
}
