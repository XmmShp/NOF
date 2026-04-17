using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NOF.Infrastructure;

/// <summary>
/// Inbox cleanup service that periodically removes old processed messages to maintain database performance.
/// </summary>
internal sealed class InboxCleanupBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<InboxCleanupBackgroundService> _logger;
    private readonly TransactionalMessageProcessorOptions _options;

    public InboxCleanupBackgroundService(
        IServiceProvider serviceProvider,
        IOptions<TransactionalMessageOptions> options,
        ILogger<InboxCleanupBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value.Inbox;
        _logger = logger;
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
        var dbContext = scope.ServiceProvider.GetRequiredService<NOFDbContext>();
        var olderThan = DateTime.UtcNow - _options.RetentionPeriod;
        var deletedCount = await dbContext.NOFInboxMessages
            .Where(m => m.Status == InboxMessageStatus.Processed)
            .Where(m => m.ProcessedAt != null && m.ProcessedAt < olderThan)
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
