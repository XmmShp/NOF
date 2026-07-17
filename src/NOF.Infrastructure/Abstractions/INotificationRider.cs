namespace NOF.Infrastructure;

public interface INotificationRider
{
    Task PublishAsync(ReadOnlyMemory<byte> payload,
        IReadOnlyCollection<string> messageRoutes,
        IEnumerable<KeyValuePair<string, string?>>? headers,
        CancellationToken cancellationToken = default);
}
