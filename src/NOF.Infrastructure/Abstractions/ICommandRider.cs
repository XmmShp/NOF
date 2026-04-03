using NOF.Contract;

namespace NOF.Infrastructure;

public interface ICommandRider
{
    Task SendAsync(ICommand command,
        IEnumerable<KeyValuePair<string, string?>>? headers,
        CancellationToken cancellationToken = default);
}
