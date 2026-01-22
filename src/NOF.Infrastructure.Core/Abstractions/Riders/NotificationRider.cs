namespace NOF;

public interface INotificationRider
{
    Task PublishAsync(INotification notification,
        IDictionary<string, object?>? headers = null,
        CancellationToken cancellationToken = default);
}
