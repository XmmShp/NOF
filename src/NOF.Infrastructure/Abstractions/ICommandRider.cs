namespace NOF.Infrastructure;

public interface ICommandRider
{
    Task SendAsync(ReadOnlyMemory<byte> payload,
        string messageRoute,
        IEnumerable<KeyValuePair<string, string?>>? headers,
        CancellationToken cancellationToken = default);
}
