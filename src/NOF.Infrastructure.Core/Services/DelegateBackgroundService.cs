using Microsoft.Extensions.Hosting;

namespace NOF.Infrastructure.Core;

internal sealed class DelegateBackgroundService : BackgroundService
{
    private readonly Func<IServiceProvider, CancellationToken, Task> _startAction;
    private readonly IServiceProvider _serviceProvider;

    public DelegateBackgroundService(IServiceProvider serviceProvider, Func<IServiceProvider, CancellationToken, Task> startAction)
    {
        ArgumentNullException.ThrowIfNull(startAction);
        _serviceProvider = serviceProvider;
        _startAction = startAction;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return _startAction(_serviceProvider, stoppingToken);
    }
}
