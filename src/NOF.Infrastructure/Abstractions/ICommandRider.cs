using NOF.Application;
using NOF.Contract;

namespace NOF.Infrastructure;

public interface ICommandRider
{
    Task SendAsync(ICommand command,
        IExecutionContext executionContext,
        CancellationToken cancellationToken = default);
}
