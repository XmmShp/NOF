namespace NOF.Infrastructure;

public interface ICommandRider
{
    Task SendAsync(object command,
        Type commandType,
        IEnumerable<KeyValuePair<string, string?>>? headers,
        CancellationToken cancellationToken = default);
}
