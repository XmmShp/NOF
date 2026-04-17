namespace NOF.Infrastructure;

public interface INotificationRider
{
    Task PublishAsync(ReadOnlyMemory<byte> payload,
        string payloadTypeName,
        IReadOnlyCollection<string> notificationTypeNames,
        IEnumerable<KeyValuePair<string, string?>>? headers,
        CancellationToken cancellationToken = default);
}
