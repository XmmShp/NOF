namespace NOF.Infrastructure;

public interface INotificationRider
{
    Task PublishAsync(object notification,
        IEnumerable<KeyValuePair<string, string?>>? headers,
        CancellationToken cancellationToken = default);
}
