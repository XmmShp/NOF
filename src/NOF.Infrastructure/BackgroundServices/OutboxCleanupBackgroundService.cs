using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
namespace NOF.Infrastructure;

/// <summary>
/// Outbox cleanup service that periodically removes old sent messages to maintain database performance.
/// </summary>
internal sealed class OutboxCleanupBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxCleanupBackgroundService> _logger;
    private readonly TransactionalMessageProcessorOptions _options;

    public OutboxCleanupBackgroundService(
        IServiceProvider serviceProvider,
        IOptions<TransactionalMessageOptions> options,
        ILogger<OutboxCleanupBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value.Outbox;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Outbox cleanup service started. Cleanup interval: {Interval}, Retention period: {Retention}",
            _options.CleanupInterval, _options.RetentionPeriod);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_options.CleanupInterval, stoppingToken);
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
        var dbContext = scope.ServiceProvider.GetRequiredService<DbContext>();
        var olderThan = DateTime.UtcNow - _options.RetentionPeriod;
        var deletedCount = await dbContext.Set<NOFOutboxMessage>()
            .Where(m => m.Status == OutboxMessageStatus.Sent)
            .Where(m => m.SentAt != null && m.SentAt < olderThan)
            .ExecuteDeleteAsync(cancellationToken);

        if (deletedCount > 0)
        {
            _logger.LogInformation(
                "Outbox cleanup completed. Deleted {Count} sent messages older than {Date}",
                deletedCount, olderThan);
        }
        else
        {
            _logger.LogDebug("Outbox cleanup completed. No messages to delete");
        }
    }
}
