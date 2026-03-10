using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NOF.Application;
using NOF.Infrastructure.Abstraction;

namespace NOF.Infrastructure.Core;

internal sealed class MemoryPersistenceWarningHostedService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MemoryPersistenceWarningHostedService> _logger;

    public MemoryPersistenceWarningHostedService(IServiceProvider serviceProvider, ILogger<MemoryPersistenceWarningHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var transactionManager = scope.ServiceProvider.GetRequiredService<ITransactionManager>();
        var inbox = scope.ServiceProvider.GetRequiredService<IInboxMessageRepository>();
        var outbox = scope.ServiceProvider.GetRequiredService<IOutboxMessageRepository>();
        var tenantRepository = scope.ServiceProvider.GetRequiredService<ITenantRepository>();
        var stateMachineRepository = scope.ServiceProvider.GetRequiredService<IStateMachineContextRepository>();

        if (unitOfWork is InMemoryUnitOfWork &&
            transactionManager is InMemoryTransactionManager &&
            inbox is InMemoryInboxMessageRepository &&
            outbox is InMemoryOutboxMessageRepository &&
            tenantRepository is InMemoryTenantRepository &&
            stateMachineRepository is InMemoryStateMachineContextRepository)
        {
            _logger.LogWarning("NOF is using the built-in in-memory persistence implementation. Data is process-local, non-durable, and does not provide real transactional guarantees. Do not use it as production persistence.");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;
}
