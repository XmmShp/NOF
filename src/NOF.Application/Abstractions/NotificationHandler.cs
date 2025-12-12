namespace NOF;

public interface INotificationHandler;

public interface INotificationHandler<TNotification> : INotificationHandler
    where TNotification : class, INotification
{
    Task HandleAsync(TNotification notification, CancellationToken cancellationToken);
}
