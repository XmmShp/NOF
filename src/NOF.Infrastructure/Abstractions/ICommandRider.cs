namespace NOF.Infrastructure;

public interface ICommandRider
{
    Task SendAsync(object command,
        IEnumerable<KeyValuePair<string, string?>>? headers,
        CancellationToken cancellationToken = default);
}
