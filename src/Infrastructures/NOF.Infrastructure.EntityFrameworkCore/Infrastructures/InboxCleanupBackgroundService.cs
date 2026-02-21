using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NOF.Application;
using NOF.Infrastructure.Abstraction;

namespace NOF.Infrastructure.EntityFrameworkCore;

/// <summary>
/// Inbox cleanup service that periodically removes old processed messages to maintain database performance.
/// </summary>
internal sealed class InboxCleanupBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<InboxCleanupBackgroundService> _logger;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(1);
    private readonly TimeSpan _retentionPeriod = TimeSpan.FromDays(7);

    public InboxCleanupBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<InboxCleanupBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Inbox cleanup service started. Cleanup interval: {Interval}, Retention period: {Retention}",
            _cleanupInterval, _retentionPeriod);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_cleanupInterval, stoppingToken);
                await CleanupInboxAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during inbox cleanup");
            }
        }

        _logger.LogInformation("Inbox cleanup service stopped");
    }

    private async Task CleanupInboxAsync(CancellationToken cancellationToken)
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
                _logger.LogDebug("Cleaning inbox messages for tenant {TenantId}", tenant.Id);

                // Use the current scope's DbContext, which automatically uses the set tenant context
                var dbContext = scope.ServiceProvider.GetRequiredService<NOFDbContext>();

                var olderThan = DateTime.UtcNow - _retentionPeriod;
                var deletedCount = await dbContext.Set<EFCoreInboxMessage>()
                    .Where(m => m.CreatedAt < olderThan)
                    .ExecuteDeleteAsync(cancellationToken);

                if (deletedCount > 0)
                {
                    _logger.LogInformation(
                        "Tenant {TenantId} inbox cleanup completed. Deleted {Count} processed messages older than {Date}",
                        tenant.Id, deletedCount, olderThan);
                }
                else
                {
                    _logger.LogDebug("Tenant {TenantId} inbox cleanup completed. No messages to delete", tenant.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning inbox messages for tenant {TenantId}", tenant.Id);
            }
        }

        // Restore the original tenant context
        invocationContext.SetTenantId(originalTenantId);
    }
}
