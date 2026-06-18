using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NOF.Application;

namespace NOF.Infrastructure;

/// <summary>
/// Inbox cleanup service that periodically removes old processed messages to maintain database performance.
/// </summary>
internal sealed class InboxCleanupBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<InboxCleanupBackgroundService> _logger;
    private readonly TransactionalMessageProcessorOptions _options;
    private readonly IHostEnvironment _hostEnvironment;

    public InboxCleanupBackgroundService(
        IServiceProvider serviceProvider,
        IOptions<TransactionalMessageOptions> options,
        ILogger<InboxCleanupBackgroundService> logger,
        IHostEnvironment hostEnvironment)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value.Inbox;
        _logger = logger;
        _hostEnvironment = hostEnvironment;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Inbox cleanup service started. Cleanup interval: {Interval}, Retention period: {Retention}",
            _options.CleanupInterval, _options.RetentionPeriod);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_options.CleanupInterval, stoppingToken);
                if (!_hostEnvironment.IsPrimaryNodeEnvironment)
                {
                    _logger.LogDebug(
                        "Skipping inbox cleanup on non-primary node {InstanceId}",
                        _hostEnvironment.InstanceId);
                    continue;
                }

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
        scope.ServiceProvider.ResolveDaemonServices();
        var dbContext = scope.ServiceProvider.GetService<IDbContext>();
        if (dbContext is null)
        {
            _logger.LogDebug("Skipping inbox cleanup because no IDbContext provider is registered.");
            return;
        }
        var olderThan = DateTime.UtcNow - _options.RetentionPeriod;
        var deletedCount = await dbContext.Set<NOFInboxMessage>()
            .Where(m => m.Status == InboxMessageStatus.Processed)
            .Where(m => m.ProcessedAtUtc != null && m.ProcessedAtUtc < olderThan)
            .ExecuteDeleteAsync(cancellationToken);

        if (deletedCount > 0)
        {
            _logger.LogInformation(
                "Inbox cleanup completed. Deleted {Count} processed messages older than {Date}",
                deletedCount, olderThan);
        }
        else
        {
            _logger.LogDebug("Inbox cleanup completed. No messages to delete");
        }
    }
}
