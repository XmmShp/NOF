namespace NOF;

public interface INotificationRider
{
    Task PublishAsync(INotification notification,
        IDictionary<string, string?>? headers = null,
        CancellationToken cancellationToken = default);
}
