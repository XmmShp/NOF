using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NOF;

/// <summary>
/// Outbox cleanup service that periodically removes old sent messages to maintain database performance.
/// </summary>
internal sealed class OutboxCleanupBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxCleanupBackgroundService> _logger;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(1);
    private readonly TimeSpan _retentionPeriod = TimeSpan.FromDays(7);

    public OutboxCleanupBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<OutboxCleanupBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Outbox cleanup service started. Cleanup interval: {Interval}, Retention period: {Retention}",
            _cleanupInterval, _retentionPeriod);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_cleanupInterval, stoppingToken);
                await CleanupOutboxAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during outbox cleanup");
            }
        }

        _logger.LogInformation("Outbox cleanup service stopped");
    }

    private async Task CleanupOutboxAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var tenantRepository = scope.ServiceProvider.GetRequiredService<ITenantRepository>();
        var invocationContext = scope.ServiceProvider.GetRequiredService<IInvocationContextInternal>();

        // Get all tenants
        var tenants = await tenantRepository.GetAllAsync();
        
        // Save the original tenant context
        var originalTenantId = invocationContext.TenantId;
        
        foreach (var tenant in tenants)
        {
            if (!tenant.IsActive)
            {
                _logger.LogDebug("Skipping inactive tenant {TenantId}", tenant.Id);
                continue;
            }

            try
            {
                // Set the tenant context
                invocationContext.SetTenantId(tenant.Id);
                _logger.LogDebug("Cleaning outbox messages for tenant {TenantId}", tenant.Id);

                // Use the current scope's DbContext, which automatically uses the set tenant context
                var dbContext = scope.ServiceProvider.GetRequiredService<DbContext>();

                var olderThan = DateTimeOffset.UtcNow - _retentionPeriod;
                var deletedCount = await dbContext.Set<EFCoreOutboxMessage>()
                    .Where(m => m.Status == OutboxMessageStatus.Sent)
                    .Where(m => m.SentAt != null && m.SentAt < olderThan)
                    .ExecuteDeleteAsync(cancellationToken);

                if (deletedCount > 0)
                {
                    _logger.LogInformation(
                        "Tenant {TenantId} outbox cleanup completed. Deleted {Count} sent messages older than {Date}",
                        tenant.Id, deletedCount, olderThan);
                }
                else
                {
                    _logger.LogDebug("Tenant {TenantId} outbox cleanup completed. No messages to delete", tenant.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning outbox messages for tenant {TenantId}", tenant.Id);
            }
        }

        // Restore the original tenant context
        invocationContext.SetTenantId(originalTenantId);
    }
}
