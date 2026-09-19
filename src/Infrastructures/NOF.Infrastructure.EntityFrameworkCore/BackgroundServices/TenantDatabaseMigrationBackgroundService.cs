using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NOF.Abstraction;
using NOF.Contract;
using NOF.Hosting;

namespace NOF.Infrastructure.EntityFrameworkCore;

/// <summary>
/// Migrates tenant databases in the background after application startup.
/// Host database migration is completed separately during initialization.
/// </summary>
public sealed class TenantDatabaseMigrationBackgroundService(
    IServiceProvider services,
    IEnumerable<IApplicationInitializationStep> initializationSteps,
    ITenantProvider tenantProvider,
    IOptions<DbContextConfigurationOptions> options,
    IHostApplicationLifetime applicationLifetime,
    ILogger<TenantDatabaseMigrationBackgroundService> logger) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (options.Value.TenantMode != TenantMode.DatabasePerTenant)
        {
            return;
        }

        try
        {
            using (var startedOrStopping = CancellationTokenSource.CreateLinkedTokenSource(
                applicationLifetime.ApplicationStarted, stoppingToken))
            {
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, startedOrStopping.Token);
                }
                catch (OperationCanceledException) when (startedOrStopping.IsCancellationRequested)
                {
                }
            }

            stoppingToken.ThrowIfCancellationRequested();
            using var hostContext = Context.PushCurrent(Context.Empty.WithTenantId(NOFAbstractionConstants.Tenant.HostId));
            var steps = initializationSteps.OfType<DbContextMigrationInitializationStep>().ToArray();
            var seen = new HashSet<string>(StringComparer.Ordinal) { NOFAbstractionConstants.Tenant.HostId };
            await foreach (var tenantId in tenantProvider.GetTenantIdsAsync(stoppingToken).WithCancellation(stoppingToken))
            {
                stoppingToken.ThrowIfCancellationRequested();
                var normalizedTenantId = TenantId.Normalize(tenantId);
                if (!seen.Add(normalizedTenantId))
                {
                    continue;
                }

                foreach (var step in steps)
                {
                    try
                    {
                        logger.LogInformation("Migrating {DbContextType} for tenant {TenantId}.", step.DbContextType.Name, normalizedTenantId);
                        await step.MigrateTenantAsync(services, normalizedTenantId, stoppingToken);
                        logger.LogInformation("Migrated {DbContextType} for tenant {TenantId}.", step.DbContextType.Name, normalizedTenantId);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception exception)
                    {
                        logger.LogError(exception,
                            "Migration failed for {DbContextType}, tenant {TenantId}; remaining tenants will still be processed.",
                            step.DbContextType.Name, normalizedTenantId);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Tenant database migration was canceled during application shutdown.");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unable to enumerate tenants for database migration.");
        }
    }
}
