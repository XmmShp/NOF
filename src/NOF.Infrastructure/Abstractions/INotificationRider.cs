namespace NOF.Infrastructure;

public interface INotificationRider
{
    Task PublishAsync(object notification,
        Type[] notificationTypes,
        IEnumerable<KeyValuePair<string, string?>>? headers,
        CancellationToken cancellationToken = default);
}
