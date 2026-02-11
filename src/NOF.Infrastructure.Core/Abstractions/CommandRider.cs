using NOF.Contract;

namespace NOF.Infrastructure.Core;

public interface ICommandRider
{
    Task SendAsync(ICommand command,
        IDictionary<string, string?>? headers = null,
        string? destinationEndpointName = null,
        CancellationToken cancellationToken = default);
}
