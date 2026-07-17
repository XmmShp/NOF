namespace NOF.Infrastructure;

public interface ICommandRider
{
    Task SendAsync(ReadOnlyMemory<byte> payload,
        string payloadTypeName,
        string dispatchRoute,
        IEnumerable<KeyValuePair<string, string?>>? headers,
        CancellationToken cancellationToken = default);
}
