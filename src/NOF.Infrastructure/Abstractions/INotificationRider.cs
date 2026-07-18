namespace NOF.Infrastructure;

public interface INotificationRider
{
    Task PublishAsync(ReadOnlyMemory<byte> payload,
        string messageRoute,
        IEnumerable<KeyValuePair<string, string?>>? headers,
        CancellationToken cancellationToken = default);
}
