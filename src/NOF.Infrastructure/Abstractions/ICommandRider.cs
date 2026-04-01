using NOF.Contract;

namespace NOF.Infrastructure;

public interface ICommandRider
{
    Task SendAsync(ICommand command,
        IDictionary<string, string?>? headers = null,
        CancellationToken cancellationToken = default);
}
